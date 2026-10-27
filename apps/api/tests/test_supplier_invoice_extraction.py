"""Focused new-profile regressions; legacy expectations are deliberately unchanged."""
from pathlib import Path
from unittest.mock import patch

import pytest

from app.arabic_text import normalize_supplier_number, normalize_value
from app.local_extraction import LocalInvoiceExtractor, _ExtractedPage, _TextLine

PROFILE = "supplier-invoice-v1"


def page(text, number=1):
    return _ExtractedPage(number, text, [
        _TextLine(line, [(0.1, 0.1, 0.9, 0.2)] * len(line), [.95] * len(line)) for line in text.splitlines()
    ], 0, None, False, source="native")


def public_extract(pages, *, used_ocr=False, **kwargs):
    extractor = LocalInvoiceExtractor(**kwargs)
    with (patch.object(extractor, "_verify_python_runtime"),
          patch.object(extractor, "_detect_media_type", return_value="application/pdf"),
          patch.object(extractor, "_extract_pdf", return_value=(pages, used_ocr))):
        return extractor.extract(Path("synthetic.pdf"), "application/pdf")


@pytest.mark.parametrize("source,expected", [
    ("1050.50", "1050.50"), ("1,050.50", "1050.50"), ("0", "0"),
    ("0.000001", "0.000001"), ("0001.00", "0001.00"),
    ("999999999999999999.999999", "999999999999999999.999999"),
    ("1,23", None), ("1.234,56", None), ("1234,567", None), ("1,,234", None),
    ("1,234٬567", None), ("1 050.50", None), ("١٬٠٥٠٫٥٠", None), ("۱٬۰۵۰٫۵۰", None),
    ("١050.50", None), ("1٬050٫50", None), ("1.2345678", None), ("-10.00", None),
    ("+10", None), ("1e3", None), ("AED 10", None), ("10 EA", None), (".50", None),
    ("1000000000000000000", None), ("10.", None), ("", None),
])
def test_strict_amount_syntax(source, expected):
    assert normalize_supplier_number(source) == expected


@pytest.mark.parametrize("text,expected", [
    ("TAX INVOICE\nPurchase Order Number: PO-7\n", "invoice"),
    ("PURCHASE ORDER\nInvoice Number: external-reference\n", "purchase_order"),
    ("أَمْر شـراء\nرقم أمر الشراء: PO-7\n", "purchase_order"),
    ("فاتورة ضريبية\nPurchase Order Number: PO-7\n", "invoice"),
    ("عرض سعر\nرقم عرض السعر: Q-7\n", "quotation"),
    ("INVOICE\nPURCHASE ORDER\n", "unknown"),
    ("Purchase Order Number: PO-7\nTotal: 50\n", "unknown"),
])
def test_role_uses_explicit_title_not_reference(text, expected):
    assert LocalInvoiceExtractor._document_type([page(text)], input_profile=PROFILE) == expected


def test_arabic_po_number_alias_retains_original_quote_and_zeroes():
    text = "أمر شراء\nرقم أمر الشراء: PO-0007\nالرقم الضريبي: 0000007\nالعملة: AED\nالإجمالي: 50.00\n"
    result = public_extract([page(text)], input_profile=PROFILE)
    field = next(field for field in result.fields if field.key == "purchase_order_number")
    assert result.document_type == "purchase_order"
    assert field.normalized_value == "PO-0007"
    evidence = field.evidence[0]
    assert text.encode("utf-8")[evidence.start_utf8:evidence.end_utf8].decode() == evidence.quote == field.value


@pytest.mark.parametrize("key,label,value", [
    ("total", "Total", "1,23"), ("total", "Total", "١٢٣٫٤٥"),
    ("invoice_number", "Invoice Number", "IN-٠٠١"),
    ("supplier_trn", "Supplier TRN", "٠٠١٢٣"),
    ("invoice_date", "Invoice Date", "٢٠٢٦-١٠-٠٦"),
    ("due_date", "Due Date", "٢٠٢٦-١٠-٠٦"),
])
def test_unsupported_numbers_keep_raw_evidence_and_explicit_uncertainty(key, label, value):
    result = public_extract([page(f"INVOICE\n{label}: {value}\n")], input_profile=PROFILE)
    field = next(field for field in result.fields if field.key == key)
    assert field.value == value
    assert field.normalized_value is None
    assert field.evidence[0].quote == value
    assert any(warning.code == "numeric_requires_review" and warning.field_key == key
               for warning in result.warnings)
    assert result.outcome == "Partial"


