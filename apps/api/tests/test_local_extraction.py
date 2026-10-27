import asyncio
from dataclasses import asdict
import hashlib
from importlib.metadata import PackageNotFoundError
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

from app.extraction import ExtractionError, MockInvoiceExtractor, get_extractor
from app.fixture_contract import validate_error_envelope, validate_normalized_result
from app.local_extraction import (
    ADAPTER_VERSION,
    ENG_MODEL_VERSION,
    PDFMINER_VERSION,
    PDFPLUMBER_VERSION,
    TESSERACT_VERSION,
    LocalInvoiceExtractor,
    _ExtractedPage,
    _TextLine,
)
from app.main import app


REPO_ROOT = Path(__file__).resolve().parents[3]
FIXTURE_ROOT = REPO_ROOT / "samples" / "phase-2"
GOLDEN_ROOT = REPO_ROOT / "apps" / "api" / "tests" / "goldens" / "local-provider" / "local-1.3.0"
RUN_RUNTIME = os.getenv("RUN_LOCAL_PROVIDER_INTEGRATION") == "1"


def load_expectation(filename: str) -> dict:
    return json.loads(
        (FIXTURE_ROOT / "expected" / filename).read_text(encoding="utf-8")
    )


def result_payload(result) -> dict:
    payload = asdict(result)
    validate_normalized_result(payload)
    return payload


def assert_duration_neutral_golden(
    test: unittest.TestCase, result, fixture_id: str
) -> None:
    payload = asdict(result)
    payload["metrics"]["provider_duration_ms"] = 0
    expected = json.loads((GOLDEN_ROOT / f"{fixture_id}.json").read_text(encoding="utf-8"))
    test.assertEqual(expected, payload)


def assert_exact_expectation(test: unittest.TestCase, result, expectation: dict) -> None:
    payload = result_payload(result)
    for key in ("schema_version", "outcome", "document_type", "language"):
        test.assertEqual(expectation[key], payload[key], key)
    test.assertEqual("local", payload["provider"]["alias"])
    test.assertEqual(expectation["pages"], payload["pages"])
    projected_fields = [
        {key: value for key, value in field.items() if key != "confidence"}
        for field in payload["fields"]
    ]
    test.assertEqual(expectation["fields"], projected_fields)
    test.assertEqual(expectation["warnings"], payload["warnings"])


def assert_semantic_expectation(test: unittest.TestCase, result, expectation: dict) -> None:
    payload = result_payload(result)
    for key in ("schema_version", "outcome", "document_type", "language"):
        test.assertEqual(expectation[key], payload[key], key)
    test.assertEqual("local", payload["provider"]["alias"])
    test.assertEqual(expectation["expected_page_count"], len(payload["pages"]))
    actual_fields = {
        (field["key"], field["occurrence"]): field for field in payload["fields"]
    }
    for expected_field in expectation["fields"]:
        identity = (expected_field["key"], expected_field["occurrence"])
        test.assertIn(identity, actual_fields)
        actual = actual_fields[identity]
        for key in ("value_type", "value", "normalized_value"):
            test.assertEqual(expected_field[key], actual[key], f"{identity}.{key}")
        for evidence in actual["evidence"]:
            page = payload["pages"][evidence["page_number"] - 1]["canonical_text"]
            selected = page.encode("utf-8")[
                evidence["start_utf8"]:evidence["end_utf8"]
            ].decode("utf-8")
            test.assertEqual(evidence["quote"], selected)
    warning_codes = {warning["code"] for warning in payload["warnings"]}
    test.assertTrue(set(expectation["required_warning_codes"]).issubset(warning_codes))


def assert_error_expectation(
    test: unittest.TestCase, error: ExtractionError, expectation: dict, path: Path
) -> None:
    test.assertEqual(expectation["error"]["code"], error.code)
    test.assertEqual(expectation["error"]["retryable"], error.retryable)
    test.assertGreater(len(error.summary), 0)
    test.assertLessEqual(len(error.summary), 300)
    test.assertNotIn(str(path), error.summary)
    test.assertNotIn(path.name, error.summary)


