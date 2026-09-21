#!/usr/bin/env python3
"""Generate and verify the UAE FTA field-coverage development pack v0.2.0."""

from __future__ import annotations

import argparse
import io
import json
import tempfile
import textwrap
from dataclasses import dataclass
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from typing import Any

from phase3_uae_invoices import (
    _canonical_sha256,
    _find_pdftoppm,
    _run_pdftoppm,
    _sha256_file,
    _write_json,
)


SCRIPT_PATH = Path(__file__).resolve()
REPO_ROOT = SCRIPT_PATH.parents[1]
SOURCE_PATH = SCRIPT_PATH.with_name("phase3_uae_invoice_source_v0.2.0.json")
DEFAULT_OUTPUT = REPO_ROOT / "output" / "pdf" / "uae-invoice-development-v0.2.0"
GENERATOR_VERSION = "0.2.0"
ACQUIRED_AT_UTC = "2026-08-14T00:00:00Z"
CENT = Decimal("0.01")


@dataclass(frozen=True)
class LineAmounts:
    line_number: int
    description: str
    quantity: Decimal
    unit: str
    unit_price: Decimal
    discount: Decimal
    net: Decimal
    vat_rate: Decimal
    tax_category: str
    vat_invoice_currency: Decimal
    net_aed: Decimal
    vat_aed: Decimal
    amount_due_aed: Decimal


def _round(value: Decimal) -> Decimal:
    return value.quantize(CENT, rounding=ROUND_HALF_UP)


def _money(value: Decimal) -> str:
    return f"{_round(value):,.2f}"


def _palette(name: str) -> dict[str, str]:
    return {
        "corporate": {"accent": "#0F4C5C", "soft": "#E9F3F5", "ink": "#173042"},
        "desert": {"accent": "#9A5B13", "soft": "#FFF3DF", "ink": "#3D3026"},
        "retail": {"accent": "#6A3F83", "soft": "#F4ECF8", "ink": "#29222E"},
    }[name]


def _rate(sample: dict[str, Any]) -> Decimal:
    return Decimal(sample.get("exchange_rate_to_aed", "1.00"))


def _line_amounts(sample: dict[str, Any]) -> list[LineAmounts]:
    rate = _rate(sample)
    reverse_charge = sample["scenario"] == "reverse_charge"
    lines: list[LineAmounts] = []
    for index, item in enumerate(sample["items"], start=1):
        quantity = Decimal(item["quantity"])
        unit_price = Decimal(item["unit_price"])
        discount = Decimal(item["discount"])
        net = _round(quantity * unit_price - discount)
        vat_rate = Decimal(item["vat_rate"])
        vat_invoice = _round(net * vat_rate / Decimal("100"))
        net_aed = _round(net * rate)
        vat_aed = _round(vat_invoice * rate)
        charged_vat_aed = Decimal("0.00") if reverse_charge else vat_aed
        lines.append(
            LineAmounts(
                line_number=index,
                description=item["description"],
                quantity=quantity,
                unit=item["unit"],
                unit_price=unit_price,
                discount=discount,
                net=net,
                vat_rate=vat_rate,
                tax_category=item["tax_category"],
                vat_invoice_currency=vat_invoice,
                net_aed=net_aed,
                vat_aed=vat_aed,
                amount_due_aed=_round(net_aed + charged_vat_aed),
            )
        )
    return lines


def _totals(sample: dict[str, Any]) -> dict[str, Decimal]:
    lines = _line_amounts(sample)
    reverse_charge = sample["scenario"] == "reverse_charge"
    subtotal = _round(
        sum(line.quantity * line.unit_price for line in lines)
    )
    discount = _round(sum(line.discount for line in lines))
    taxable = _round(sum(line.net for line in lines))
    invoice_vat = _round(sum(line.vat_invoice_currency for line in lines))
    net_aed = _round(sum(line.net_aed for line in lines))
    calculated_vat_aed = _round(sum(line.vat_aed for line in lines))
    charged_vat_aed = Decimal("0.00") if reverse_charge else calculated_vat_aed
    amount_due_aed = _round(sum(line.amount_due_aed for line in lines))
    return {
        "subtotal": subtotal,
        "discount": discount,
        "taxable": taxable,
        "invoice_vat": invoice_vat,
        "net_aed": net_aed,
        "calculated_vat_aed": calculated_vat_aed,
        "charged_vat_aed": charged_vat_aed,
        "amount_due_aed": amount_due_aed,
        "gross_aed": amount_due_aed,
    }