def test_line_total_does_not_fill_missing_document_total():
    fields = LocalInvoiceExtractor._fields([
        page("INVOICE\nInvoice Number: I-7\nLine 1 Total: 50.00\nLine total: 40.00\n")
    ], input_profile=PROFILE)
    assert "total" not in {field.key for field in fields}
    assert next(field for field in fields if field.key == "line_total").value == "50.00"


def table_line(cells):
    text, boxes = "", []
    for anchor, value in cells:
        text += value
        boxes.extend((anchor + index * .001, .2, anchor + (index + 1) * .001, .21)
                     for index in range(len(value)))
    return _TextLine(text, boxes, [.95] * len(text))


def table_page(header=None, row=None):
    lines = [header or table_line([(0.07, "Description"), (.54, "Qty"), (.64, "Unit price"), (.82, "Net AED")]),
             row or table_line([(.07, "Training"), (.55, "2"), (.66, "400.00"), (.83, "800.00")])]
    return _ExtractedPage(1, "\n".join(line.text for line in lines) + "\n", lines, 0, .95, False)


def test_geometry_recovers_adjacent_cells_without_gross_total_inference():
    source = table_page()
    fields, warnings, _ = LocalInvoiceExtractor._table_fields([source])
    assert not warnings
    assert [(field.key, field.value) for field in fields] == [
        ("line_description", "Training"), ("line_quantity", "2"), ("line_unit_price", "400.00")]
    for field in fields:
        evidence = field.evidence[0]
        assert source.canonical_text.encode()[evidence.start_utf8:evidence.end_utf8].decode() == evidence.quote
        assert evidence.polygon and field.confidence == pytest.approx(.95)


@pytest.mark.parametrize("failure", ["missing_header_box", "missing_row_box", "outside_columns", "overlapping_headers", "crossed_cell"])
def test_uncertain_geometry_refuses_entire_row(failure):
    source = table_page()
    if failure == "missing_header_box":
        source.lines[0].character_boxes[0] = None
    elif failure == "missing_row_box":
        source.lines[1].character_boxes[0] = None
    elif failure == "outside_columns":
        source.lines[1].character_boxes[0] = (0, .2, .001, .21)
    elif failure == "overlapping_headers":
        source.lines[0] = table_line([(.07, "Description"), (.07, "Qty"), (.64, "Unit price"), (.82, "Net AED")])
    else:
        source.lines[1].character_boxes[0] = (.1, .2, .9, .21)
    fields, warnings, _ = LocalInvoiceExtractor._table_fields([source])
    assert not fields
    assert any(warning.code == "line_table_requires_review" for warning in warnings)


def test_unreadable_table_cannot_donate_description_total_to_document():
    text = "INVOICE\nInvoice Number: I-7\nDescriptionQtyUnit priceNet AED\nTotal: 400\n"
    fields = LocalInvoiceExtractor._fields([page(text)], input_profile=PROFILE)
    assert "total" not in {field.key for field in fields}


def test_public_multiple_refused_tables_keep_unique_contract_ordered_warnings():
    text = "INVOICE\nInvoice Number: I-7\nDescriptionQtyUnit priceNet AED\nX123\nDescriptionQtyUnit priceNet AED\nX456\n"
    result = public_extract([page(text)], input_profile=PROFILE)
    assert result.outcome == "Partial"
    assert len([warning for warning in result.warnings if warning.code == "line_table_requires_review"]) == 1


def test_table_description_due_date_cannot_become_document_due_date():
    text = "INVOICE\nInvoice Number: I-7\nDescriptionQtyUnit priceNet AED\nDue Date: 2026-10-21\n"
    fields = LocalInvoiceExtractor._fields([page(text)], input_profile=PROFILE)
    assert "due_date" not in {field.key for field in fields}
    fields = LocalInvoiceExtractor._fields([
        page("INVOICE\nInvoice Number: I-7\nLine 1 Description: Due Date: 2026-10-21\n")
    ], input_profile=PROFILE)
    assert "due_date" not in {field.key for field in fields}


