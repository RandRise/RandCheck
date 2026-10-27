#!/usr/bin/env python3
"""Freeze actual local PDF extraction and score against pre-authored source truth."""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from dataclasses import asdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "apps/api"))
from app.extraction import ExtractionError  # noqa: E402
from app.local_extraction import LocalInvoiceExtractor  # noqa: E402


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    truth = json.loads((args.source / "source-truth.json").read_text(encoding="utf-8"))
    extractor = LocalInvoiceExtractor(language="eng+ara", arabic_model="best", layout_profile="lines-v1")
    receipts = []
    for source in truth["files"]:
        path = args.source / source["name"]
        assert hashlib.sha256(path.read_bytes()).hexdigest() == source["sha256"]
        receipt = {"source": source, "expected": truth["expected"]}
        try:
            result = extractor.extract(path, "application/pdf")
            payload = asdict(result)
            encoded = (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
            filename = path.stem + "-extraction.json"
            (args.output / filename).write_bytes(encoded)
            observed = {f.key: f.normalized_value or f.value for f in result.fields}
            receipt.update({
                "extractionFile": filename,
                "extractionSha256": hashlib.sha256(encoded).hexdigest(),
                "outcome": result.outcome,
                "observed": observed,
                "checks": {key: observed.get(key) == value for key, value in truth["expected"].items()},
            })
            for field in result.fields:
                for span in field.evidence:
                    page = next(p for p in result.pages if p.page_number == span.page_number)
                    assert page.canonical_text.encode("utf-8")[span.start_utf8:span.end_utf8].decode("utf-8") == span.quote
            receipt["allEvidenceSlicesMatch"] = True
        except ExtractionError as error:
            receipt.update({"error": error.code, "summary": error.summary})
        receipts.append(receipt)
        print(json.dumps(receipt, ensure_ascii=True), flush=True)
    (args.output / "manifest.json").write_text(json.dumps({
        "dataset": truth["dataset_version"], "kind": truth["kind"],
        "profile": "eng+ara/best/lines-v1", "pageTimeoutSeconds": 30,
        "receipts": receipts,
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if any("error" in receipt for receipt in receipts):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