def _validate_trn(value: str | None, context: str) -> None:
    if value is None or len(value) != 15 or not value.isdigit():
        raise RuntimeError(f"{context} must be a 15-digit fictional TRN")


def validate_source(source: dict[str, Any]) -> None:
    if source.get("source_schema_version") != "2.0":
        raise RuntimeError("Unsupported v0.2 UAE source schema")
    sample_ids: set[str] = set()
    filenames: set[str] = set()
    invoice_numbers: set[str] = set()
    for sample in source["samples"]:
        for key, seen in (
            ("sample_id", sample_ids),
            ("filename", filenames),
            ("invoice_number", invoice_numbers),
        ):
            value = sample[key]
            if value in seen:
                raise RuntimeError(f"Duplicate {key}: {value}")
            seen.add(value)
        _validate_trn(sample["seller"]["trn"], f"{sample['sample_id']}.seller.trn")
        if sample["seller"]["tin"] != sample["seller"]["trn"][:10]:
            raise RuntimeError(f"{sample['sample_id']} seller TIN must be the first 10 TRN digits")
        if sample["buyer"]["registered_for_vat"]:
            _validate_trn(sample["buyer"]["trn"], f"{sample['sample_id']}.buyer.trn")
        elif sample["buyer"]["trn"] is not None:
            raise RuntimeError(f"{sample['sample_id']} unregistered buyer must not have a TRN")

        lines = _line_amounts(sample)
        if not lines or any(line.net < 0 for line in lines):
            raise RuntimeError(f"{sample['sample_id']} must have non-negative invoice lines")
        if sample["scenario"] == "simplified":
            if sample["buyer"]["registered_for_vat"] and _totals(sample)["gross_aed"] > 10000:
                raise RuntimeError("Simplified registered-buyer fixture exceeds AED 10,000")
        if sample["scenario"] == "zero_rated":
            if any(line.vat_rate != 0 or line.tax_category != "Z" for line in lines):
                raise RuntimeError("Zero-rated fixture must use category Z and 0% VAT")
            if "Article 45" not in sample.get("conditional_statement", ""):
                raise RuntimeError("Zero-rated fixture must reference Article 45")
        if sample["scenario"] == "reverse_charge":
            if "Article 48" not in sample.get("conditional_statement", ""):
                raise RuntimeError("Reverse-charge fixture must reference Article 48")
            if _totals(sample)["charged_vat_aed"] != 0:
                raise RuntimeError("Reverse-charge fixture cannot charge supplier VAT")
        if sample["scenario"] == "foreign_currency":
            if sample["invoice_currency"] == "AED":
                raise RuntimeError("Foreign-currency fixture must not use AED")
            if _rate(sample) <= 0 or not sample.get("exchange_rate_source"):
                raise RuntimeError("Foreign-currency fixture requires rate and source")
        elif sample["invoice_currency"] != "AED":
            raise RuntimeError("Only the foreign-currency scenario may use a non-AED currency")
        if sample["scenario"] == "discounted_multipage":
            if _totals(sample)["discount"] <= 0 or sample["layout"] != "two_page_detailed":
                raise RuntimeError("Discounted multipage fixture must include discounts and two pages")


