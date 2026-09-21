#!/usr/bin/env python3
"""Generate the versioned Phase 4 synthetic UAE procurement chain."""

from __future__ import annotations

import argparse
import hashlib
import json
import tempfile
import textwrap
from pathlib import Path
from typing import Any


SCRIPT_PATH = Path(__file__).resolve()
REPO_ROOT = SCRIPT_PATH.parents[1]
SOURCE_PATH = SCRIPT_PATH.with_name("phase4_procurement_source_v0.1.0.json")
DEFAULT_OUTPUT = REPO_ROOT / "output" / "pdf" / "uae-procurement-reconciliation-v0.1.0"
GENERATOR_VERSION = "0.1.0"


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _write_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def _field_lines(document: dict[str, Any], source: dict[str, Any]) -> list[str]:
    number_label = {
        "Quotation": "Quotation Number",
        "PurchaseOrder": "Purchase Order Number",
        "Invoice": "Invoice Number",
    }[document["role"]]
    lines = [
        f"{number_label}: {document['document_number']}",
        f"Supplier TRN: {source['supplier']['trn']}",
        f"Currency: {source['currency']}",
        f"Subtotal: {document['subtotal']}",
        f"VAT: {document['vat']}",
        f"Total: {document['total']}",
    ]
    for item in document["lines"]:
        occurrence = item["occurrence"]
        lines.extend([
            f"Line {occurrence} Item Code: {item['item_code']}",
            f"Line {occurrence} Description: {item['description']}",
            f"Line {occurrence} Quantity: {item['quantity']}",
            f"Line {occurrence} Unit Price: {item['unit_price']}",
            f"Line {occurrence} Tax: {item['tax']}",
            f"Line {occurrence} Total: {item['total']}",
        ])
    return lines


def _draw_page(path: Path, document: dict[str, Any], source: dict[str, Any], *, supplement: bool = False) -> None:
    from reportlab.lib.colors import HexColor
    from reportlab.lib.pagesizes import A4
    from reportlab.pdfgen import canvas

    width, height = A4
    pdf = canvas.Canvas(str(path), pagesize=A4, pageCompression=1, invariant=1)
    accent = "#0D5B55" if document["role"] == "Quotation" else "#263B63" if document["role"] == "PurchaseOrder" else "#8A4E18"
    pdf.setFillColor(HexColor(accent))
    pdf.rect(0, height - 20, width, 20, fill=1, stroke=0)
    pdf.setFillColor(HexColor("#FFF0F1"))
    pdf.roundRect(92, height - 44, width - 184, 16, 4, fill=1, stroke=0)
    pdf.setFillColor(HexColor("#A51D2D"))
    pdf.setFont("Helvetica-Bold", 7.5)
    pdf.drawCentredString(width / 2, height - 39, "SYNTHETIC DEVELOPMENT DOCUMENT - NOT FOR COMMERCIAL OR TAX USE")
    title = "RECONCILIATION DATA SUPPLEMENT" if supplement else ("PURCHASE ORDER" if document["role"] == "PurchaseOrder" else "QUOTATION")
    pdf.setFillColor(HexColor("#1C2F32"))
    pdf.setFont("Helvetica-Bold", 19)
    pdf.drawString(38, height - 78, title)
    pdf.setFont("Helvetica", 8)
    subtitle = "Appendix to the FTA field-coverage invoice on page 1" if supplement else "Fictional UAE procurement document"
    pdf.drawString(38, height - 92, subtitle)

    if not supplement:
        pdf.setFillColor(HexColor("#EDF4F2"))
        pdf.roundRect(38, height - 202, width - 76, 88, 5, fill=1, stroke=0)
        pdf.setFillColor(HexColor("#1C2F32"))
        pdf.setFont("Helvetica-Bold", 8)
        pdf.drawString(50, height - 132, "SUPPLIER")
        pdf.drawString(310, height - 132, "BUYER")
        pdf.setFont("Helvetica", 7.5)
        for x, party in ((50, source["supplier"]), (310, source["buyer"])):
            pdf.drawString(x, height - 147, party["name"])
            address_lines = textwrap.wrap(party["address"], width=42, break_long_words=False)[:2]
            for index, address_line in enumerate(address_lines):
                pdf.drawString(x, height - 160 - index * 11, address_line)
            pdf.drawString(x, height - 187, f"TRN: {party['trn']}")

    y = height - (220 if not supplement else 124)
    pdf.setFillColor(HexColor(accent))
    pdf.setFont("Helvetica-Bold", 8)
    pdf.drawString(38, y, "REVIEWABLE RECONCILIATION FIELDS")
    y -= 16
    pdf.setFont("Courier", 7.2)
    pdf.setFillColor(HexColor("#1C2F32"))
    for line in _field_lines(document, source):
        pdf.drawString(42, y, line)
        y -= 15
    pdf.setStrokeColor(HexColor("#B9C7C4"))
    pdf.rect(34, y - 8, width - 68, (height - (220 if not supplement else 124)) - y + 16, fill=0, stroke=1)
    pdf.setFillColor(HexColor("#667673"))
    pdf.setFont("Helvetica", 6.5)
    pdf.drawString(38, 24, f"Phase 4 synthetic truth set v0.1.0 | {document['role']} | Page {'2' if supplement else '1'}")
    pdf.save()


