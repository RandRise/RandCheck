#!/usr/bin/env python3
"""Create two fictional, two-page mixed-language invoices for workflow checks.

Requires reportlab, Pillow, arabic-reshaper, python-bidi and Poppler. These are
development documents, not independent observations of production accuracy.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--render-dir", type=Path, required=True)
    parser.add_argument("--pdftoppm", type=Path, required=True)
    parser.add_argument("--font", type=Path, required=True)
    args = parser.parse_args()

    import arabic_reshaper
    from bidi import get_display
    from reportlab.lib import colors
    from reportlab.lib.pagesizes import A4
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    from reportlab.pdfgen import canvas

    args.output.mkdir(parents=True, exist_ok=True)
    args.render_dir.mkdir(parents=True, exist_ok=True)
    native = args.output / "mixed-native-two-page.pdf"
    scanned = args.output / "mixed-scanned-two-page.pdf"
    if native.exists() or scanned.exists():
        raise SystemExit("Refusing to overwrite an existing PDF pack")
    pdfmetrics.registerFont(TTFont("Arabic", str(args.font)))
    width, height = A4
    c = canvas.Canvas(str(native), pagesize=A4, invariant=1)
    c.setTitle("Fictional mixed-language invoice AR-2026-101")
    c.setAuthor("UAE Document Intelligence development fixtures")

    def text(x: float, y: float, value: str, size: int = 11) -> None:
        c.setFont("Helvetica", size)
        c.setFillColor(colors.HexColor("#183542"))
        c.drawString(x, y, value)

    def arabic(x: float, y: float, value: str, size: int = 16) -> None:
        c.setFont("Arabic", size)
        c.setFillColor(colors.HexColor("#183542"))
        c.drawRightString(x, y, get_display(arabic_reshaper.reshape(value)))

    def frame(page: int) -> None:
        c.setFillColor(colors.HexColor("#e9f2f3"))
        c.rect(0, height - 37, width, 37, fill=1, stroke=0)
        text(35, height - 23, "SYNTHETIC DEVELOPMENT DOCUMENT - NOT A REAL TAX INVOICE", 9)
        c.setStrokeColor(colors.HexColor("#c6d6db"))
        c.line(35, 64, width - 35, 64)
        text(35, 45, "Fictional parties and tax identifier. For local development only.", 9)
        text(width - 105, 45, f"Page {page} of 2", 9)

    frame(1)
    text(40, 765, "DESERT DEMO SERVICES", 23)
    text(40, 741, "Training invoice / two-page development sample", 12)
    text(40, 710, "Supplier TRN: 000000000000701")
    text(40, 687, "Invoice number: AR-2026-101")
    text(40, 664, "Invoice date: 2026-09-22")
    text(40, 641, "Due date: 2026-10-22")
    text(40, 618, "Currency: AED")
    text(40, 573, "BILL TO", 10)
    text(40, 550, "Oasis Demo Customer LLC (fictional)", 14)
    text(40, 528, "Training District, Dubai, United Arab Emirates")
    c.setFillColor(colors.HexColor("#e9f2f3"))
    c.rect(35, 464, width - 70, 29, fill=1, stroke=0)
    for x, label in [(42, "Description"), (320, "Qty"), (378, "Unit price"), (485, "Net AED")]:
        text(x, 475, label, 10)
    for y, description, qty, price, amount in [
        (435, "Document review training", "2", "400.00", "800.00"),
        (395, "Workflow configuration session", "1", "400.00", "400.00"),
    ]:
        for x, value in [(42, description), (325, qty), (390, price), (486, amount)]:
            text(x, y, value, 10)
        c.line(35, y - 16, width - 35, y - 16)
    text(40, 335, "Amounts are in AED. VAT rate: 5%.")
    text(40, 312, "Invoice totals and payment information continue on page 2.")
    text(40, 267, "No banking details are included in this fictional sample.", 10)
    c.showPage()

    frame(2)
    text(40, 765, "AR-2026-101 / continued", 14)
    arabic(width - 40, 715, "ملخص الفاتورة", 24)
    arabic(width - 40, 680, "شركة الصحراء التجريبية", 16)
    c.setStrokeColor(colors.HexColor("#c6d6db"))
    c.line(40, 652, width - 40, 652)
    for y, label, value in [
        (605, "المجموع الفرعي:", "1200.00"),
        (555, "ضريبة القيمة المضافة:", "60.00"),
        (505, "الإجمالي:", "1260.00"),
        (455, "العملة:", "AED"),
    ]:
        arabic(width - 45, y, label)
        text(90, y, value, 16)
    c.line(40, 427, width - 40, 427)
    arabic(width - 45, 380, "شروط الدفع:")
    arabic(290, 380, "خلال 30 يوماً")
    arabic(width - 45, 330, "يرجى مراجعة البيانات قبل اعتماد الفاتورة.", 14)
    text(40, 260, "Review note: dates and amounts use Western digits.", 10)
    text(40, 237, "This document is a workflow fixture, not a production accuracy benchmark.", 10)
    c.save()

    subprocess.run([
        str(args.pdftoppm), "-r", "300", "-png", str(native),
        str(args.render_dir / "native"),
    ], check=True)
    c = canvas.Canvas(str(scanned), pagesize=A4, invariant=1)
    c.setTitle("Image-only copy of fictional mixed-language invoice AR-2026-101")
    for page in (1, 2):
        c.drawImage(str(args.render_dir / f"native-{page}.png"), 0, 0, width, height)
        c.showPage()
    c.save()
    subprocess.run([
        str(args.pdftoppm), "-r", "110", "-png", str(scanned),
        str(args.render_dir / "scanned-preview"),
    ], check=True)
    truth = {
        "schema_version": "1.0", "dataset_version": "pdf-v0.1.0",
        "kind": "fictional workflow fixtures; same content in two representations",
        "expected": {
            "invoice_number": "AR-2026-101", "invoice_date": "2026-09-22",
            "due_date": "2026-10-22", "supplier_trn": "000000000000701",
            "currency": "AED", "subtotal": "1200.00", "vat": "60.00",
            "total": "1260.00", "payment_terms": "خلال 30 يوماً",
        },
        "files": [{"name": p.name, "sha256": hashlib.sha256(p.read_bytes()).hexdigest(),
                   "bytes": p.stat().st_size, "pages": 2} for p in (native, scanned)],
    }
    (args.output / "source-truth.json").write_text(
        json.dumps(truth, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(json.dumps(truth, ensure_ascii=True))


if __name__ == "__main__":
    main()