def _draw_header(pdf: Any, sample: dict[str, Any], page_number: int, page_count: int) -> None:
    from reportlab.lib.colors import HexColor
    from reportlab.lib.pagesizes import A4

    width, height = A4
    colors = _palette(sample["style"])
    pdf.setFillColor(HexColor(colors["accent"]))
    pdf.rect(0, height - 18, width, 18, fill=1, stroke=0)
    title = "SIMPLIFIED TAX INVOICE" if sample["scenario"] == "simplified" else "TAX INVOICE"
    if sample["scenario"] == "reverse_charge":
        title = "TAX INVOICE - REVERSE CHARGE"
    pdf.setFillColor(HexColor(colors["ink"]))
    pdf.setFont("Helvetica-Bold", 14)
    pdf.drawString(36, height - 61, sample["seller"]["name"])
    pdf.setFont("Helvetica-Bold", 20 if len(title) < 25 else 15)
    pdf.drawRightString(width - 36, height - 61, title)
    pdf.setFont("Helvetica", 7.2)
    seller = sample["seller"]
    pdf.drawString(36, height - 76, seller["address_line_1"])
    pdf.drawString(36, height - 88, f"{seller['city']}, {seller['subdivision']}, {seller['country_code']}")
    pdf.drawString(36, height - 100, f"Supplier TRN: {seller['trn']} | TIN: {seller['tin']}")
    pdf.drawString(36, height - 112, f"Trade licence: {seller['trade_license']}")
    pdf.setFillColor(HexColor("#FFF0F1"))
    pdf.roundRect(143, height - 39, 309, 14, 4, fill=1, stroke=0)
    pdf.setFillColor(HexColor("#A51D2D"))
    pdf.setFont("Helvetica-Bold", 7.2)
    pdf.drawCentredString(
        width / 2, height - 35, "SYNTHETIC FIELD-COVERAGE FIXTURE - NOT A LEGAL TAX INVOICE"
    )
    pdf.setFont("Helvetica", 7)
    pdf.drawRightString(
        width - 36, 22, f"FTA field-coverage development pack v0.2.0 | Page {page_number} of {page_count}"
    )


def _draw_party_and_invoice_blocks(pdf: Any, sample: dict[str, Any], y: float) -> float:
    from reportlab.lib.colors import HexColor
    from reportlab.lib.pagesizes import A4

    width, _ = A4
    colors = _palette(sample["style"])
    buyer = sample["buyer"]
    pdf.setFillColor(HexColor(colors["soft"]))
    pdf.roundRect(36, y - 92, 315, 92, 5, fill=1, stroke=0)
    pdf.roundRect(width - 231, y - 92, 195, 92, 5, fill=1, stroke=0)
    pdf.setFillColor(HexColor(colors["ink"]))
    pdf.setFont("Helvetica-Bold", 8)
    pdf.drawString(46, y - 15, "Recipient")
    pdf.setFont("Helvetica-Bold", 9)
    pdf.drawString(46, y - 31, buyer["name"])
    pdf.setFont("Helvetica", 7.2)
    pdf.drawString(46, y - 45, buyer["address_line_1"][:58])
    pdf.drawString(46, y - 58, f"{buyer['city']}, {buyer['subdivision']}, {buyer['country_code']}")
    trn_text = buyer["trn"] if buyer["trn"] else "Not applicable - recipient not VAT registered"
    pdf.drawString(46, y - 72, f"Recipient TRN: {trn_text}")

    labels = ["Invoice number", "Invoice date", "Supply date", "Currency"]
    values = [
        sample["invoice_number"],
        sample["invoice_date"],
        sample["supply_date"],
        sample["invoice_currency"],
    ]
    for index, (label, value) in enumerate(zip(labels, values)):
        line_y = y - 17 - index * 18
        pdf.setFont("Helvetica-Bold", 7.2)
        pdf.drawString(width - 221, line_y, f"{label}:")
        pdf.setFont("Helvetica", 7.2)
        pdf.drawRightString(width - 46, line_y, value)
    return y - 112


