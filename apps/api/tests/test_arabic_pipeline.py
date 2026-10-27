"""Regressions against the public extractor envelope, not just private parsing helpers."""
from pathlib import Path
from unittest.mock import patch

import pytest

from app.local_extraction import LocalInvoiceExtractor, _ExtractedPage, _TextLine


@pytest.mark.parametrize("dates,warning", [
    ("تاريخ الاستحقاق: 21/10/2026", "due_date_requires_review"),
    ("تاريخ الاستحقاق: 2026-10-21\nDue Date: 2026-10-22", "due_date_conflicting"),
    ("Due Date: 2026-02-30", "due_date_requires_review"),
])
def test_missing_due_date_warning_survives_complete_result_validation(dates, warning):
    text = "Invoice Number: TEST-7\n" + dates + "\nTotal: 210.50\n"
    lines = [_TextLine(line, [(0.1, 0.1, 0.9, 0.2)]*len(line), [.95]*len(line))
             for line in text.splitlines()]
    page = _ExtractedPage(1, text, lines, 0, .95, False, source="ocr")
    extractor = LocalInvoiceExtractor(language="ara+eng")
    with (patch.object(extractor, "_verify_python_runtime"),
          patch.object(extractor, "_detect_media_type", return_value="application/pdf"),
          patch.object(extractor, "_extract_pdf", return_value=([page], True))):
        result = extractor.extract(Path("synthetic.pdf"), "application/pdf")
    assert result.outcome == "Partial"
    assert warning in {w.code for w in result.warnings}
    assert "due_date" not in {f.key for f in result.fields}
    assert next(f for f in result.fields if f.key == "total").normalized_value == "210.50"


@pytest.mark.parametrize("marker,description,tax", [
    ("Line", "Description", "Tax"),
    ("Line", "الوصف", "الضريبة"),
    ("البند", "وصف الصنف", "ضريبة القيمة المضافة"),
    ("السطر", "الوصف", "الضريبة"),
    ("البَنْد", "الوَصْف", "الضَّريبة"),
])
def test_labeled_rows_share_english_types_numbers_and_original_evidence(marker, description, tax):
    english = marker == "Line" and description == "Description"
    labels = ("Item Code", "Quantity", "Unit Price", "Total") if english else (
        "رمز الصنف", "الكَمـية", "سعر الوحدة", "الإِجـمالي")
    entries = [
        (labels[0], "TRAIN-01", "line_item_code", "String"),
        (description, "تدريب عمليّ ﻻختبار English", "line_description", "String"),
        (labels[1], "2", "line_quantity", "Decimal"),
        (labels[2], "400.00", "line_unit_price", "Decimal"),
        (tax, "40.00", "line_tax", "Decimal"),
        (labels[3], "840.00", "line_total", "Decimal"),
    ]
    pages = []
    for number in (1, 2):
        texts = [f"{marker} {number} {label}: {value}  " for label, value, _, _ in entries]
        # Distinct value/label geometry catches accidentally using matching-view offsets.
        lines = []
        for text in texts:
            start = text.index(":") + 2
            end = len(text.rstrip())
            boxes = [(0.0, 0.0, 0.1, 0.1)] * start + [(0.2, 0.3, 0.8, 0.4)] * (end - start)
            boxes += [None] * (len(text) - end)
            confidences = [0.1] * start + [0.95] * (end - start) + [None] * (len(text) - end)
            lines.append(_TextLine(text, boxes, confidences))
        pages.append(_ExtractedPage(number, "\n".join(texts) + "\n", lines, 0, .95, False))

    fields = LocalInvoiceExtractor._line_fields(pages)

    assert len(fields) == 12
    for field, (_, value, key, kind) in zip(fields, entries * 2, strict=True):
        assert (field.key, field.value_type, field.value, field.normalized_value) == (key, kind, value, value)
        evidence = field.evidence[0]
        assert evidence.page_number == field.occurrence
        source = pages[evidence.page_number - 1].canonical_text.encode("utf-8")
        assert source[evidence.start_utf8:evidence.end_utf8].decode("utf-8") == value == evidence.quote
        assert evidence.polygon == [[0.2, 0.3], [0.8, 0.3], [0.8, 0.4], [0.2, 0.4]]
        assert field.confidence == pytest.approx(.95)


def test_arabic_labeled_rows_survive_public_contract_and_keep_review_warning():
    text = (
        "رقم الفاتورة: INV-007\nتاريخ الفاتورة: 2026-09-22\n"
        "تاريخ الاستحقاق: 2026-10-22\nالعملة: AED\n"
        "ضريبة القيمة المضافة: 40.00\nالإجمالي: 840.00\n"
        "البند 1 الوصف: تدريب عملي\nالبند 1 الكمية: 2\nالبند 1 سعر الوحدة: 400.00\n"
        "Line 2 Unit Price: 100.00\n"
    )
    lines = [_TextLine(line, [(0.1, 0.1, 0.9, 0.2)] * len(line), [.95] * len(line))
             for line in text.splitlines()]
    page = _ExtractedPage(1, text, lines, 0, .95, False, source="ocr")
    extractor = LocalInvoiceExtractor(language="eng+ara")
    with (patch.object(extractor, "_verify_python_runtime"),
          patch.object(extractor, "_detect_media_type", return_value="application/pdf"),
          patch.object(extractor, "_extract_pdf", return_value=([page], True))):
        result = extractor.extract(Path("synthetic.pdf"), "application/pdf")

    fields = {(field.key, field.occurrence): field for field in result.fields}
    assert fields["invoice_date", 0].normalized_value == "2026-09-22"
    assert fields["due_date", 0].normalized_value == "2026-10-22"
    assert fields["vat", 0].normalized_value == "40.00"
    assert fields["total", 0].normalized_value == "840.00"
    assert fields["line_unit_price", 1].normalized_value == "400.00"
    assert fields["line_unit_price", 2].normalized_value == "100.00"
    assert result.pages[0].canonical_text == text
    assert result.outcome == "Partial"
    assert "arabic_ocr_requires_review" in {warning.code for warning in result.warnings}
