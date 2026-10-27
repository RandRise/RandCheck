"""Offline line-crop diagnostics; no field correction or golden-label lookup."""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageOps
import pytesseract

from phase7_ocr_evaluate import LocalInvoiceExtractor


def rows(image):
    ink = ImageOps.invert(image.convert("L")).point(lambda p: 255 if p > 100 else 0)
    active = [y for y in range(ink.height) if ink.crop((0, y, ink.width, y+1)).getbbox()]
    groups = []
    for y in active:
        if not groups or y - groups[-1][-1] > 12:
            groups.append([])
        groups[-1].append(y)
    return [(0, max(0, g[0]-15), image.width, min(image.height, g[-1]+16)) for g in groups]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--images", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cases", nargs="+", default=("mixed-identifiers", "mixed-neighbor-fields"))
    args = parser.parse_args()
    LocalInvoiceExtractor(language="eng+ara", arabic_model="best")._verify_ocr_engine(pytesseract)
    result = []
    for name in args.cases:
        image = Image.open(args.images / (name + ".png"))
        for index, box in enumerate(rows(image)):
            crop = image.crop(box)
            for language in ("ara", "eng+ara", "ara+eng"):
                data = pytesseract.image_to_data(crop, lang=language, config="--oem 1 --psm 7",
                                                output_type=pytesseract.Output.DICT, timeout=30)
                words = [{key: data[key][n] for key in ("text", "conf", "left", "top", "width", "height")}
                         for n, word in enumerate(data["text"]) if word.strip()]
                record = {"id": name, "row": index, "box": box, "mode": language, "words": words}
                result.append(record)
                print(json.dumps(record, ensure_ascii=False), flush=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