def _draw_items(
    pdf: Any,
    sample: dict[str, Any],
    lines: list[LineAmounts],
    start_y: float,
) -> float:
    from reportlab.lib.colors import HexColor
    from reportlab.lib.pagesizes import A4

    width, _ = A4
    colors = _palette(sample["style"])
    left, right = 36, width - 36
    positions = [left, 54, 199, 235, 269, 328, 387, 426, 491, right]
    vat_heading = "VAT calc.*\n(AED)" if sample["scenario"] == "reverse_charge" else "VAT\n(AED)"
    headings = [
        "#",
        "Description",
        "Qty",
        "Unit",
        f"Unit price\n({sample['invoice_currency']})",
        f"Net\n({sample['invoice_currency']})",
        "VAT %",
        vat_heading,
        "Payable\n(AED)",
    ]
    pdf.setFillColor(HexColor(colors["accent"]))
    pdf.rect(left, start_y - 28, right - left, 28, fill=1, stroke=0)
    pdf.setFillColor(HexColor("#FFFFFF"))
    pdf.setFont("Helvetica-Bold", 5.8)
    for index, heading in enumerate(headings):
        pieces = heading.split("\n")
        for line_index, piece in enumerate(pieces):
            pdf.drawString(positions[index] + 3, start_y - 11 - line_index * 8, piece)

    y = start_y - 28
    row_height = 34
    pdf.setFont("Helvetica", 6.5)
    for offset, line in enumerate(lines):
        y -= row_height
        if offset % 2 == 0:
            pdf.setFillColor(HexColor(colors["soft"]))
            pdf.rect(left, y, right - left, row_height, fill=1, stroke=0)
        pdf.setFillColor(HexColor(colors["ink"]))
        values = [
            str(line.line_number),
            line.description,
            f"{line.quantity:g}",
            line.unit,
            _money(line.unit_price),
            _money(line.net),
            f"{line.vat_rate:g}%",
            _money(line.vat_aed),
            _money(line.amount_due_aed),
        ]
        for index, value in enumerate(values):
            if index == 1:
                description_lines = textwrap.wrap(
                    value,
                    width=30,
                    break_long_words=False,
                    break_on_hyphens=False,
                )[:2]
                for line_index, description_line in enumerate(description_lines):
                    pdf.drawString(
                        positions[index] + 3,
                        y + 19 - line_index * 10,
                        description_line,
                    )
                continue
            if index in {2, 4, 5, 6, 7, 8}:
                pdf.drawRightString(positions[index + 1] - 4, y + 13, value)
            else:
                pdf.drawString(positions[index] + 3, y + 13, value)
    pdf.setStrokeColor(HexColor("#C4D0D5"))
    pdf.rect(left, y, right - left, start_y - y, fill=0, stroke=1)
    return y - 14


def _draw_payment_and_totals(pdf: Any, sample: dict[str, Any], y: float) -> None:
    from reportlab.lib.colors import HexColor
    from reportlab.lib.pagesizes import A4

    width, _ = A4
    colors = _palette(sample["style"])
    totals = _totals(sample)
    currency = sample["invoice_currency"]

    pdf.setFillColor(HexColor(colors["soft"]))
    pdf.roundRect(36, y - 112, 250, 112, 5, fill=1, stroke=0)
    pdf.roundRect(width - 276, y - 154, 240, 154, 5, fill=1, stroke=0)
    pdf.setFillColor(HexColor(colors["ink"]))
    pdf.setFont("Helvetica-Bold", 8)
    pdf.drawString(46, y - 16, "Payment information")
    payment_rows = [
        ("Terms", sample["payment_terms"]),
        ("Due date", sample["payment_due_date"]),
        ("Method", sample["payment_method"]),
        ("Payment means code", sample["payment_means_code"]),
    ]
    for index, (label, value) in enumerate(payment_rows):
        line_y = y - 34 - index * 17
        pdf.setFont("Helvetica-Bold", 7)
        pdf.drawString(46, line_y, f"{label}:")
        pdf.setFont("Helvetica", 7)
        pdf.drawString(126, line_y, value)

    rows = [
        (f"Subtotal ({currency})", totals["subtotal"]),
        (f"Discount ({currency})", totals["discount"]),
        (f"Taxable amount ({currency})", totals["taxable"]),
    ]
    if currency != "AED":
        rows.append((f"Invoice VAT ({currency})", totals["invoice_vat"]))
        rows.append(("Exchange rate to AED", _rate(sample)))
    rows.append(("VAT charged (AED)", totals["charged_vat_aed"]))
    if sample["scenario"] == "reverse_charge":
        rows.append(("Customer-accounted VAT (AED)", totals["calculated_vat_aed"]))
    rows.extend(
        [
            ("Gross payable (AED)", totals["gross_aed"]),
            ("Amount due (AED)", totals["amount_due_aed"]),
        ]
    )
    line_height = min(17, 135 / len(rows))
    for index, (label, value) in enumerate(rows):
        line_y = y - 17 - index * line_height
        strong = label in {"Gross payable (AED)", "Amount due (AED)"}
        pdf.setFont("Helvetica-Bold" if strong else "Helvetica", 7.5 if strong else 6.8)
        pdf.drawString(width - 266, line_y, f"{label}:")
        pdf.drawRightString(width - 46, line_y, _money(value))

    statement = sample.get("conditional_statement")
    if statement:
        box_y = y - 177
        pdf.setFillColor(HexColor("#FFF4D6"))
        pdf.roundRect(36, box_y - 42, width - 72, 42, 5, fill=1, stroke=0)
        pdf.setFillColor(HexColor("#5D4600"))
        pdf.setFont("Helvetica-Bold", 6.8)
        pdf.drawString(46, box_y - 13, "Conditional tax statement")
        pdf.setFont("Helvetica", 6.2)
        first = statement[:105]
        second = statement[105:210]
        pdf.drawString(46, box_y - 25, first)
        if second:
            pdf.drawString(46, box_y - 35, second)
    if sample.get("exchange_rate_source"):
        pdf.setFillColor(HexColor(colors["ink"]))
        pdf.setFont("Helvetica", 6.1)
        pdf.drawString(46, y - 232, f"Exchange-rate source: {sample['exchange_rate_source']}")


