"""Create shaped synthetic images and compare real local OCR language modes."""
import argparse
import hashlib
import json
import re
import unicodedata
from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageFont, features

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "apps/api"))
from app.local_extraction import LocalInvoiceExtractor  # noqa: E402
from app.extraction import ExtractionError  # noqa: E402
from phase7_evaluate import evaluate, text_page  # noqa: E402

def render_text(line, profile):
    if profile == "legacy-v1":
        return line
    # Isolate Latin identifiers and numeric runs so an RTL base does not reverse
    # ISO date components. These layout controls are not added to source labels.
    run = r"[A-Za-z0-9]+(?:[-.,/][A-Za-z0-9]+)*"
    pattern = rf"{run}(?:[ \t]+{run})*" if profile == "isolated-v3" else run
    return re.sub(pattern,
                  lambda match: "\u2066" + match.group() + "\u2069", line)


def line_is_rtl(line):
    for char in line:
        direction = unicodedata.bidirectional(char)
        if direction in {"L", "R", "AL"}:
            return direction != "L"
    return False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path,
                        default=ROOT / "samples/phase-7/language-v0.2.0/cases.json")
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--render-profile", choices=("legacy-v1", "isolated-v2", "isolated-v3"), default="isolated-v3")
    parser.add_argument("--arabic-model", choices=("fast", "best"), default="fast")
    parser.add_argument("--layout-profile", choices=("block", "lines-v1"), default="block")
    parser.add_argument("--modes", nargs="+", choices=("eng", "ara", "ara+eng", "eng+ara"),
                        default=("eng", "ara", "ara+eng", "eng+ara"))
    args = parser.parse_args()
    if args.layout_profile == "lines-v1" and any(mode not in {"eng+ara", "ara+eng"} for mode in args.modes):
        parser.error("lines-v1 requires --modes eng+ara and/or ara+eng")
    args.output_dir.mkdir(parents=True, exist_ok=False)
    if not features.check_feature("raqm"):
        raise RuntimeError("Arabic fixture rendering requires RAQM")
    font_path = Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
    font = ImageFont.truetype(str(font_path), 44)
    path = args.dataset
    dataset = json.loads(path.read_text(encoding="utf-8"))
    results, fixtures = [], []
    for case in dataset["cases"]:
        image_path = args.output_dir / (case["id"] + ".png")
        lines = case["text"].splitlines()
        image = Image.new("RGB", (2600, max(900, 100 + 100 * len(lines))), "white")
        draw = ImageDraw.Draw(image)
        for index, line in enumerate(lines):
            rtl = case["language"] != "en" if args.render_profile == "legacy-v1" else line_is_rtl(line)
            draw.text((2500 if rtl else 100, 70 + index * 100), render_text(line, args.render_profile), font=font,
                      fill="black", direction="rtl" if rtl else "ltr", anchor="rt" if rtl else "lt")
        image.save(image_path, dpi=(300, 300))
        fixtures.append({"id": case["id"], "sha256": hashlib.sha256(image_path.read_bytes()).hexdigest()})
        for language in args.modes:
            extractor = LocalInvoiceExtractor(language=language, arabic_model=args.arabic_model,
                                              layout_profile=args.layout_profile)
            try:
                result = extractor.extract(image_path, "image/png")
                pages = [text_page(page.canonical_text) for page in result.pages]
                score = evaluate(case, pages, fields=result.fields, warnings=result.warnings)
                exact = all(page.canonical_text.encode()[span.start_utf8:span.end_utf8].decode() == span.quote
                            for field in result.fields for span in field.evidence
                            for page in result.pages if page.page_number == span.page_number)
                score.update({"mode": language, "provider": result.provider.model,
                              "modelVersion": result.provider.model_version,
                              "durationMs": result.metrics.provider_duration_ms,
                              "canonicalText": [page.canonical_text for page in result.pages],
                              "pipelineExactEvidence": exact})
                score["passed"] = score["passed"] and exact
            except ExtractionError as error:
                score = {"id": case["id"], "language": case["language"], "mode": language,
                         "passed": False, "error": error.code}
                # Synthetic evaluation only: retain the contract diagnostic for
                # local debugging; public application errors stay deliberately safe.
                if error.code == "internal_extraction_error" and error.__cause__ is not None:
                    score["contractDiagnostic"] = str(error.__cause__)
            results.append(score)
            print(json.dumps({k: score[k] for k in ("id", "mode", "passed")}), flush=True)
    report = {"dataset": dataset["version"], "datasetSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
              "renderProfile": args.render_profile,
              "arabicModel": args.arabic_model,
              "layoutProfile": args.layout_profile,
              "arabicModelSha256": hashlib.sha256(Path("/usr/share/tesseract-ocr/5/tessdata/ara.traineddata").read_bytes()).hexdigest(),
              "fontSha256": hashlib.sha256(font_path.read_bytes()).hexdigest(), "fixtures": fixtures,
              "scope": "synthetic raster images; no real-invoice accuracy claim", "cases": results}
    (args.output_dir / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