class LocalProviderSelectionTests(unittest.TestCase):
    def test_provider_selection_preserves_mock_and_selects_local(self):
        self.assertIsInstance(get_extractor("mock"), MockInvoiceExtractor)
        with patch.dict(
            os.environ,
            {
                "OCR_LANGUAGE": "eng",
                "OCR_TIMEOUT_SECONDS": "30",
                "OCR_RENDER_DPI": "300",
            },
        ):
            self.assertIsInstance(get_extractor("local"), LocalInvoiceExtractor)
        with self.assertRaisesRegex(ValueError, "Unsupported extraction provider"):
            get_extractor("unknown")

    def test_uae_compound_summary_labels_stop_before_neighboring_values(self):
        cases = (
            (
                "Supplier TRN: 000000000000701 | TIN: 0000000000",
                ("Supplier TRN",),
                "000000000000701",
            ),
            (
                "Recipient TRN: 000000000000711 Currency: AED",
                ("Currency",),
                "AED",
            ),
            (
                "Payment information Subtotal (AED): 8,400.00",
                ("Subtotal",),
                "8,400.00",
            ),
            (
                "Terms: 50% now, balance Net 30 Discount (AED): 1,000.00",
                ("Terms",),
                "50% now, balance Net 30",
            ),
        )
        for text, prefixes, expected in cases:
            with self.subTest(text=text):
                match = LocalInvoiceExtractor._match_line(text, prefixes)
                self.assertIsNotNone(match)
                self.assertEqual(expected, match[0])

    def test_specific_uae_vat_label_wins_over_earlier_invoice_currency_vat(self):
        lines = [
            _TextLine("Invoice VAT (USD): 75.00", [None] * 24, [None] * 24),
            _TextLine("VAT charged (AED): 275.44", [None] * 25, [None] * 25),
        ]
        page = _ExtractedPage(
            1,
            "\n".join(line.text for line in lines) + "\n",
            lines,
            0,
            None,
            False,
        )

        vat = next(field for field in LocalInvoiceExtractor._fields([page]) if field.key == "vat")

        self.assertEqual("275.44", vat.value)

    def test_classifies_procurement_document_titles(self):
        cases = {
            "QUOTATION\nQuotation Number: Q-1\n": "quotation",
            "PURCHASE ORDER\nPurchase Order Number: PO-1\n": "purchase_order",
            "TAX INVOICE\nInvoice Number: I-1\n": "invoice",
        }
        for text, expected in cases.items():
            with self.subTest(expected=expected):
                page = _ExtractedPage(1, text, [], 0, None, False)
                self.assertEqual(expected, LocalInvoiceExtractor._document_type([page]))

    def test_extracts_repeated_reconciliation_line_fields_with_page_evidence(self):
        texts = [
            "Line 1 Item Code: DOC-WORKFLOW",
            "Line 1 Description: Document workflow configuration",
            "Line 1 Quantity: 1",
            "Line 1 Unit Price: 6000.00",
            "Line 1 Tax: 300.00",
            "Line 1 Total: 6300.00",
            "Line 2 Item Code: TRAINING",
            "Line 2 Quantity: 2",
        ]
        lines = [_TextLine(text, [None] * len(text), [None] * len(text)) for text in texts]
        page = _ExtractedPage(2, "\n".join(texts) + "\n", lines, 0, None, False)

        fields = LocalInvoiceExtractor._line_fields([page])

        self.assertEqual(8, len(fields))
        quantity = next(field for field in fields if field.key == "line_quantity" and field.occurrence == 2)
        self.assertEqual("2", quantity.normalized_value)
        self.assertEqual(2, quantity.evidence[0].page_number)
        selected = page.canonical_text.encode("utf-8")[
            quantity.evidence[0].start_utf8:quantity.evidence[0].end_utf8
        ].decode("utf-8")
        self.assertEqual("2", selected)

    def test_local_configuration_fails_fast_for_invalid_settings(self):
        invalid_settings = (
            ({"OCR_LANGUAGE": "fra"}, "OCR_LANGUAGE must be"),
            ({"OCR_TIMEOUT_SECONDS": "not-a-number"}, "must be numeric"),
            ({"OCR_TIMEOUT_SECONDS": "0"}, "between 1 and 120"),
            ({"OCR_RENDER_DPI": "149"}, "between 150 and 400"),
        )
        defaults = {
            "OCR_LANGUAGE": "eng",
            "OCR_TIMEOUT_SECONDS": "30",
            "OCR_RENDER_DPI": "300",
        }
        for changes, message in invalid_settings:
            with self.subTest(changes=changes):
                environment = defaults | changes
                with patch.dict(os.environ, environment, clear=False):
                    with self.assertRaisesRegex(ValueError, message):
                        LocalInvoiceExtractor.from_environment()

    def test_local_runtime_fails_fast_when_pins_are_missing_or_mismatched(self):
        expected = {
            "pdfplumber": PDFPLUMBER_VERSION,
            "pdfminer.six": PDFMINER_VERSION,
            "pypdfium2": "5.12.1",
            "pytesseract": "0.3.13",
            "Pillow": "13.0.0",
        }
        with patch("app.local_extraction.version", side_effect=expected.__getitem__):
            LocalInvoiceExtractor._verify_python_runtime()

        for change in ({"pdfplumber": "0.0.0"}, {"Pillow": "12.3.0"}):
            with self.subTest(change=change):
                mismatched = expected | change
                with patch("app.local_extraction.version", side_effect=mismatched.__getitem__):
                    with self.assertRaises(ExtractionError) as captured:
                        LocalInvoiceExtractor._verify_python_runtime()
                self.assertEqual("ocr_unavailable", captured.exception.code)
                self.assertFalse(captured.exception.retryable)

    def test_ocr_engine_and_required_models_fail_fast_when_unpinned_or_missing(self):
        cases = (
            (
                types.SimpleNamespace(
                    get_languages=lambda config: ["eng", "osd"],
                    get_tesseract_version=lambda: "4.0.0",
                ),
                "version",
            ),
            (
                types.SimpleNamespace(
                    get_languages=lambda config: ["eng"],
                    get_tesseract_version=lambda: TESSERACT_VERSION,
                ),
                "models",
            ),
        )
        for runtime, message in cases:
            with self.subTest(message=message):
                with self.assertRaises(ExtractionError) as captured:
                    LocalInvoiceExtractor()._verify_ocr_engine(runtime)
                self.assertEqual("ocr_unavailable", captured.exception.code)
                self.assertFalse(captured.exception.retryable)
                self.assertIn(message, captured.exception.summary.lower())

        with patch(
            "app.local_extraction.version",
            side_effect=PackageNotFoundError("pdfplumber"),
        ):
            with self.assertRaises(ExtractionError) as captured:
                LocalInvoiceExtractor._verify_python_runtime()
        self.assertEqual("ocr_unavailable", captured.exception.code)
        self.assertFalse(captured.exception.retryable)

    def test_ocr_model_hashes_accept_exact_files_and_reject_missing_or_mismatch(self):
        with tempfile.TemporaryDirectory() as directory:
            tessdata = Path(directory)
            eng = tessdata / "eng.traineddata"
            osd = tessdata / "osd.traineddata"
            eng.write_bytes(b"pinned-eng")
            osd.write_bytes(b"pinned-osd")
            expected = {
                "ENG_MODEL_SHA256": hashlib.sha256(eng.read_bytes()).hexdigest(),
                "OSD_MODEL_SHA256": hashlib.sha256(osd.read_bytes()).hexdigest(),
            }
            with (
                patch.dict(os.environ, {"OCR_TESSDATA_DIR": directory}),
                patch("app.local_extraction.ENG_MODEL_SHA256", expected["ENG_MODEL_SHA256"]),
                patch("app.local_extraction.OSD_MODEL_SHA256", expected["OSD_MODEL_SHA256"]),
            ):
                LocalInvoiceExtractor._verify_model_hashes()

                eng.write_bytes(b"tampered-eng")
                with self.assertRaises(ExtractionError) as captured:
                    LocalInvoiceExtractor._verify_model_hashes()
                self.assertEqual("ocr_unavailable", captured.exception.code)
                self.assertIn("does not match", captured.exception.summary)

                eng.write_bytes(b"pinned-eng")
                osd.unlink()
                with self.assertRaises(ExtractionError) as captured:
                    LocalInvoiceExtractor._verify_model_hashes()
                self.assertEqual("ocr_unavailable", captured.exception.code)
                self.assertIn("unavailable", captured.exception.summary)


