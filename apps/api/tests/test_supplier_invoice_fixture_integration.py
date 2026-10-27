"""Independent frozen truth; actual public native PDF proof is labelled separately."""
from copy import deepcopy
from hashlib import sha256
import importlib.util
import json
from pathlib import Path

import pdfplumber
import pytest

from app.local_extraction import LocalInvoiceExtractor, _ExtractedPage

ROOT = Path(__file__).resolve().parents[3]
FIXTURES = ROOT / "samples/supplier-invoice/v0.1.0"
SPEC = importlib.util.spec_from_file_location("supplier_invoice_fixture_evaluate", FIXTURES / "evaluate.py")
EVALUATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(EVALUATE)
TRUTH = EVALUATE.load_truth()


@pytest.mark.parametrize("doc", TRUTH["documents"], ids=lambda doc: doc["id"])
def test_frozen_text_headers_roles_labelled_rows_and_exact_original_quotes(doc):
    page = EVALUATE.text_page((FIXTURES / (doc["id"] + ".txt")).read_text(encoding="utf-8"))
    before = deepcopy(page)
    fields, _ = LocalInvoiceExtractor._supplier_fields([page])
    assert LocalInvoiceExtractor._supplier_document_type([page]) == doc["role"]
    expected = {(key, 0): value for key, value in doc["expected"].items()} | EVALUATE.expected_rows(TRUTH)
    EVALUATE.check_fields([page], fields, expected)
    assert page == before, "Normalization must not rewrite source text/geometry"


@pytest.mark.parametrize("case", TRUTH["numericCases"], ids=lambda case: repr(case["raw"]))
def test_frozen_ascii_numeric_policy_and_preserved_invalid_raw_value(case):
    assert LocalInvoiceExtractor._supplier_normalize(case["raw"], "Decimal") == case["normalized"]
    if not case["raw"]:
        return  # Empty source cells are missing coverage, not invented field evidence.
    page = EVALUATE.text_page("INVOICE\nTotal: " + case["raw"] + "\n")
    before = deepcopy(page)
    fields, warnings = LocalInvoiceExtractor._supplier_fields([page])
    total = next(field for field in fields if field.key == "total")
    assert total.value == case["raw"] and total.normalized_value == case["normalized"]
    quote = total.evidence[0]
    assert quote.quote == case["raw"]
    assert page.canonical_text.encode("utf-8")[quote.start_utf8:quote.end_utf8].decode("utf-8") == case["raw"]
    assert any(warning.code == "numeric_requires_review" and warning.field_key == "total" and warning.occurrence == 0 for warning in warnings) == (case["normalized"] is None)
    assert page == before


@pytest.mark.parametrize("doc", [doc for doc in TRUTH["documents"] if "native-pdf" in doc["representation"]], ids=lambda doc: doc["id"])
def test_actual_public_native_pdf_extraction_schema_version_profile_rows_and_geometry(doc):
    result = LocalInvoiceExtractor(input_profile="supplier-invoice-v1", language="eng").extract(FIXTURES / (doc["id"] + ".pdf"), "application/pdf")
    assert result.schema_version == "2.0"
    assert result.document_type == doc["role"]
    assert result.provider.adapter_version == "local-1.6.0" and result.provider.model == "pdfplumber"
    assert result.outcome == "Partial"
    assert any(warning.code == "line_coverage_incomplete" for warning in result.warnings)
    assert not any(warning.code == "line_table_requires_review" for warning in result.warnings), "Fixture footer is outside the complete two-row table"
    assert {field.occurrence for field in result.fields if field.occurrence > 0} == {1, 2}
    expected = {(key, 0): value for key, value in doc["expected"].items()} | EVALUATE.expected_rows(TRUTH)
    EVALUATE.check_fields(result.pages, result.fields, expected, real_geometry=True)


def native_table_pages():
    pack = ROOT / "samples/supplier-invoice/native-table-v0.1.0"
    manifest = json.loads((pack / "manifest.json").read_text("utf-8"))
    path = pack / manifest["file"]
    assert sha256(path.read_bytes()).hexdigest() == manifest["sha256"]
    with pdfplumber.open(path) as pdf:
        assert len(pdf.pages) == manifest["pages"] == 1
        return [LocalInvoiceExtractor._native_page(page, index) for index, page in enumerate(pdf.pages, 1)]


