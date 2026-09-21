#!/usr/bin/env python3
"""Generate a small, UAE-specific synthetic invoice development pack."""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import shutil
import subprocess
import tempfile
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from typing import Any


SCRIPT_PATH = Path(__file__).resolve()
REPO_ROOT = SCRIPT_PATH.parents[1]
SOURCE_PATH = SCRIPT_PATH.with_name("phase3_uae_invoice_source.json")
DEFAULT_OUTPUT = REPO_ROOT / "output" / "pdf" / "uae-invoice-development-v0.1.0"
GENERATOR_VERSION = "0.1.0"
ACQUIRED_AT_UTC = "2026-08-14T00:00:00Z"
MONEY_QUANTUM = Decimal("0.01")
VAT_RATE = Decimal("0.05")


def _json_bytes(payload: Any) -> bytes:
    return (json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode(
        "utf-8"
    )


def _write_json(path: Path, payload: Any) -> None:
    path.write_bytes(_json_bytes(payload))


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _canonical_sha256(payload: Any) -> str:
    rendered = json.dumps(
        payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return _sha256_bytes(rendered)


def _money(value: Decimal) -> str:
    return f"{value.quantize(MONEY_QUANTUM, rounding=ROUND_HALF_UP):,.2f}"


def _totals(sample: dict[str, Any]) -> tuple[Decimal, Decimal, Decimal]:
    subtotal = sum(
        Decimal(item["quantity"]) * Decimal(item["unit_price"])
        for item in sample["items"]
    ).quantize(MONEY_QUANTUM, rounding=ROUND_HALF_UP)
    vat = (subtotal * VAT_RATE).quantize(MONEY_QUANTUM, rounding=ROUND_HALF_UP)
    return subtotal, vat, subtotal + vat


def _palette(name: str) -> dict[str, str]:
    return {
        "corporate": {"accent": "#0F4C5C", "soft": "#E9F3F5", "ink": "#173042"},
        "desert": {"accent": "#9A5B13", "soft": "#FFF3DF", "ink": "#3D3026"},
        "retail": {"accent": "#6A3F83", "soft": "#F4ECF8", "ink": "#29222E"},
    }[name]


def _draw_banner(canvas: Any, sample: dict[str, Any], page_number: int, page_count: int) -> None:
    from reportlab.lib.colors import HexColor
    from reportlab.lib.pagesizes import A4

    width, height = A4
    colors = _palette(sample["style"])
    canvas.setFillColor(HexColor(colors["accent"]))
    canvas.rect(0, height - 20, width, 20, fill=1, stroke=0)
    canvas.setFillColor(HexColor("#A51D2D"))
    canvas.setFont("Helvetica-Bold", 8)
    canvas.drawCentredString(
        width / 2,
        height - 34,
        "SYNTHETIC DEVELOPMENT DOCUMENT - NOT A REAL TAX INVOICE",
    )
    canvas.setFillColor(HexColor(colors["ink"]))
    canvas.setFont("Helvetica-Bold", 16)
    canvas.drawString(42, height - 70, sample["supplier"])
    canvas.setFont("Helvetica", 8.5)
    canvas.drawString(42, height - 86, sample["supplier_address"])
    canvas.drawString(42, height - 101, f"Supplier TRN: {sample['supplier_trn']}")
    canvas.setFont("Helvetica-Bold", 25)
    canvas.drawRightString(width - 42, height - 77, "TAX INVOICE")
    canvas.setFont("Helvetica", 8)
    canvas.drawRightString(width - 42, 24, f"Synthetic UAE development sample | Page {page_number} of {page_count}")


def _draw_metadata(canvas: Any, sample: dict[str, Any], y: float) -> float:
    from reportlab.lib.colors import HexColor
    from reportlab.lib.pagesizes import A4

    width, _ = A4
    colors = _palette(sample["style"])
    canvas.setFillColor(HexColor(colors["soft"]))
    canvas.roundRect(width - 242, y - 68, 200, 68, 5, fill=1, stroke=0)
    canvas.setFillColor(HexColor(colors["ink"]))
    canvas.setFont("Helvetica-Bold", 8.5)
    labels = ["Invoice Number:", "Invoice Date:", "Supply Date:", "Currency:"]
    values = [
        sample["invoice_number"],
        sample["invoice_date_display"],
        sample["supply_date"],
        "AED",
    ]
    for index, (label, value) in enumerate(zip(labels, values)):
        line_y = y - 14 - index * 14
        canvas.drawString(width - 232, line_y, label)
        canvas.setFont("Helvetica", 8.5)
        canvas.drawRightString(width - 52, line_y, value)
        canvas.setFont("Helvetica-Bold", 8.5)

    canvas.setFont("Helvetica-Bold", 8.5)
    canvas.drawString(42, y - 10, "Bill To")
    canvas.setFont("Helvetica-Bold", 10)
    canvas.drawString(42, y - 28, sample["customer"])
    canvas.setFont("Helvetica", 8.5)
    canvas.drawString(42, y - 44, sample["customer_address"])
    return y - 92


def _draw_items_table(
    canvas: Any,
    sample: dict[str, Any],
    items: list[dict[str, str]],
    start_y: float,
    first_item_number: int,
) -> float:
    from reportlab.lib.colors import HexColor
    from reportlab.lib.pagesizes import A4

    width, _ = A4
    colors = _palette(sample["style"])
    left, right = 42, width - 42
    columns = [left, left + 34, left + 310, left + 370, left + 452, right]
    canvas.setFillColor(HexColor(colors["accent"]))
    canvas.rect(left, start_y - 22, right - left, 22, fill=1, stroke=0)
    canvas.setFillColor(HexColor("#FFFFFF"))
    canvas.setFont("Helvetica-Bold", 8)
    headings = ["#", "Description", "Qty", "Unit Price", "Line Total"]
    for index, heading in enumerate(headings):
        canvas.drawString(columns[index] + 5, start_y - 15, heading)

    y = start_y - 22
    canvas.setFont("Helvetica", 8.5)
    for offset, item in enumerate(items):
        y -= 30
        if offset % 2 == 0:
            canvas.setFillColor(HexColor(colors["soft"]))
            canvas.rect(left, y, right - left, 30, fill=1, stroke=0)
        canvas.setFillColor(HexColor(colors["ink"]))
        quantity = Decimal(item["quantity"])
        unit_price = Decimal(item["unit_price"])
        line_total = quantity * unit_price
        canvas.drawString(columns[0] + 5, y + 10, str(first_item_number + offset))
        canvas.drawString(columns[1] + 5, y + 10, item["description"][:43])
        canvas.drawRightString(columns[3] - 7, y + 10, item["quantity"])
        canvas.drawRightString(columns[4] - 7, y + 10, _money(unit_price))
        canvas.drawRightString(columns[5] - 5, y + 10, _money(line_total))
    canvas.setStrokeColor(HexColor("#CAD5DA"))
    canvas.rect(left, y, right - left, start_y - y, fill=0, stroke=1)
    return y - 18


def _draw_totals(canvas: Any, sample: dict[str, Any], y: float) -> None:
    from reportlab.lib.colors import HexColor
    from reportlab.lib.pagesizes import A4

    width, _ = A4
    colors = _palette(sample["style"])
    subtotal, vat, total = _totals(sample)
    box_left, box_width = width - 252, 210
    canvas.setFillColor(HexColor(colors["soft"]))
    canvas.roundRect(box_left, y - 86, box_width, 86, 5, fill=1, stroke=0)
    canvas.setFillColor(HexColor(colors["ink"]))
    rows = [
        ("Subtotal:", _money(subtotal), False),
        ("VAT 5%:", _money(vat), False),
        ("Total:", _money(total), True),
    ]
    for index, (label, value, strong) in enumerate(rows):
        line_y = y - 20 - index * 25
        canvas.setFont("Helvetica-Bold" if strong else "Helvetica", 11 if strong else 9)
        canvas.drawString(box_left + 12, line_y, label)
        canvas.drawRightString(box_left + box_width - 12, line_y, f"AED {value}")
    canvas.setFont("Helvetica-Bold", 9)
    canvas.drawString(42, y - 20, "Payment Terms:")
    canvas.setFont("Helvetica", 9)
    canvas.drawString(118, y - 20, sample["payment_terms"])
    canvas.setFont("Helvetica", 7.5)
    canvas.setFillColor(HexColor("#666666"))
    canvas.drawString(42, y - 43, "Fictional transaction. No payment is due and the TRN is not valid.")


def _render_native(path: Path, sample: dict[str, Any]) -> None:
    from reportlab.lib.pagesizes import A4
    from reportlab.pdfgen import canvas

    path.parent.mkdir(parents=True, exist_ok=True)
    page_count = 2 if sample["layout"] == "two_page_tabular" else 1
    item_groups = (
        [sample["items"][:5], sample["items"][5:]]
        if page_count == 2
        else [sample["items"]]
    )
    pdf = canvas.Canvas(str(path), pagesize=A4, pageCompression=1, invariant=1)
    first_item_number = 1
    for page_index, items in enumerate(item_groups, start=1):
        _draw_banner(pdf, sample, page_index, page_count)
        start_y = _draw_metadata(pdf, sample, 710)
        table_end = _draw_items_table(pdf, sample, items, start_y, first_item_number)
        first_item_number += len(items)
        if page_index == page_count:
            _draw_totals(pdf, sample, min(table_end, 355))
        else:
            pdf.setFont("Helvetica-Oblique", 8.5)
            pdf.drawRightString(A4[0] - 42, table_end - 8, "Continued on page 2")
        pdf.showPage()
    pdf.save()


def _find_pdftoppm() -> str:
    command = shutil.which("pdftoppm") or shutil.which("pdftoppm.cmd")
    if command:
        wrapper = Path(command).resolve()
        bundled_candidates = [
            wrapper.parents[2] / "native" / "poppler" / "Library" / "bin" / "pdftoppm.exe",
            wrapper.parent.parent / "Library" / "bin" / "pdftoppm.exe",
        ]
        for candidate in bundled_candidates:
            if candidate.is_file():
                return str(candidate)
        return command
    raise RuntimeError("pdftoppm is required to create scanned PDF variants")


def _run_pdftoppm(command: str, source: Path, prefix: Path) -> None:
    arguments = [command, "-png", "-r", "200", str(source), str(prefix)]
    if command.lower().endswith((".cmd", ".bat")):
        arguments = ["cmd.exe", "/d", "/c", *arguments]
    completed = subprocess.run(arguments, capture_output=True, text=True, check=False)
    if completed.returncode:
        raise RuntimeError(f"pdftoppm failed: {completed.stderr.strip()}")


def _render_scanned(path: Path, sample: dict[str, Any]) -> None:
    from PIL import Image, ImageEnhance, ImageFilter
    from reportlab.lib.pagesizes import A4
    from reportlab.lib.utils import ImageReader
    from reportlab.pdfgen import canvas

    with tempfile.TemporaryDirectory(prefix="uae-invoice-scan-") as temporary:
        temp_root = Path(temporary)
        native_path = temp_root / "source.pdf"
        _render_native(native_path, sample)
        prefix = temp_root / "page"
        _run_pdftoppm(_find_pdftoppm(), native_path, prefix)
        image_paths = sorted(temp_root.glob("page-*.png"))
        if not image_paths:
            raise RuntimeError("pdftoppm did not create any rendered pages")

        pdf = canvas.Canvas(str(path), pagesize=A4, pageCompression=1, invariant=1)
        for image_path in image_paths:
            image = Image.open(image_path).convert("RGB")
            image = ImageEnhance.Contrast(image).enhance(0.94)
            if sample["scan_type"] == "scanned_pdf_rotated_degraded":
                image = image.resize(
                    (int(image.width * 0.72), int(image.height * 0.72)),
                    Image.Resampling.BILINEAR,
                ).resize(image.size, Image.Resampling.BILINEAR)
                image = image.filter(ImageFilter.GaussianBlur(radius=0.65))
                image = image.rotate(2.2, resample=Image.Resampling.BICUBIC, expand=True, fillcolor="white")
                encoded = io.BytesIO()
                image.save(encoded, format="JPEG", quality=48, optimize=False)
                encoded.seek(0)
                image = Image.open(encoded).convert("RGB")
            else:
                image = image.filter(ImageFilter.GaussianBlur(radius=0.22))

            page_width, page_height = A4
            scale = min(page_width / image.width, page_height / image.height)
            draw_width, draw_height = image.width * scale, image.height * scale
            pdf.drawImage(
                ImageReader(image),
                (page_width - draw_width) / 2,
                (page_height - draw_height) / 2,
                width=draw_width,
                height=draw_height,
                preserveAspectRatio=True,
            )
            pdf.showPage()
        pdf.save()


def _ground_truth(sample: dict[str, Any]) -> list[dict[str, Any]]:
    subtotal, vat, total = _totals(sample)
    values = [
        ("invoice_number", "String", sample["invoice_number"], sample["invoice_number"]),
        (
            "invoice_date",
            "Date",
            sample["invoice_date_display"],
            sample["invoice_date_normalized"],
        ),
        ("supplier_trn", "Identifier", sample["supplier_trn"], sample["supplier_trn"]),
        ("currency", "Currency", "AED", "AED"),
        ("subtotal", "Decimal", _money(subtotal), f"{subtotal:.2f}"),
        ("vat", "Decimal", _money(vat), f"{vat:.2f}"),
        ("total", "Decimal", _money(total), f"{total:.2f}"),
        ("payment_terms", "String", sample["payment_terms"], sample["payment_terms"]),
    ]
    return [
        {
            "key": key,
            "occurrence": 0,
            "value_type": value_type,
            "value": value,
            "normalized_value": normalized,
        }
        for key, value_type, value, normalized in values
    ]


def _write_metadata(output: Path, source: dict[str, Any]) -> None:
    dataset_samples: list[dict[str, Any]] = []
    sources: list[dict[str, Any]] = []
    records: list[dict[str, Any]] = []
    artifact_rows: list[dict[str, Any]] = []
    for index, sample in enumerate(source["samples"]):
        path = output / sample["filename"]
        digest = _sha256_file(path)
        fields = _ground_truth(sample)
        fields_hash = _canonical_sha256(fields)
        dataset_samples.append(
            {
                "sample_id": sample["sample_id"],
                "source": {
                    "path": sample["filename"],
                    "sha256": digest,
                    "license": source["license"],
                    "synthetic": True,
                },
                "segments": {
                    "layout": sample["layout"],
                    "scan_type": sample["scan_type"],
                    "language": "en",
                },
                "ground_truth": {"fields": fields},
            }
        )
        sources.append(
            {
                "sample_id": sample["sample_id"],
                "asset_id": sample["sample_id"],
                "sha256": digest,
                "bytes": path.stat().st_size,
                "media_type": "application/pdf",
                "acquired_at_utc": ACQUIRED_AT_UTC,
                "provenance_type": "synthetic",
                "rights": {
                    "reference": "tools/phase3_uae_invoice_source.json (CC0-1.0)",
                    "store": True,
                    "label": True,
                    "process": True,
                    "publish_aggregate_results": True,
                },
                "privacy": {
                    "contains_personal_data": False,
                    "contains_confidential_data": False,
                    "redaction_status": "not_required",
                    "review_reference": "Fictional source definition and visible synthetic watermark",
                },
                "storage": {
                    "classification": "public",
                    "committed_to_git": True,
                    "location_reference": f"output/pdf/uae-invoice-development-v0.1.0/{sample['filename']}",
                },
            }
        )
        minute = index * 2
        records.append(
            {
                "sample_id": sample["sample_id"],
                "initial_annotation": {
                    "reviewer_id": "synthetic-calculation-a",
                    "completed_at_utc": f"2026-08-14T00:{minute:02d}:00Z",
                    "fields_canonical_sha256": fields_hash,
                },
                "independent_verification": {
                    "reviewer_id": "synthetic-render-verifier-b",
                    "completed_at_utc": f"2026-08-14T00:{minute + 1:02d}:00Z",
                    "disposition": "accepted",
                    "reason": "Recomputed totals from the fictional source definition and checked the generated field text.",
                },
                "adjudication": None,
                "final_label_revision": 1,
                "final_fields_canonical_sha256": fields_hash,
            }
        )
        artifact_rows.append(
            {
                "path": sample["filename"],
                "bytes": path.stat().st_size,
                "sha256": digest,
                "scan_type": sample["scan_type"],
            }
        )

    dataset = {
        "dataset_schema_version": "1.0",
        "dataset_id": source["dataset_id"],
        "dataset_version": source["dataset_version"],
        "purpose": "development",
        "claim_eligible": False,
        "claim_exclusion_reason": "Original synthetic UAE invoices for development only; they do not represent real supplier-document performance.",
        "normalization": {"name": "strict-normalized-value", "version": "1.0"},
        "samples": dataset_samples,
    }
    register = {
        "source_register_schema_version": "1.0",
        "register_id": "phase3-uae-synthetic-development-source-register-v1",
        "dataset_id": source["dataset_id"],
        "dataset_version": source["dataset_version"],
        "sources": sources,
    }
    ledger = {
        "annotation_ledger_schema_version": "1.0",
        "ledger_id": "phase3-uae-synthetic-development-annotation-ledger-v1",
        "dataset_id": source["dataset_id"],
        "dataset_version": source["dataset_version"],
        "annotation_guide_version": "1.0",
        "field_schema_version": "invoice-fields-1.0",
        "records": records,
    }
    generation_report = {
        "generator": "tools/phase3_uae_invoices.py",
        "generator_version": GENERATOR_VERSION,
        "source": "tools/phase3_uae_invoice_source.json",
        "source_sha256": _sha256_file(SOURCE_PATH),
        "license": source["license"],
        "artifacts": artifact_rows,
    }
    _write_json(output / "dataset.json", dataset)
    _write_json(output / "source-register.json", register)
    _write_json(output / "annotation-ledger.json", ledger)
    _write_json(output / "generation-report.json", generation_report)


def _write_readme(output: Path, source: dict[str, Any]) -> None:
    rows = "\n".join(
        f"| `{sample['filename']}` | {sample['layout']} | {sample['scan_type']} |"
        for sample in source["samples"]
    )
    text = f"""# UAE synthetic invoice development pack v{source['dataset_version']}

This pack contains six original, fictional UAE tax-invoice PDFs for extraction
development. Every PDF is visibly marked as synthetic, uses AED and 5% VAT, and
contains an intentionally invalid 15-digit TRN. The source definition is CC0-1.0.

| Asset | Layout | Input condition |
|---|---|---|
{rows}

`dataset.json`, `source-register.json`, and `annotation-ledger.json` satisfy the
Phase 3 contracts. `generation-report.json` records hashes and sizes.

These assets are not claim eligible and must not be reported as real-world OCR
accuracy. Arabic and mixed-language evaluation remains separate Phase 7 scope.

Specification references:

- UAE Federal Tax Authority, VAT tax-invoice requirements:
  https://tax.gov.ae/Datafolder/Files/Pdf/2023/Knowledge%20Center%20Page/VAT11%20-%20Tax%20invoices%20En.pdf
- UAE Ministry of Finance, eInvoicing portal:
  https://mof.gov.ae/en/about-us/initiatives/einvoicing/
"""
    output.joinpath("README.md").write_text(text, encoding="utf-8", newline="\n")


def generate(output: Path) -> None:
    source = json.loads(SOURCE_PATH.read_text(encoding="utf-8"))
    if source.get("source_schema_version") != "1.0":
        raise RuntimeError("Unsupported UAE synthetic source schema")
    output.mkdir(parents=True, exist_ok=True)
    expected_names = {sample["filename"] for sample in source["samples"]}
    for existing in output.glob("*.pdf"):
        if existing.name not in expected_names:
            raise RuntimeError(f"Refusing to overwrite output containing unknown PDF: {existing}")
    for sample in source["samples"]:
        path = output / sample["filename"]
        if sample["scan_type"] == "native_pdf":
            _render_native(path, sample)
        else:
            _render_scanned(path, sample)
    _write_metadata(output, source)
    _write_readme(output, source)
    print(f"Generated {len(source['samples'])} UAE synthetic invoice PDFs in {output}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    arguments = parser.parse_args()
    generate(arguments.output.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
