import json
from pathlib import Path
import unittest
from unittest.mock import patch

from app.arabic_text import matching_view, normalize_value, script_language
from app.extraction import ExtractionError
from app.local_extraction import LocalInvoiceExtractor, _ExtractedPage, _TextLine

ROOT = Path(__file__).resolve().parents[3]


class ArabicEvidenceTests(unittest.TestCase):
    def test_versioned_language_cases_keep_exact_source_evidence(self):
        data = json.loads((ROOT / "samples/phase-7/language-v0.1.0/cases.json").read_text(encoding="utf-8"))
        for case in data["cases"]:
            with self.subTest(case=case["id"]):
                lines = [_TextLine(line, [None] * len(line), [None] * len(line))
                         for line in case["text"].splitlines()]
                page = _ExtractedPage(1, case["text"], lines, 0, None, False)
                fields = LocalInvoiceExtractor._fields([page])
                actual = {f.key: f for f in fields}
                self.assertEqual(case["language"], script_language(page.canonical_text))
                for key, value in case["expected"].items():
                    self.assertEqual(value, actual[key].normalized_value, key)
                for key in case.get("absent", []):
                    self.assertNotIn(key, actual)
                for field in fields:
                    for evidence in field.evidence:
                        selected = page.canonical_text.encode()[evidence.start_utf8:evidence.end_utf8].decode()
                        self.assertEqual(selected, evidence.quote)
                        self.assertEqual(selected, field.value)

    def test_normalization_never_changes_names_or_string_identifiers(self):
        for kind in ("String", "Name"):
            self.assertEqual("أحمد ٠٠١", normalize_value("أحمد ٠٠١", kind))
        self.assertEqual("000123", normalize_value("٠٠٠۱۲۳", "Identifier"))
        self.assertEqual("1050.50", normalize_value("١٬٠٥٠٫٥٠", "Decimal"))

    def test_ocr_boundary_direction_marks_do_not_become_field_values(self):
        for text, prefix, expected in (
            ("Invoice Number: INV-007\u200f", "Invoice Number", "INV-007"),
            ("الإجمالي: \u200e210.50\u200f", "الإجمالي", "210.50"),
            ("شروط الدفع: خلال 30 يوماً\u200f", "شروط الدفع", "خلال 30 يوماً"),
        ):
            with self.subTest(text=text):
                value, start, end = LocalInvoiceExtractor._match_line(text, (prefix,))
                self.assertEqual(expected, value)
                self.assertEqual(value, text[start:end])
        self.assertIsNone(LocalInvoiceExtractor._match_line("الإجمالي: \u200e", ("الإجمالي",)))

    def test_compatibility_expansion_and_marks_map_back_to_whole_source_characters(self):
        source = "ﻻ إِجـمالي"
        view = matching_view(source)
        self.assertEqual("لا اجمالي", view.text)
        self.assertEqual((0, 1), view.source_span(0, 2))
        start, end = view.source_span(3, len(view.text))
        self.assertEqual("إِجـمالي", source[start:end])
        with self.assertRaises(ValueError):
            view.source_span(0, 0)

    def test_ocr_logical_order_and_boxes_survive_decreasing_x_coordinates(self):
        data = {"text": ["الإجمالي:", "١٠٥٫٠٠", "AED"], "block_num": [1]*3,
                "par_num": [1]*3, "line_num": [1]*3, "left": [700, 500, 400],
                "top": [10]*3, "width": [100]*3, "height": [30]*3, "conf": [95]*3}
        page = LocalInvoiceExtractor._page_from_ocr(data, 1, 0, (1000, 1000))
        self.assertEqual("الإجمالي: ١٠٥٫٠٠ AED\n", page.canonical_text)
        self.assertEqual((.5, .01, .6, .04), page.lines[0].character_boxes[10])

    def test_language_configuration_is_explicit_and_missing_arabic_model_fails_closed(self):
        for lang in ("eng", "ara", "ara+eng", "eng+ara"):
            self.assertEqual(lang, LocalInvoiceExtractor(language=lang).language)
        with self.assertRaises(ValueError):
            LocalInvoiceExtractor(language="ara --psm 0")
        with patch("app.local_extraction.Path.read_bytes", return_value=b"wrong model"):
            with self.assertRaises(ExtractionError):
                LocalInvoiceExtractor._verify_model_hashes("ara+eng")

    def test_arabic_ocr_always_requires_source_review_even_with_high_confidence(self):
        line = "رقم الفاتورة: ٠١ | الإجمالي: ١٠٥٫٠٠"
        page = _ExtractedPage(1, line + "\n", [_TextLine(line, [None]*len(line), [1.0]*len(line))],
                              0, 1.0, False, source="ocr")
        warnings = LocalInvoiceExtractor._warnings([page], LocalInvoiceExtractor._fields([page]))
        warning = next(w for w in warnings if w.code == "arabic_ocr_requires_review")
        self.assertEqual("degraded", warning.severity)

    def test_diacritic_due_date_preserves_raw_digits_and_source_offsets(self):
        line = "تاريخ الاسْتِحقاق: ٢٠٢٨-٠٢-٢٩"
        page = _ExtractedPage(1, line + "\n", [_TextLine(line, [None]*len(line), [None]*len(line))],
                              0, None, False)
        field = next(f for f in LocalInvoiceExtractor._fields([page]) if f.key == "due_date")
        self.assertEqual("2028-02-29", field.normalized_value)
        self.assertEqual("٢٠٢٨-٠٢-٢٩", field.value)
        evidence = field.evidence[0]
        self.assertEqual(field.value, page.canonical_text.encode()[evidence.start_utf8:evidence.end_utf8].decode())