class LocalProviderFailureTests(unittest.TestCase):
    @staticmethod
    def _page(
        page_number: int,
        text: str,
        *,
        degraded: bool = False,
        confidence: float | None = None,
    ) -> _ExtractedPage:
        canonical = f"{text}\n" if text else ""
        line = _TextLine(text, [None] * len(text), [confidence] * len(text))
        return _ExtractedPage(
            page_number,
            canonical,
            [line] if text else [],
            0,
            confidence,
            degraded,
        )

    def test_unsupported_fixture_returns_safe_non_retryable_error(self):
        path = FIXTURE_ROOT / "generated" / "unsupported.txt"
        expectation = load_expectation("unsupported.expected.json")
        with patch.object(LocalInvoiceExtractor, "_verify_python_runtime"):
            with self.assertRaises(ExtractionError) as captured:
                LocalInvoiceExtractor().extract(path, "text/plain")
        assert_error_expectation(self, captured.exception, expectation, path)

    def test_detected_content_must_match_declared_media_type(self):
        path = FIXTURE_ROOT / "generated" / "invoice.png"
        with patch.object(LocalInvoiceExtractor, "_verify_python_runtime"):
            with self.assertRaises(ExtractionError) as captured:
                LocalInvoiceExtractor().extract(path, "image/jpeg")
        self.assertEqual("unsupported_media_type", captured.exception.code)
        self.assertFalse(captured.exception.retryable)
        self.assertNotIn(path.name, captured.exception.summary)

    def test_readable_document_without_invoice_fields_is_unsupported(self):
        extractor = LocalInvoiceExtractor()
        page = self._page(1, "Readable content without invoice labels", confidence=0.99)
        with (
            patch.object(extractor, "_verify_python_runtime"),
            patch.object(extractor, "_detect_media_type", return_value="image/png"),
            patch.object(extractor, "_open_image", return_value=(object(), 0)),
            patch.object(extractor, "_ocr_image", return_value=page),
            self.assertRaises(ExtractionError) as captured,
        ):
            extractor.extract(Path("ignored.png"), "image/png")
        self.assertEqual("unsupported_document", captured.exception.code)
        self.assertFalse(captured.exception.retryable)

    def test_blank_valid_image_is_document_unreadable(self):
        from PIL import Image

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "blank.png"
            Image.new("RGB", (32, 32), "white").save(path, format="PNG")
            extractor = LocalInvoiceExtractor()
            with (
                patch.object(extractor, "_verify_python_runtime"),
                patch.object(extractor, "_ocr_image", return_value=self._page(1, "")),
                self.assertRaises(ExtractionError) as captured,
            ):
                extractor.extract(path, "image/png")
        self.assertEqual("document_unreadable", captured.exception.code)
        self.assertFalse(captured.exception.retryable)

    def test_pdf_page_limit_is_enforced_before_rendering(self):
        class OversizedDocument:
            def __len__(self) -> int:
                return 51

        fake_pdfium = types.ModuleType("pypdfium2")
        fake_pdfium.PdfDocument = lambda path: OversizedDocument()
        with patch.dict(sys.modules, {"pypdfium2": fake_pdfium}):
            with self.assertRaises(ExtractionError) as captured:
                LocalInvoiceExtractor()._ocr_pdf(Path("oversized.pdf"))
        self.assertEqual("page_limit_exceeded", captured.exception.code)
        self.assertFalse(captured.exception.retryable)

    def test_decoded_pixel_budget_is_enforced(self):
        image = types.SimpleNamespace(width=5001, height=5000)
        with self.assertRaises(ExtractionError) as captured:
            LocalInvoiceExtractor._check_pixel_budget(image)
        self.assertEqual("input_too_large", captured.exception.code)
        self.assertFalse(captured.exception.retryable)

    def test_pdf_dimensions_are_rejected_before_rendering(self):
        class OversizedPage:
            render_called = False

            def get_size(self):
                return (20_000, 20_000)

            def render(self, *, scale):
                self.render_called = True
                raise AssertionError("render must not run after dimension rejection")

        class Document:
            page = OversizedPage()

            def __len__(self):
                return 1

            def __getitem__(self, index):
                return self.page

        document = Document()
        fake_pdfium = types.ModuleType("pypdfium2")
        fake_pdfium.PdfDocument = lambda path: document
        with patch.dict(sys.modules, {"pypdfium2": fake_pdfium}):
            with self.assertRaises(ExtractionError) as captured:
                LocalInvoiceExtractor()._ocr_pdf(Path("oversized-page.pdf"))
        self.assertEqual("input_too_large", captured.exception.code)
        self.assertFalse(document.page.render_called)

    def test_image_header_dimensions_and_decompression_bombs_reject_before_decode(self):
        from PIL import Image

        class OversizedSource:
            width = 5001
            height = 5000
            verify_called = False

            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc_value, traceback):
                return False

            def verify(self):
                self.verify_called = True

        oversized = OversizedSource()
        with patch.object(Image, "open", return_value=oversized):
            with self.assertRaises(ExtractionError) as captured:
                LocalInvoiceExtractor._open_image(Path("oversized.png"))
        self.assertEqual("input_too_large", captured.exception.code)
        self.assertFalse(oversized.verify_called)

        with patch.object(
            Image,
            "open",
            side_effect=Image.DecompressionBombWarning("decompression bomb"),
        ):
            with self.assertRaises(ExtractionError) as captured:
                LocalInvoiceExtractor._open_image(Path("bomb.png"))
        self.assertEqual("input_too_large", captured.exception.code)

    def test_mixed_pdf_keeps_native_pages_and_ocr_replaces_only_blank_pages(self):
        class PreflightDocument:
            def close(self) -> None:
                pass

        class NativeDocument:
            pages = [object(), object()]

            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc_value, traceback):
                return False

        fake_pdfium = types.ModuleType("pypdfium2")
        fake_pdfium.PdfDocument = lambda path: PreflightDocument()
        fake_pdfplumber = types.ModuleType("pdfplumber")
        fake_pdfplumber.open = lambda path: NativeDocument()
        fake_pdfminer = types.ModuleType("pdfminer")
        fake_pdfminer.__path__ = []
        fake_pdfdocument = types.ModuleType("pdfminer.pdfdocument")
        fake_pdfdocument.PDFPasswordIncorrect = type("PDFPasswordIncorrect", (Exception,), {})
        fake_pdfparser = types.ModuleType("pdfminer.pdfparser")
        fake_pdfparser.PDFSyntaxError = type("PDFSyntaxError", (Exception,), {})
        modules = {
            "pdfplumber": fake_pdfplumber,
            "pypdfium2": fake_pdfium,
            "pdfminer": fake_pdfminer,
            "pdfminer.pdfdocument": fake_pdfdocument,
            "pdfminer.pdfparser": fake_pdfparser,
        }
        native = self._page(1, "Invoice Number: INV-1")
        blank = self._page(2, "")
        ocr = self._page(2, "Total: 10.00", confidence=0.9)
        ocr.source = "ocr"
        extractor = LocalInvoiceExtractor()
        with (
            patch.dict(sys.modules, modules),
            patch.object(extractor, "_native_page", side_effect=[native, blank]),
            patch.object(extractor, "_ocr_pdf", return_value=[ocr]) as ocr_pdf,
        ):
            pages, used_ocr = extractor._extract_pdf(Path("mixed.pdf"))
        self.assertTrue(used_ocr)
        self.assertEqual([native, ocr], pages)
        ocr_pdf.assert_called_once_with(Path("mixed.pdf"), [2])

    def test_mixed_native_and_ocr_result_reports_truthful_hybrid_identity(self):
        native = self._page(1, "Invoice Number: INV-1")
        ocr = self._page(2, "Total: 10.00", confidence=0.9)
        ocr.source = "ocr"
        extractor = LocalInvoiceExtractor()
        with (
            patch.object(extractor, "_verify_python_runtime"),
            patch.object(extractor, "_detect_media_type", return_value="application/pdf"),
            patch.object(extractor, "_extract_pdf", return_value=([native, ocr], True)),
            patch("app.local_extraction.validate_normalized_result"),
        ):
            result = extractor.extract(Path("mixed.pdf"), "application/pdf")
        self.assertEqual("pdfplumber-tesseract-eng-osd", result.provider.model)
        self.assertIn("pdfplumber", result.provider.model_version)
        self.assertIn("tesseract", result.provider.model_version)

    def test_native_pdf_rotation_is_preserved(self):
        page = types.SimpleNamespace(chars=[], width=100, height=200, rotation=90)
        extracted = LocalInvoiceExtractor._native_page(page, 1)
        self.assertEqual(90, extracted.rotation_degrees)
        self.assertEqual("native", extracted.source)

    def test_exif_orientation_is_applied_and_reported(self):
        from PIL import Image

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "oriented.jpg"
            source = Image.new("RGB", (20, 10), "white")
            exif = Image.Exif()
            exif[274] = 6
            source.save(path, format="JPEG", exif=exif)
            corrected, rotation = LocalInvoiceExtractor._open_image(path)
        self.assertEqual(90, rotation)
        self.assertEqual((10, 20), corrected.size)
        ocr_data = {
            "text": ["Invoice"],
            "conf": [95],
            "block_num": [1],
            "par_num": [1],
            "line_num": [1],
            "left": [0],
            "top": [0],
            "width": [10],
            "height": [10],
        }
        extractor = LocalInvoiceExtractor()
        with (
            patch.object(extractor, "_ocr_orientation", return_value=0),
            patch.object(extractor, "_ocr_data", return_value=ocr_data),
        ):
            page = extractor._ocr_image(corrected, 1, applied_rotation=rotation)
        self.assertEqual(90, page.rotation_degrees)
        self.assertEqual("ocr", page.source)

    def test_media_detection_reads_only_bounded_header(self):
        class PathDouble:
            def __init__(self):
                self.read_sizes = []

            def open(self, mode):
                test = self

                class Stream(io.BytesIO):
                    def read(self, size=-1):
                        test.read_sizes.append(size)
                        return super().read(size)

                return Stream(b"%PDF-1.7" + b"x" * 1024)

            def read_bytes(self):
                raise AssertionError("unbounded read_bytes must not be used")

        path = PathDouble()
        media_type = LocalInvoiceExtractor._detect_media_type(path, "application/pdf")
        self.assertEqual("application/pdf", media_type)
        self.assertEqual([16], path.read_sizes)

    def test_warnings_are_degraded_bounded_and_deterministically_ordered(self):
        pages = [self._page(2, "second", degraded=True), self._page(1, "first", degraded=True)]
        invoice_number = types.SimpleNamespace(key="invoice_number")
        warnings = LocalInvoiceExtractor._warnings(pages, [invoice_number])
        identities = [
            (
                warning.code,
                warning.severity,
                warning.page_number,
                warning.field_key,
                warning.occurrence,
            )
            for warning in warnings
        ]
        self.assertEqual(
            [
                ("required_field_missing", "degraded", None, None, None),
                ("page_ocr_degraded", "degraded", 1, None, None),
                ("page_ocr_degraded", "degraded", 2, None, None),
            ],
            identities,
        )
        self.assertTrue(all(len(warning.safe_summary) <= 300 for warning in warnings))

    def test_page_ocr_confidence_is_weighted_by_non_whitespace_characters(self):
        data = {
            "text": ["A", "BBBBBBBBB"],
            "conf": [100, 0],
            "block_num": [1, 1],
            "par_num": [1, 1],
            "line_num": [1, 1],
            "left": [0, 10],
            "top": [0, 0],
            "width": [5, 45],
            "height": [10, 10],
        }
        page = LocalInvoiceExtractor._page_from_ocr(data, 1, 0, (100, 100))
        self.assertAlmostEqual(0.1, page.ocr_confidence)

    def test_missing_ocr_module_returns_safe_ocr_unavailable(self):
        extractor = LocalInvoiceExtractor()
        with patch.dict(sys.modules, {"pytesseract": None}):
            with self.assertRaises(ExtractionError) as captured:
                extractor._ocr_data(object())
        self.assertEqual("ocr_unavailable", captured.exception.code)
        self.assertFalse(captured.exception.retryable)
        self.assertNotIn("pytesseract", captured.exception.summary.lower())

    def test_ocr_timeout_is_safe_and_retryable(self):
        class FakeTesseractNotFoundError(RuntimeError):
            pass

        fake = types.SimpleNamespace(
            Output=types.SimpleNamespace(DICT="dict"),
            TesseractNotFoundError=FakeTesseractNotFoundError,
            get_languages=lambda config: ["eng", "osd"],
            get_tesseract_version=lambda: TESSERACT_VERSION,
            image_to_data=lambda *args, **kwargs: (_ for _ in ()).throw(
                RuntimeError("timeout at C:/sensitive/document.png")
            ),
        )
        with (
            patch.dict(sys.modules, {"pytesseract": fake}),
            patch.object(LocalInvoiceExtractor, "_verify_model_hashes"),
        ):
            with self.assertRaises(ExtractionError) as captured:
                LocalInvoiceExtractor()._ocr_data(object())
        self.assertEqual("provider_timeout", captured.exception.code)
        self.assertTrue(captured.exception.retryable)
        self.assertNotIn("sensitive", captured.exception.summary.lower())

    def test_remaining_attempt_deadline_is_passed_to_tesseract_timeout(self):
        captured_timeouts = []

        class FakeTesseractNotFoundError(RuntimeError):
            pass

        def image_to_data(*args, **kwargs):
            del args
            captured_timeouts.append(kwargs["timeout"])
            return {"text": [], "conf": []}

        fake = types.SimpleNamespace(
            Output=types.SimpleNamespace(DICT="dict"),
            TesseractNotFoundError=FakeTesseractNotFoundError,
            get_languages=lambda config: ["eng", "osd"],
            get_tesseract_version=lambda: TESSERACT_VERSION,
            image_to_data=image_to_data,
        )
        with (
            patch.dict(sys.modules, {"pytesseract": fake}),
            patch.object(LocalInvoiceExtractor, "_verify_model_hashes"),
            patch("app.local_extraction.time.monotonic", return_value=100.0),
        ):
            LocalInvoiceExtractor(timeout_seconds=30)._ocr_data(
                object(), deadline_monotonic=107.5
            )

        self.assertEqual([7.5], captured_timeouts)

    def test_deadline_crossed_between_pdf_pages_returns_safe_retryable_timeout(self):
        class PreflightDocument:
            def close(self):
                return None

        class NativeDocument:
            pages = [object(), object()]

            def __enter__(self):
                return self

            def __exit__(self, *args):
                del args
                return False

        fake_pdfium = types.ModuleType("pypdfium2")
        fake_pdfium.PdfDocument = lambda path: PreflightDocument()
        fake_pdfplumber = types.ModuleType("pdfplumber")
        fake_pdfplumber.open = lambda path: NativeDocument()
        fake_pdfdocument = types.ModuleType("pdfminer.pdfdocument")
        fake_pdfdocument.PDFPasswordIncorrect = type(
            "PDFPasswordIncorrect", (Exception,), {}
        )
        fake_pdfparser = types.ModuleType("pdfminer.pdfparser")
        fake_pdfparser.PDFSyntaxError = type("PDFSyntaxError", (Exception,), {})
        extractor = LocalInvoiceExtractor()
        native = _ExtractedPage(1, "Invoice", [], 0, None, False, False, "native")
        with (
            patch.dict(
                sys.modules,
                {
                    "pypdfium2": fake_pdfium,
                    "pdfplumber": fake_pdfplumber,
                    "pdfminer.pdfdocument": fake_pdfdocument,
                    "pdfminer.pdfparser": fake_pdfparser,
                },
            ),
            patch.object(extractor, "_native_page", return_value=native) as native_page,
            patch(
                "app.local_extraction.time.monotonic",
                side_effect=[0.0, 1.0, 2.0, 3.0, 11.0],
            ),
        ):
            with self.assertRaises(ExtractionError) as captured:
                extractor._extract_pdf(Path("two-pages.pdf"), deadline_monotonic=10.0)

        self.assertEqual("provider_timeout", captured.exception.code)
        self.assertTrue(captured.exception.retryable)
        self.assertEqual(1, native_page.call_count)
        self.assertNotIn("two-pages.pdf", captured.exception.summary)