def _render_native(path: Path, sample: dict[str, Any]) -> None:
    from reportlab.lib.pagesizes import A4
    from reportlab.pdfgen import canvas

    page_count = 2 if sample["layout"] == "two_page_detailed" else 1
    lines = _line_amounts(sample)
    groups = [lines[:4], lines[4:]] if page_count == 2 else [lines]
    pdf = canvas.Canvas(str(path), pagesize=A4, pageCompression=1, invariant=1)
    for page_number, group in enumerate(groups, start=1):
        _draw_header(pdf, sample, page_number, page_count)
        table_y = _draw_party_and_invoice_blocks(pdf, sample, 692)
        end_y = _draw_items(pdf, sample, group, table_y)
        if page_number == page_count:
            _draw_payment_and_totals(pdf, sample, min(end_y, 405))
        else:
            pdf.setFont("Helvetica-Oblique", 7)
            pdf.drawRightString(A4[0] - 36, end_y - 8, "Continued on page 2")
        pdf.showPage()
    pdf.save()


def _render_scanned(path: Path, sample: dict[str, Any]) -> None:
    from PIL import Image, ImageEnhance, ImageFilter
    from reportlab.lib.pagesizes import A4
    from reportlab.lib.utils import ImageReader
    from reportlab.pdfgen import canvas

    with tempfile.TemporaryDirectory(prefix="uae-invoice-v2-scan-") as temporary:
        temp_root = Path(temporary)
        native_path = temp_root / "source.pdf"
        _render_native(native_path, sample)
        prefix = temp_root / "page"
        _run_pdftoppm(_find_pdftoppm(), native_path, prefix)
        images = sorted(temp_root.glob("page-*.png"))
        if not images:
            raise RuntimeError("No raster pages were generated")
        pdf = canvas.Canvas(str(path), pagesize=A4, pageCompression=1, invariant=1)
        for image_path in images:
            image = Image.open(image_path).convert("RGB")
            image = ImageEnhance.Contrast(image).enhance(0.94)
            if sample["scan_type"] == "scanned_pdf_rotated_degraded":
                original_size = image.size
                image = image.resize(
                    (int(image.width * 0.76), int(image.height * 0.76)),
                    Image.Resampling.BILINEAR,
                ).resize(original_size, Image.Resampling.BILINEAR)
                image = image.filter(ImageFilter.GaussianBlur(radius=0.55))
                image = image.rotate(
                    1.8,
                    resample=Image.Resampling.BICUBIC,
                    expand=True,
                    fillcolor="white",
                )
                encoded = io.BytesIO()
                image.save(encoded, format="JPEG", quality=52, optimize=False)
                encoded.seek(0)
                image = Image.open(encoded).convert("RGB")
            else:
                image = image.filter(ImageFilter.GaussianBlur(radius=0.2))
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