def test_frozen_native_table_geometry_six_fields_without_claiming_ocr_public_path():
    pages = native_table_pages()
    before = deepcopy(pages)
    fields, _ = LocalInvoiceExtractor._supplier_fields(pages)
    EVALUATE.check_fields(pages, fields, EVALUATE.expected_rows(TRUTH), real_geometry=True)
    assert pages == before


def test_concatenated_phase7_table_without_geometry_cannot_infer_rows():
    pages = native_table_pages()
    for page in pages:
        for line in page.lines:
            line.character_boxes = [None] * len(line.text)
    fields, warnings = LocalInvoiceExtractor._supplier_fields(pages)
    assert not any(field.occurrence > 0 for field in fields)
    assert any(warning.code == "line_table_requires_review" for warning in warnings)


def test_real_native_table_second_page_repeat_header_keeps_unique_occurrences():
    original = native_table_pages()[0]
    lines = [line for line in original.lines if line.text.startswith(("DescriptionQty", "Document review training", "Workflow configuration session"))]
    pages = []
    for number, row in enumerate(lines[1:], 1):
        selected = deepcopy([lines[0], row])
        pages.append(_ExtractedPage(number, "\n".join(line.text for line in selected) + "\n", selected, 0, None, False, False, "native"))
    fields, _ = LocalInvoiceExtractor._supplier_fields(pages)
    EVALUATE.check_fields(pages, fields, EVALUATE.expected_rows(TRUTH), real_geometry=True)
    assert {field.evidence[0].page_number for field in fields if field.occurrence == 1} == {1}
    assert {field.evidence[0].page_number for field in fields if field.occurrence == 2} == {2}


@pytest.mark.parametrize("titles", ["Supplier statement", "INVOICE\nPURCHASE ORDER", "Invoice Number: SINV-0001"])
def test_absent_conflicting_or_identifier_only_title_requires_role_review(titles):
    pages = [EVALUATE.text_page(titles + "\nTotal: 1260.00\n")]
    role = LocalInvoiceExtractor._supplier_document_type(pages)
    fields, _ = LocalInvoiceExtractor._supplier_fields(pages)
    assert role == "unknown"
    assert any(warning.code == "document_role_requires_review" for warning in LocalInvoiceExtractor._supplier_coverage(fields, role))


def test_explicit_row_total_never_becomes_document_total():
    page = EVALUATE.text_page("INVOICE\nInvoice Number: SINV-0003\nLine 1 Description: Training\nLine 1 Quantity: 1\nLine 1 Unit Price: 10.00\nLine 1 Total: 10.00\n")
    fields, _ = LocalInvoiceExtractor._supplier_fields([page])
    assert not any(field.key == "total" for field in fields)
    assert next(field for field in fields if field.key == "line_total").value == "10.00"
    assert any(warning.code == "supplier_required_fields_missing" for warning in LocalInvoiceExtractor._supplier_coverage(fields, "invoice"))


@pytest.mark.parametrize("value,kind", [("٢٠٢٦-10-06", "Date"), ("2026-02-30", "Date"), ("٠00000000000701", "Identifier")])
def test_unsupported_digits_and_invalid_dates_do_not_normalize(value, kind):
    assert LocalInvoiceExtractor._supplier_normalize(value, kind) is None


def test_legacy_numeric_normalization_remains_distinct():
    from app.arabic_text import normalize_value
    assert normalize_value("١٬٠٥٠٫٥٠", "Decimal") == "1050.50"
    assert LocalInvoiceExtractor._supplier_normalize("١٬٠٥٠٫٥٠", "Decimal") is None
    assert LocalInvoiceExtractor().input_profile == "legacy"


def test_native_row_with_unboxed_nonspace_character_is_not_inferred():
    page = native_table_pages()[0]
    row = next(line for line in page.lines if line.text.startswith("Document review training"))
    row.character_boxes[3] = None
    fields, warnings = LocalInvoiceExtractor._supplier_fields([page])
    assert not any(field.occurrence > 0 for field in fields)
    assert any(warning.code == "line_table_requires_review" for warning in warnings)


