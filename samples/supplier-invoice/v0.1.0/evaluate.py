"""Bounded actual-extractor check; native PDF and text-helper proof stay separate."""
from __future__ import annotations

import argparse
from dataclasses import asdict
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[3]
FIXTURES = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "apps/api"))
from app.local_extraction import LocalInvoiceExtractor, _ExtractedPage, _TextLine


def load_truth():
    truth_path = FIXTURES / "source-truth.json"
    truth = json.loads(truth_path.read_text(encoding="utf-8"))
    manifest = json.loads((FIXTURES / "manifest.json").read_text(encoding="utf-8"))
    assert sha256(truth_path.read_bytes()).hexdigest() == manifest["truthSha256"], "Frozen truth changed"
    for source in manifest["files"]:
        assert sha256((FIXTURES / source["name"]).read_bytes()).hexdigest() == source["sha256"], source["name"]
    return truth


def text_page(text: str, page_number: int = 1):
    """Synthetic test geometry; not evidence of actual PDF/OCR reading."""
    lines = []
    for row, line in enumerate(text.rstrip("\n").split("\n")):
        y = .04 + row * .018
        boxes = [(.05 + index * .003, y, .052 + index * .003, y + .01) for index in range(len(line))]
        lines.append(_TextLine(line, boxes, [.95] * len(line)))
    canonical = "\n".join(line.text for line in lines) + "\n"
    return _ExtractedPage(page_number, canonical, lines, 0, None, False, False, "native")


def expected_rows(truth):
    return {(key, row["occurrence"]): row[value] for row in truth["rows"] for key, value in
            [("line_description", "description"), ("line_quantity", "quantity"), ("line_unit_price", "unitPrice")]}


def check_fields(pages, fields, expected, *, real_geometry=False):
    observed = {(field.key, field.occurrence): field for field in fields}
    assert len(observed) == len(fields), "Duplicate field identities"
    for identity, value in expected.items():
        assert identity in observed, f"Missing {identity}"
        assert observed[identity].normalized_value == value, f"Wrong {identity}: {observed[identity].normalized_value!r} != {value!r}"
    for field in fields:
        assert field.evidence, field.key
        for evidence in field.evidence:
            page = next(page for page in pages if page.page_number == evidence.page_number)
            assert evidence.quote == field.value
            assert page.canonical_text.encode("utf-8")[evidence.start_utf8:evidence.end_utf8].decode("utf-8") == field.value
            if real_geometry:
                assert evidence.coordinate_unit == "normalized" and evidence.polygon
                assert all(0 <= coordinate <= 1 for point in evidence.polygon for coordinate in point)
    assert not any(field.key == "line_total" for field in fields), "Net amounts were promoted to gross line totals"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    truth = load_truth()
    output = args.output or ROOT / "outputs/supplier-invoice-S1-20261006" / ("manual-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%f"))
    output.mkdir(parents=True, exist_ok=False)
    receipts = []
    for doc in truth["documents"]:
        page = text_page((FIXTURES / (doc["id"] + ".txt")).read_text(encoding="utf-8"))
        role = LocalInvoiceExtractor._supplier_document_type([page])
        fields, warnings = LocalInvoiceExtractor._supplier_fields([page])
        assert role == doc["role"], (doc["id"], role)
        expected = {(key, 0): value for key, value in doc["expected"].items()} | expected_rows(truth)
        check_fields([page], fields, expected)
        receipts.append({"source": doc["id"] + ".txt", "boundary": "text helper; synthetic geometry", "role": role, "fields": [asdict(field) for field in fields], "warnings": [asdict(warning) for warning in warnings]})
    for case in truth["numericCases"]:
        assert LocalInvoiceExtractor._supplier_normalize(case["raw"], "Decimal") == case["normalized"], case["raw"]
    print("Text helper checks: four roles/headers/labelled rows and 28 ASCII-number acceptance/refusal cases passed.")
    extractor = LocalInvoiceExtractor(input_profile=truth["inputProfile"], language="eng")
    for doc in truth["documents"]:
        if "native-pdf" not in doc["representation"]:
            continue
        result = extractor.extract(FIXTURES / (doc["id"] + ".pdf"), "application/pdf")
        assert result.document_type == doc["role"]
        assert result.provider.adapter_version == "local-1.6.0"
        assert result.provider.model == "pdfplumber", "This native check must not invoke OCR"
        assert result.outcome == "Partial", "Missing unit/basis coverage cannot be Ready"
        assert {field.occurrence for field in result.fields if field.occurrence > 0} == {1, 2}, "Unexpected table row count"
        assert any(warning.code == "line_coverage_incomplete" for warning in result.warnings)
        assert not any(warning.code == "line_table_requires_review" for warning in result.warnings), "Fixture footer is outside the complete two-row table"
        expected = {(key, 0): value for key, value in doc["expected"].items()} | expected_rows(truth)
        check_fields(result.pages, result.fields, expected, real_geometry=True)
        payload = asdict(result)
        (output / (doc["id"] + "-extraction.json")).write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        receipts.append({"source": doc["id"] + ".pdf", "boundary": "public native PDF extraction; no OCR", "role": result.document_type, "adapter": result.provider.adapter_version, "outcome": result.outcome, "expectedRows": 2, "verifiedLineFields": 6, "allEvidenceSlicesMatch": True})
        print(f'{doc["id"]}.pdf: {result.document_type}; total 1260.00 AED; rows qty 2/1, price 400.00/400.00; six row fields and exact page quotes PASS; Partial (unit/basis coverage remains).')
    (output / "receipt.json").write_text(json.dumps({"dataset": truth["dataset"], "receipts": receipts, "numericCases": truth["numericCases"], "limits": truth["limits"]}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Saved raw values, normalized values, exact quotes and polygons:", output)
    print("No OCR, backend review/pair comparison or synthetic UI preview acceptance is implied.")


if __name__ == "__main__":
    main()