def _render_invoice(path: Path, document: dict[str, Any], source: dict[str, Any]) -> None:
    from pypdf import PdfReader, PdfWriter

    original = REPO_ROOT / document["source_invoice"]
    if not original.is_file():
        raise RuntimeError(f"Required Phase 3 invoice is missing: {original}")
    with tempfile.TemporaryDirectory(prefix="phase4-invoice-") as temporary:
        supplement = Path(temporary) / "supplement.pdf"
        _draw_page(supplement, document, source, supplement=True)
        writer = PdfWriter()
        for page in PdfReader(str(original)).pages:
            writer.add_page(page)
        for page in PdfReader(str(supplement)).pages:
            writer.add_page(page)
        writer.add_metadata({
            "/Title": "Synthetic UAE tax invoice with reconciliation supplement",
            "/Subject": "Phase 4 development fixture; not a legal tax invoice",
        })
        with path.open("wb") as stream:
            writer.write(stream)


def validate(source: dict[str, Any]) -> None:
    roles = [document["role"] for document in source["documents"]]
    if roles != ["Quotation", "PurchaseOrder", "Invoice"]:
        raise RuntimeError("The source must contain one ordered quotation, PO, and invoice")
    if len({document["filename"] for document in source["documents"]}) != 3:
        raise RuntimeError("Document filenames must be unique")
    for document in source["documents"]:
        if not document["lines"] or any(item["occurrence"] <= 0 for item in document["lines"]):
            raise RuntimeError(f"{document['role']} must contain positive line occurrences")
        if len({item["occurrence"] for item in document["lines"]}) != len(document["lines"]):
            raise RuntimeError(f"{document['role']} line occurrences must be unique")


def generate(output: Path) -> None:
    source = json.loads(SOURCE_PATH.read_text(encoding="utf-8"))
    validate(source)
    output.mkdir(parents=True, exist_ok=True)
    for document in source["documents"]:
        path = output / document["filename"]
        if document["role"] == "Invoice":
            _render_invoice(path, document, source)
        else:
            _draw_page(path, document, source)
    truth = {
        "schema_version": "1.0",
        "dataset_id": source["dataset_id"],
        "dataset_version": source["dataset_version"],
        "rule_set_version": "1.0",
        "synthetic": True,
        "claim_eligible": False,
        "tolerances": {"quantity": "0", "unit_price": "0.01", "tax": "0.01", "line_total": "0.01", "document_total": "0.01"},
        "documents": [{
            "role": document["role"], "filename": document["filename"],
            "sha256": _sha256(output / document["filename"]),
            "expected_fields": _field_lines(document, source),
        } for document in source["documents"]],
        "expected_findings": source["expected_findings"],
    }
    _write_json(output / "truth-set.json", truth)
    _write_json(output / "generation-report.json", {
        "generator": "tools/phase4_procurement_documents.py",
        "generator_version": GENERATOR_VERSION,
        "source": SOURCE_PATH.relative_to(REPO_ROOT).as_posix(),
        "source_sha256": _sha256(SOURCE_PATH),
        "license": source["license"],
        "artifacts": [{"filename": document["filename"], "sha256": _sha256(output / document["filename"]), "bytes": (output / document["filename"]).stat().st_size} for document in source["documents"]],
    })
    output.joinpath("README.md").write_text(
        "# UAE procurement reconciliation synthetic truth set v0.1.0\n\n"
        "This pack contains one fictional quotation, one fictional purchase order, and the existing Phase 3 FTA field-coverage invoice with an appended reconciliation-data page. The visible differences are intentional and listed in `truth-set.json`.\n\n"
        "The pack proves development integration only. It is not a real-world accuracy set, legal advice, an issued tax invoice, or a UAE MoF eInvoice.\n",
        encoding="utf-8", newline="\n",
    )
    print(f"Generated 3 Phase 4 PDFs and truth metadata in {output}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--validate-source-only", action="store_true")
    arguments = parser.parse_args()
    source = json.loads(SOURCE_PATH.read_text(encoding="utf-8"))
    validate(source)
    if arguments.validate_source_only:
        print("Validated Phase 4 procurement source v0.1.0")
        return 0
    generate(arguments.output.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
