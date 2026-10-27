"""Evaluate frozen language labels, separately from OCR and model accuracy."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "apps/api"))
from app.arabic_text import POLICY_VERSION, script_language  # noqa: E402
from app.local_extraction import LocalInvoiceExtractor, _ExtractedPage, _TextLine  # noqa: E402


def text_page(text):
    lines = [_TextLine(line, [None] * len(line), [None] * len(line))
             for line in text.splitlines()]
    return _ExtractedPage(1, text, lines, 0, None, False)


def evaluate(case, pages, *, fields=None, warnings=None):
    fields = LocalInvoiceExtractor._fields(pages) if fields is None else fields
    actual = {field.key: field.normalized_value for field in fields if field.occurrence == 0}
    checks = {key: actual.get(key) == expected for key, expected in case["expected"].items()}
    for key in case.get("absent", []):
        checks["absent:" + key] = key not in actual
    warnings = {w.code for w in (LocalInvoiceExtractor._warnings(pages, fields) if warnings is None else warnings)}
    for code in case.get("warnings", []):
        checks["warning:" + code] = code in warnings
    checks["language"] = script_language("\n".join(p.canonical_text for p in pages)) == case["language"]
    evidence = []
    for field in fields:
        for span in field.evidence:
            page = next(p for p in pages if p.page_number == span.page_number)
            evidence.append(page.canonical_text.encode()[span.start_utf8:span.end_utf8].decode() == span.quote)
    checks["exact_evidence"] = bool(evidence) and all(evidence)
    return {"id": case["id"], "language": case["language"], "passed": all(checks.values()),
            "checks": checks, "actual": actual, "evidenceSpans": len(evidence)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset", type=Path,
                        default=ROOT / "samples/phase-7/language-v0.2.0/cases.json")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    path = args.dataset
    data = json.loads(path.read_text(encoding="utf-8"))
    cases = [evaluate(case, [text_page(case["text"])]) for case in data["cases"]]
    report = {"dataset": data["version"], "datasetSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
              "policy": POLICY_VERSION, "scope": "canonical-text parsing; no OCR or model calls",
              "groups": {language: {"passed": sum(c["passed"] for c in cases if c["language"] == language),
                                    "total": sum(c["language"] == language for c in cases)}
                         for language in ("ar", "ar-en", "en")}, "cases": cases}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    # Preserve previous receipts. A new evaluation needs a new output path.
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(json.dumps(report["groups"]))
    return 0 if all(c["passed"] for c in cases) else 1


if __name__ == "__main__":
    raise SystemExit(main())