class LocalProviderApiTests(unittest.TestCase):
    def test_safe_error_envelope_and_status_mapping(self):
        import httpx

        statuses = {
            "unsupported_media_type": 415,
            "provider_timeout": 504,
            "ocr_unavailable": 503,
            "corrupt_document": 422,
            "encrypted_document": 422,
        }

        async def exercise() -> None:
            with tempfile.TemporaryDirectory() as directory:
                document = Path(directory) / "source.pdf"
                document.write_bytes(b"safe test input")
                transport = httpx.ASGITransport(app=app, raise_app_exceptions=False)
                async with httpx.AsyncClient(
                    transport=transport, base_url="http://test"
                ) as client:
                    for code, expected_status in statuses.items():
                        with self.subTest(code=code):
                            provider = _FailingProvider(
                                ExtractionError(
                                    code,
                                    "The configured provider could not process this document.",
                                    retryable=code == "provider_timeout",
                                )
                            )
                            with (
                                patch("app.main.get_settings", return_value=types.SimpleNamespace(
                                    enable_legacy_extract=True,
                                    extraction_provider="mock",
                                )),
                                patch("app.main.get_extractor", return_value=provider),
                            ):
                                response = await client.post(
                                    "/extract",
                                    headers={"x-request-id": "request-123"},
                                    json={
                                        "filePath": str(document),
                                        "contentType": "application/pdf",
                                    },
                                )
                            self.assertEqual(expected_status, response.status_code)
                            payload = response.json()
                            validate_error_envelope(payload)
                            self.assertEqual(code, payload["error"]["code"])
                            self.assertEqual("request-123", payload["error"]["correlation_id"])

        asyncio.run(exercise())

    def test_error_correlation_id_is_bounded_and_safe(self):
        import httpx

        async def exercise() -> None:
            with tempfile.TemporaryDirectory() as directory:
                document = Path(directory) / "source.pdf"
                document.write_bytes(b"safe test input")
                provider = _FailingProvider(
                    ExtractionError(
                        "corrupt_document", "The PDF could not be parsed safely."
                    )
                )
                transport = httpx.ASGITransport(app=app, raise_app_exceptions=False)
                with (
                    patch("app.main.get_settings", return_value=types.SimpleNamespace(
                        enable_legacy_extract=True,
                        extraction_provider="mock",
                    )),
                    patch("app.main.get_extractor", return_value=provider),
                ):
                    async with httpx.AsyncClient(
                        transport=transport, base_url="http://test"
                    ) as client:
                        response = await client.post(
                            "/extract",
                            headers={"x-request-id": "unsafe id / source.pdf" * 10},
                            json={
                                "filePath": str(document),
                                "contentType": "application/pdf",
                            },
                        )
                correlation = response.json()["error"]["correlation_id"]
                self.assertRegex(correlation, r"^[A-Za-z0-9._:-]{1,100}$")
                self.assertNotIn("source.pdf", correlation)

        asyncio.run(exercise())

    def test_disabled_legacy_route_is_path_independent_404_before_file_access(self):
        import httpx

        async def exercise() -> None:
            transport = httpx.ASGITransport(app=app, raise_app_exceptions=False)
            with (
                patch("app.main.get_settings", return_value=types.SimpleNamespace(
                    enable_legacy_extract=False,
                    extraction_provider="local",
                )),
                patch("app.main.Path", side_effect=AssertionError("path must not be touched")),
            ):
                async with httpx.AsyncClient(
                    transport=transport, base_url="http://test"
                ) as client:
                    existing = await client.post(
                        "/extract",
                        json={"filePath": "existing.pdf", "contentType": "application/pdf"},
                    )
                    missing = await client.post(
                        "/extract",
                        json={"filePath": "missing.pdf", "contentType": "application/pdf"},
                    )
            self.assertEqual(404, existing.status_code)
            self.assertEqual(existing.json(), missing.json())

        asyncio.run(exercise())


