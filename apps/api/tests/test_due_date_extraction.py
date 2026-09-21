import os
import unittest
from pathlib import Path

from app.local_extraction import LocalInvoiceExtractor, _ExtractedPage, _TextLine


def page(number, text):
    lines = [_TextLine(line, [None] * len(line), [None] * len(line)) for line in text.splitlines()]
    return _ExtractedPage(number, text, lines, 0, None, False)


class DueDateExtractionTests(unittest.TestCase):
    def test_explicit_date_stays_distinct_from_invoice_date_terms_and_amount(self):
        source = page(1, "é Invoice Date: 2026-08-01\nPayment Due Date: 2026-08-31 | Total: 125.00\nTerms: Net 30")
        fields = {f.key: f for f in LocalInvoiceExtractor._fields([source])}
        self.assertEqual("2026-08-01", fields["invoice_date"].value)
        self.assertEqual("125.00", fields["total"].value)
        due = fields["due_date"]
        self.assertEqual(("Date", "2026-08-31", "2026-08-31"), (due.value_type, due.value, due.normalized_value))
        evidence = due.evidence[0]
        self.assertEqual(evidence.quote, source.canonical_text.encode()[evidence.start_utf8:evidence.end_utf8].decode())

    def test_terms_or_amount_do_not_manufacture_a_due_date(self):
        for text in ("Invoice Date: 2026-08-01\nTerms: Net 30", "Amount due: 100.00", "Payment date: 2026-08-31"):
            with self.subTest(text=text):
                self.assertNotIn("due_date", {f.key for f in LocalInvoiceExtractor._fields([page(1, text)])})

    def test_conflicts_on_same_line_or_across_pages_are_not_first_match(self):
        cases = [[page(1, "Due Date: 2026-08-31 | Due Date: 2026-09-15")],
                 [page(1, "Due Date: 2026-08-31"), page(2, "Payment Due Date: 2026-09-15")]]
        for pages in cases:
            with self.subTest(pages=pages):
                fields = LocalInvoiceExtractor._fields(pages)
                self.assertNotIn("due_date", {f.key for f in fields})
                warning = next(w for w in LocalInvoiceExtractor._warnings(pages, fields) if w.code == "due_date_conflicting")
                self.assertEqual(("due_date", 0, "degraded"), (warning.field_key, warning.occurrence, warning.severity))

    def test_duplicate_consistent_dates_keep_both_source_references(self):
        pages = [page(1, "Due Date: 2026-08-31"), page(2, "Due Date: 2026-08-31")]
        due = next(f for f in LocalInvoiceExtractor._fields(pages) if f.key == "due_date")
        self.assertEqual([1, 2], [e.page_number for e in due.evidence])

    def test_invalid_empty_or_ambiguous_dates_require_review_even_beside_valid_date(self):
        for value in ("2026-02-30", "08/09/2026", "", "Net 30"):
            for prefix in ("", "Due Date: 2026-08-31\n"):
                with self.subTest(value=value, prefix=prefix):
                    pages = [page(1, prefix + "Due Date: " + value)]
                    fields = LocalInvoiceExtractor._fields(pages)
                    self.assertNotIn("due_date", {f.key for f in fields})
                    self.assertIn("due_date_requires_review", {w.code for w in LocalInvoiceExtractor._warnings(pages, fields)})

    def test_leap_day_is_validated(self):
        fields = LocalInvoiceExtractor._fields([page(1, "Due Date: 2028-02-29")])
        self.assertEqual("2028-02-29", next(f.value for f in fields if f.key == "due_date"))

    @unittest.skipUnless(os.getenv("RUN_DUE_DATE_NATIVE") == "1", "Requires the pinned native-PDF runtime")
    def test_existing_synthetic_invoice_yields_due_date_with_exact_page_evidence(self):
        root = Path(__file__).resolve().parents[3]
        result = LocalInvoiceExtractor().extract(root / "output/pdf/uae-invoice-development-v0.2.0/fta-standard-services-native.pdf", "application/pdf")
        due = next(f for f in result.fields if f.key == "due_date")
        self.assertEqual("2026-08-31", due.normalized_value)
        for evidence in due.evidence:
            text = next(p.canonical_text for p in result.pages if p.page_number == evidence.page_number)
            self.assertEqual(evidence.quote, text.encode()[evidence.start_utf8:evidence.end_utf8].decode())