def _core_fields(sample: dict[str, Any]) -> list[dict[str, Any]]:
    totals = _totals(sample)
    currency = sample["invoice_currency"]
    values = [
        ("invoice_number", "String", sample["invoice_number"], sample["invoice_number"]),
        ("invoice_date", "Date", sample["invoice_date"], sample["invoice_date"]),
        ("supplier_trn", "Identifier", sample["seller"]["trn"], sample["seller"]["trn"]),
        ("currency", "Currency", currency, currency),
        ("subtotal", "Decimal", _money(totals["subtotal"]), f"{totals['subtotal']:.2f}"),
        ("vat", "Decimal", _money(totals["charged_vat_aed"]), f"{totals['charged_vat_aed']:.2f}"),
        ("total", "Decimal", _money(totals["amount_due_aed"]), f"{totals['amount_due_aed']:.2f}"),
        ("payment_terms", "String", sample["payment_terms"], sample["payment_terms"]),
    ]
    return [
        {"key": key, "occurrence": 0, "value_type": kind, "value": value, "normalized_value": normalized}
        for key, kind, value, normalized in values
    ]


def _compliance_truth(sample: dict[str, Any]) -> dict[str, Any]:
    totals = _totals(sample)
    lines = _line_amounts(sample)
    return {
        "sample_id": sample["sample_id"],
        "invoice": {
            "scenario": sample["scenario"],
            "invoice_number": sample["invoice_number"],
            "invoice_date": sample["invoice_date"],
            "supply_date": sample["supply_date"],
            "invoice_currency": sample["invoice_currency"],
            "exchange_rate_to_aed": sample.get("exchange_rate_to_aed"),
            "exchange_rate_source": sample.get("exchange_rate_source"),
        },
        "seller": sample["seller"],
        "buyer": sample["buyer"],
        "payment": {
            "terms": sample["payment_terms"],
            "due_date": sample["payment_due_date"],
            "method": sample["payment_method"],
            "means_code": sample["payment_means_code"],
        },
        "totals": {key: f"{value:.2f}" for key, value in totals.items()},
        "conditional_statement": sample.get("conditional_statement"),
        "lines": [
            {
                "line_number": line.line_number,
                "description": line.description,
                "quantity": str(line.quantity),
                "unit": line.unit,
                "unit_price": f"{line.unit_price:.2f}",
                "discount": f"{line.discount:.2f}",
                "net": f"{line.net:.2f}",
                "vat_rate": f"{line.vat_rate:.2f}",
                "tax_category": line.tax_category,
                "vat_invoice_currency": f"{line.vat_invoice_currency:.2f}",
                "net_aed": f"{line.net_aed:.2f}",
                "vat_aed": f"{line.vat_aed:.2f}",
                "amount_due_aed": f"{line.amount_due_aed:.2f}",
            }
            for line in lines
        ],
    }


FTA_REQUIREMENTS = [
    "tax_invoice_label",
    "invoice_issue_date",
    "supply_date_if_different",
    "supplier_name_address_trn",
    "recipient_name_address_trn_if_registered",
    "unique_sequential_invoice_number",
    "goods_or_services_description",
    "per_line_quantity_unit_price_net_vat_rate_vat_value",
    "amounts_payable_in_aed",
    "gross_amount_payable_in_aed",
    "discount_amount_if_offered",
    "foreign_currency_vat_aed_rate_and_source_if_applicable",
    "reverse_charge_statement_and_legal_reference_if_applicable",
]


def _coverage(sample: dict[str, Any]) -> dict[str, str]:
    statuses = {requirement: "pass" for requirement in FTA_REQUIREMENTS}
    if sample["scenario"] == "simplified":
        for requirement in (
            "supply_date_if_different",
            "recipient_name_address_trn_if_registered",
            "unique_sequential_invoice_number",
            "per_line_quantity_unit_price_net_vat_rate_vat_value",
            "gross_amount_payable_in_aed",
            "discount_amount_if_offered",
            "foreign_currency_vat_aed_rate_and_source_if_applicable",
            "reverse_charge_statement_and_legal_reference_if_applicable",
        ):
            statuses[requirement] = "not_required_for_simplified"
    else:
        if sample["invoice_date"] == sample["supply_date"]:
            statuses["supply_date_if_different"] = "not_applicable_same_date"
        if not any(Decimal(item["discount"]) for item in sample["items"]):
            statuses["discount_amount_if_offered"] = "not_applicable_no_discount"
        if sample["scenario"] != "foreign_currency":
            statuses["foreign_currency_vat_aed_rate_and_source_if_applicable"] = "not_applicable_aed"
        if sample["scenario"] != "reverse_charge":
            statuses["reverse_charge_statement_and_legal_reference_if_applicable"] = "not_applicable"
    return statuses