def test_supplier_ocr_metadata_only_names_models_that_were_configured():
    source = page("INVOICE\nInvoice Number: I-7\nTotal: 50\n")
    source.source = "ocr"
    english = public_extract([source], used_ocr=True, input_profile=PROFILE)
    mixed = public_extract([source], used_ocr=True, input_profile=PROFILE, language="eng+ara", arabic_model="best")
    assert english.provider.adapter_version == mixed.provider.adapter_version == "local-1.6.0"
    assert "/ara-" not in english.provider.model_version
    assert "/ara-best/" in mixed.provider.model_version
    assert english.provider.model_version.endswith("/layout-block")


@pytest.mark.parametrize("language,model,layout", [
    ("eng", "fast", "block"), ("eng", "best", "block"),
    ("ara", "fast", "block"), ("ara", "best", "block"),
    ("eng+ara", "fast", "block"), ("ara+eng", "best", "block"),
    ("eng+ara", "best", "lines-v1"), ("ara+eng", "fast", "lines-v1"),
])
@pytest.mark.parametrize("sources", [("native",), ("ocr",), ("native", "ocr")])
def test_supplier_provider_identity_fits_existing_backend_bounds(language, model, layout, sources):
    pages = [page("INVOICE\nInvoice Number: I-7\nTotal: 50\n", number=index)
             for index, _ in enumerate(sources, 1)]
    for source, kind in zip(pages, sources, strict=True):
        source.source = kind
    result = public_extract(pages, used_ocr="ocr" in sources, input_profile=PROFILE,
                            language=language, arabic_model=model, layout_profile=layout)
    assert result.provider.adapter_version == "local-1.6.0"
    assert len(result.provider.model_version) <= 100
    assert ("/ara-" in result.provider.model_version) == ("ara" in language and "ocr" in sources)


def test_labeled_fields_cannot_merge_with_unrelated_ordinary_table_row():
    source = table_page()
    source.lines.append(_TextLine("Row amounts are NET AED.", [None] * 24, [None] * 24))
    text = "Line 1 Item Code: UNRELATED"
    source.lines.append(_TextLine(text, [None] * len(text), [None] * len(text)))
    source.canonical_text = "\n".join(line.text for line in source.lines) + "\n"
    fields, warnings = LocalInvoiceExtractor._supplier_fields([source])
    assert not any(field.occurrence > 0 for field in fields)
    assert any(warning.code == "line_table_requires_review" for warning in warnings)


def test_repeated_explicit_line_identity_is_unavailable_not_arbitrarily_selected():
    fields, warnings = LocalInvoiceExtractor._supplier_fields([
        page("Line 1 Quantity: 2\n"), page("Line 1 Quantity: 3\n", number=2)])
    assert not fields
    assert any(warning.code == "field_identity_ambiguous" for warning in warnings)


def test_profile_identity_and_legacy_arabic_normalization_are_preserved():
    text = "Invoice Number: I-7\nTotal: ١٬٠٥٠٫٥٠\n"
    legacy = public_extract([page(text)])
    supplier = public_extract([page("INVOICE\n" + text)], input_profile=PROFILE)
    assert legacy.provider.adapter_version == "local-1.3.0"
    assert next(field for field in legacy.fields if field.key == "total").normalized_value == "1050.50"
    assert supplier.provider.adapter_version == "local-1.6.0"
    assert next(field for field in supplier.fields if field.key == "total").normalized_value is None
    assert normalize_value("١٬٠٥٠٫٥٠", "Decimal") == "1050.50"


def test_input_profile_environment_is_opt_in_and_validated(monkeypatch):
    monkeypatch.delenv("LOCAL_INPUT_PROFILE", raising=False)
    assert LocalInvoiceExtractor.from_environment().input_profile == "legacy"
    monkeypatch.setenv("LOCAL_INPUT_PROFILE", PROFILE)
    assert LocalInvoiceExtractor.from_environment().input_profile == PROFILE
    with pytest.raises(ValueError, match="LOCAL_INPUT_PROFILE"):
        LocalInvoiceExtractor(input_profile="unknown")