def test_wrapped_native_description_is_not_guessed_into_next_row():
    original = native_table_pages()[0]
    header = next(line for line in original.lines if line.text.startswith("DescriptionQty"))
    row = deepcopy(next(line for line in original.lines if line.text.startswith("Document review training")))
    end = len("Document review training")
    row.text = row.text[:end]
    row.character_boxes = row.character_boxes[:end]
    row.confidences = row.confidences[:end]
    page = _ExtractedPage(1, header.text + "\n" + row.text + "\n", [header, row], 0, None, False, False, "native")
    fields, warnings = LocalInvoiceExtractor._supplier_fields([page])
    assert not any(field.occurrence > 0 for field in fields)
    assert any(warning.code == "line_table_requires_review" for warning in warnings)


def test_restarted_labelled_row_identity_requires_review_without_overwrite():
    first = EVALUATE.text_page("INVOICE\nLine 1 Description: First\nLine 1 Quantity: 1\nLine 1 Unit Price: 10.00\n", 1)
    second = EVALUATE.text_page("Line 1 Description: Second\nLine 1 Quantity: 2\nLine 1 Unit Price: 20.00\n", 2)
    fields, warnings = LocalInvoiceExtractor._supplier_fields([first, second])
    assert not any(field.occurrence > 0 for field in fields)
    assert any(warning.code == "field_identity_ambiguous" for warning in warnings)


@pytest.mark.parametrize("label,key", [("رقم الفاتورة", "invoice_number"), ("رقم أمر الشراء", "purchase_order_number")])
def test_string_identifier_with_unsupported_digits_keeps_raw_evidence_and_requires_review(label, key):
    page = EVALUATE.text_page(label + ": DOC-٠٠1\n")
    fields, warnings = LocalInvoiceExtractor._supplier_fields([page])
    field = next(field for field in fields if field.key == key)
    assert field.value == "DOC-٠٠1" and field.normalized_value is None
    quote = field.evidence[0]
    assert quote.quote == field.value
    assert page.canonical_text.encode("utf-8")[quote.start_utf8:quote.end_utf8].decode("utf-8") == field.value
    assert any(warning.code == "numeric_requires_review" and warning.field_key == key for warning in warnings)


def test_native_table_header_with_unboxed_glyph_cannot_define_cell_anchors():
    page = native_table_pages()[0]
    header = next(line for line in page.lines if line.text.startswith("DescriptionQty"))
    header.character_boxes[3] = None
    fields, warnings = LocalInvoiceExtractor._supplier_fields([page])
    assert not any(field.occurrence > 0 for field in fields)
    assert any(warning.code == "line_table_requires_review" for warning in warnings)


def test_overlapping_native_table_header_anchors_reject_without_exception():
    page = native_table_pages()[0]
    header = next(line for line in page.lines if line.text.startswith("DescriptionQty"))
    header.character_boxes = [(.1, .1, .101, .12)] * len(header.text)
    fields, warnings = LocalInvoiceExtractor._supplier_fields([page])
    assert not any(field.occurrence > 0 for field in fields)
    assert any(warning.code == "line_table_requires_review" for warning in warnings)


@pytest.mark.parametrize("raw", ["1,23", "١٬٠٥٠٫٥٠"])
def test_public_envelope_with_mocked_pages_keeps_invalid_amount_and_valid_partial_warnings(raw, monkeypatch):
    """Public serialization/schema proof; this deliberately mocks PDF reading."""
    page = EVALUATE.text_page("INVOICE\nInvoice Number: SINV-0010\nSupplier TRN: 000000000000701\nCurrency: AED\nTotal: " + raw + "\n")
    extractor = LocalInvoiceExtractor(input_profile="supplier-invoice-v1")
    monkeypatch.setattr(extractor, "_extract_pdf", lambda *args, **kwargs: ([page], False))
    result = extractor.extract(FIXTURES / "english-invoice.pdf", "application/pdf")
    assert result.outcome == "Partial" and result.provider.adapter_version == "local-1.6.0"
    total = next(field for field in result.fields if field.key == "total")
    assert total.value == raw and total.normalized_value is None
    quote = total.evidence[0]
    assert page.canonical_text.encode("utf-8")[quote.start_utf8:quote.end_utf8].decode("utf-8") == raw
    assert {warning.code for warning in result.warnings} >= {"numeric_requires_review", "supplier_required_fields_missing", "line_coverage_missing"}