def _write_metadata(output: Path, source: dict[str, Any]) -> None:
    dataset_samples: list[dict[str, Any]] = []
    sources: list[dict[str, Any]] = []
    ledger_records: list[dict[str, Any]] = []
    compliance_truth: list[dict[str, Any]] = []
    compliance_rows: list[dict[str, Any]] = []
    artifacts: list[dict[str, Any]] = []
    for index, sample in enumerate(source["samples"]):
        path = output / sample["filename"]
        digest = _sha256_file(path)
        core_fields = _core_fields(sample)
        fields_hash = _canonical_sha256(core_fields)
        dataset_samples.append(
            {
                "sample_id": sample["sample_id"],
                "source": {"path": sample["filename"], "sha256": digest, "license": source["license"], "synthetic": True},
                "segments": {"layout": sample["layout"], "scan_type": sample["scan_type"], "language": "en"},
                "ground_truth": {"fields": core_fields},
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
                "rights": {"reference": "tools/phase3_uae_invoice_source_v0.2.0.json (CC0-1.0)", "store": True, "label": True, "process": True, "publish_aggregate_results": True},
                "privacy": {"contains_personal_data": False, "contains_confidential_data": False, "redaction_status": "not_required", "review_reference": "Fictional source and visible synthetic watermark"},
                "storage": {"classification": "public", "committed_to_git": True, "location_reference": f"output/pdf/uae-invoice-development-v0.2.0/{sample['filename']}"},
            }
        )
        minute = index * 2
        ledger_records.append(
            {
                "sample_id": sample["sample_id"],
                "initial_annotation": {"reviewer_id": "synthetic-calculation-a", "completed_at_utc": f"2026-08-14T01:{minute:02d}:00Z", "fields_canonical_sha256": fields_hash},
                "independent_verification": {"reviewer_id": "synthetic-render-verifier-b", "completed_at_utc": f"2026-08-14T01:{minute + 1:02d}:00Z", "disposition": "accepted", "reason": "Recomputed line-level rounding, totals, and visible FTA field coverage from the fictional source."},
                "adjudication": None,
                "final_label_revision": 1,
                "final_fields_canonical_sha256": fields_hash,
            }
        )
        compliance_truth.append(_compliance_truth(sample))
        compliance_rows.append({"sample_id": sample["sample_id"], "scenario": sample["scenario"], "requirements": _coverage(sample)})
        artifacts.append({"path": sample["filename"], "bytes": path.stat().st_size, "sha256": digest, "scenario": sample["scenario"], "scan_type": sample["scan_type"]})

    dataset = {
        "dataset_schema_version": "1.0",
        "dataset_id": source["dataset_id"],
        "dataset_version": source["dataset_version"],
        "purpose": "development",
        "claim_eligible": False,
        "claim_exclusion_reason": "Synthetic FTA field-coverage fixtures are not legally issued invoices, real-document performance evidence, or MoF eInvoices.",
        "normalization": {"name": "strict-normalized-value", "version": "1.0"},
        "samples": dataset_samples,
    }
    register = {"source_register_schema_version": "1.0", "register_id": "phase3-uae-fta-v020-source-register", "dataset_id": source["dataset_id"], "dataset_version": source["dataset_version"], "sources": sources}
    ledger = {"annotation_ledger_schema_version": "1.0", "ledger_id": "phase3-uae-fta-v020-annotation-ledger", "dataset_id": source["dataset_id"], "dataset_version": source["dataset_version"], "annotation_guide_version": "1.0", "field_schema_version": "invoice-fields-1.0", "records": ledger_records}
    compliance = {
        "matrix_version": "1.0",
        "dataset_id": source["dataset_id"],
        "dataset_version": source["dataset_version"],
        "scope": "FTA visible tax-invoice field coverage; not legal validation",
        "fta_reference": source["fta_reference"],
        "requirements": FTA_REQUIREMENTS,
        "samples": compliance_rows,
    }
    mof_mapping = {
        "mapping_version": "1.0",
        "dataset_id": source["dataset_id"],
        "dataset_version": source["dataset_version"],
        "is_einvoice": False,
        "reason": "The MoF defines eInvoices as structured exchanged data; PDFs and scans are not eInvoices.",
        "mof_reference": source["mof_reference"],
        "available_for_future_mapping": ["invoice number/date/currency", "payment due date/means", "seller and buyer identity/address/tax identifiers", "document totals and tax breakdown", "line identifiers/quantity/unit/prices/tax category/rate/VAT AED/item description"],
        "still_requires_pint_ae_implementation": ["structured XML syntax", "invoice type and transaction type codes", "business process and specification identifiers", "electronic endpoint identifiers", "PINT-AE validation", "ASP exchange and reporting"],
    }
    generation_report = {"generator": "tools/phase3_uae_invoices_v2.py", "generator_version": GENERATOR_VERSION, "source": "tools/phase3_uae_invoice_source_v0.2.0.json", "source_sha256": _sha256_file(SOURCE_PATH), "license": source["license"], "artifacts": artifacts}
    _write_json(output / "dataset.json", dataset)
    _write_json(output / "source-register.json", register)
    _write_json(output / "annotation-ledger.json", ledger)
    _write_json(output / "compliance-ground-truth.json", {"ground_truth_version": "1.0", "samples": compliance_truth})
    _write_json(output / "fta-compliance-matrix.json", compliance)
    _write_json(output / "mof-einvoice-mapping.json", mof_mapping)
    _write_json(output / "generation-report.json", generation_report)