class _FailingProvider:
    def __init__(self, error: ExtractionError) -> None:
        self.error = error

    def extract(self, path: Path, content_type: str):
        raise self.error


@unittest.skipUnless(
    RUN_RUNTIME,
    "set RUN_LOCAL_PROVIDER_INTEGRATION=1 in the pinned local-provider runtime",
)
class LocalProviderRuntimeFixtureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        # Docker Desktop can be CPU-throttled when all OCR fixtures run serially.
        # Keep the integration gate bounded while avoiding host-load flakiness.
        cls.extractor = LocalInvoiceExtractor(timeout_seconds=60)

    def test_native_pdf_matches_exact_expectation(self):
        result = self.extractor.extract(
            FIXTURE_ROOT / "generated" / "native-two-page-invoice.pdf",
            "application/pdf",
        )
        assert_exact_expectation(
            self, result, load_expectation("native-two-page-invoice.expected.json")
        )
        assert_duration_neutral_golden(self, result, "native-two-page-invoice")
        self.assertEqual(ADAPTER_VERSION, result.provider.adapter_version)
        self.assertEqual("pdfplumber", result.provider.model)
        self.assertEqual(
            f"{PDFPLUMBER_VERSION}/pdfminer-{PDFMINER_VERSION}",
            result.provider.model_version,
        )

    def test_scanned_pdf_matches_semantic_expectation(self):
        result = self.extractor.extract(
            FIXTURE_ROOT / "generated" / "scanned-two-page-invoice-300dpi.pdf",
            "application/pdf",
        )
        assert_semantic_expectation(
            self,
            result,
            load_expectation("scanned-two-page-invoice-300dpi.expected.json"),
        )
        assert_duration_neutral_golden(
            self, result, "scanned-two-page-invoice-300dpi"
        )
        self._assert_ocr_identity(result)

    def test_png_and_jpeg_match_semantic_expectations(self):
        cases = (
            ("invoice.png", "image/png", "invoice-png.expected.json"),
            ("invoice.jpg", "image/jpeg", "invoice-jpg.expected.json"),
        )
        for document, content_type, expected in cases:
            with self.subTest(document=document):
                result = self.extractor.extract(
                    FIXTURE_ROOT / "generated" / document, content_type
                )
                assert_semantic_expectation(self, result, load_expectation(expected))
                golden_id = "invoice-png" if document.endswith(".png") else "invoice-jpg"
                assert_duration_neutral_golden(
                    self, result, golden_id
                )
                self._assert_ocr_identity(result)

    def test_rotated_degraded_pdf_matches_partial_semantic_expectation(self):
        result = self.extractor.extract(
            FIXTURE_ROOT / "generated" / "rotated-low-quality-invoice.pdf",
            "application/pdf",
        )
        assert_semantic_expectation(
            self,
            result,
            load_expectation("rotated-low-quality-invoice.expected.json"),
        )
        assert_duration_neutral_golden(self, result, "rotated-low-quality-invoice")
        self._assert_ocr_identity(result)

    def test_corrupt_and_encrypted_fixtures_match_safe_expectations(self):
        cases = (
            ("corrupt.pdf", "application/pdf", "corrupt.expected.json"),
            ("encrypted.pdf", "application/pdf", "encrypted.expected.json"),
            ("unsupported.txt", "text/plain", "unsupported.expected.json"),
        )
        for document, content_type, expected in cases:
            with self.subTest(document=document):
                path = FIXTURE_ROOT / "generated" / document
                with self.assertRaises(ExtractionError) as captured:
                    self.extractor.extract(path, content_type)
                assert_error_expectation(
                    self, captured.exception, load_expectation(expected), path
                )

    def _assert_ocr_identity(self, result) -> None:
        self.assertEqual("local", result.provider.alias)
        self.assertEqual(ADAPTER_VERSION, result.provider.adapter_version)
        self.assertEqual("tesseract-eng-osd", result.provider.model)
        self.assertEqual(
            f"{TESSERACT_VERSION}/eng-osd-{ENG_MODEL_VERSION}",
            result.provider.model_version,
        )


if __name__ == "__main__":
    unittest.main()