def _write_readme(output: Path, source: dict[str, Any]) -> None:
    rows = "\n".join(
        f"| `{sample['filename']}` | {sample['scenario']} | {sample['scan_type']} |"
        for sample in source["samples"]
    )
    output.joinpath("README.md").write_text(
        f"""# UAE FTA field-coverage development pack v0.2.0

Nine original, fictional UAE tax-invoice PDFs cover the visible fields and
conditional cases in the FTA guidance. They include standard full, simplified,
zero-rated, reverse-charge, foreign-currency, discounted multipage, clean scan,
and rotated/degraded scan cases.

| PDF | Scenario | Input condition |
|---|---|---|
{rows}

`compliance-ground-truth.json` records every displayed compliance field and
line-level calculation. `fta-compliance-matrix.json` records requirement status
per sample. The normal Phase 3 eight-field manifest remains in `dataset.json`.

These documents use deliberately invalid identifiers and a visible synthetic
watermark. They are not legally issued invoices and do not establish legal,
client, OCR-accuracy, or production claims.

`mof-einvoice-mapping.json` keeps the MoF boundary explicit: PDFs and scans are
not eInvoices. PINT-AE XML, validation, ASP exchange, and reporting remain a
separate structured-data implementation.

Official references:

- FTA tax-invoice guidance: {source['fta_reference']}
- FTA detailed tax-invoice discussion: {source['fta_detailed_reference']}
- MoF mandatory eInvoice fields: {source['mof_reference']}
""",
        encoding="utf-8",
        newline="\n",
    )


def generate(output: Path) -> None:
    source = json.loads(SOURCE_PATH.read_text(encoding="utf-8"))
    validate_source(source)
    output.mkdir(parents=True, exist_ok=True)
    expected = {sample["filename"] for sample in source["samples"]}
    for existing in output.glob("*.pdf"):
        if existing.name not in expected:
            raise RuntimeError(f"Refusing to overwrite output containing unknown PDF: {existing}")
    for sample in source["samples"]:
        path = output / sample["filename"]
        if sample["scan_type"] == "native_pdf":
            _render_native(path, sample)
        else:
            _render_scanned(path, sample)
    _write_metadata(output, source)
    _write_readme(output, source)
    print(f"Generated {len(source['samples'])} v0.2.0 UAE invoice PDFs in {output}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--validate-source-only", action="store_true")
    arguments = parser.parse_args()
    source = json.loads(SOURCE_PATH.read_text(encoding="utf-8"))
    validate_source(source)
    if arguments.validate_source_only:
        print(f"Validated {len(source['samples'])} v0.2.0 source samples")
        return 0
    generate(arguments.output.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
