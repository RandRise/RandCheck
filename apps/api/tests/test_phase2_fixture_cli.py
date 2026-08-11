import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from PIL import Image
from reportlab import __file__ as reportlab_module
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

from app.fixture_contract import validate_source_register


REPO_ROOT = Path(__file__).resolve().parents[3]
TOOL = REPO_ROOT / "tools" / "phase2_fixtures.py"
COMMITTED_ROOT = REPO_ROOT / "samples" / "phase-2"

DOCUMENT_EXPECTATIONS = {
    "generated/corrupt.pdf": "expected/corrupt.expected.json",
    "generated/encrypted.pdf": "expected/encrypted.expected.json",
    "generated/invoice.jpg": "expected/invoice-jpg.expected.json",
    "generated/invoice.png": "expected/invoice-png.expected.json",
    "generated/native-two-page-invoice.pdf":
        "expected/native-two-page-invoice.expected.json",
    "generated/rotated-low-quality-invoice.pdf":
        "expected/rotated-low-quality-invoice.expected.json",
    "generated/scanned-two-page-invoice-300dpi.pdf":
        "expected/scanned-two-page-invoice-300dpi.expected.json",
    "generated/unsupported.txt": "expected/unsupported.expected.json",
}


def run_cli(*arguments: str) -> subprocess.CompletedProcess[str]:
    environment = os.environ.copy()
    environment.update({"PYTHONHASHSEED": "0", "TZ": "UTC"})
    return subprocess.run(
        [sys.executable, str(TOOL), *arguments],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        timeout=120,
        check=False,
    )


class Phase2FixtureCliTests(unittest.TestCase):
    def test_generate_twice_is_byte_for_byte_deterministic_and_verifiable(self):
        with tempfile.TemporaryDirectory() as directory:
            first = Path(directory) / "first"
            second = Path(directory) / "second"
            first_result = run_cli("generate", "--output", str(first))
            second_result = run_cli("generate", "--output", str(second))
            self.assertEqual(0, first_result.returncode, first_result.stderr)
            self.assertEqual(0, second_result.returncode, second_result.stderr)

            first_register = validate_source_register(first)
            second_register = validate_source_register(second)
            self.assertEqual(first_register, second_register)
            first_files = sorted(
                path.relative_to(first).as_posix()
                for path in first.rglob("*") if path.is_file()
            )
            second_files = sorted(
                path.relative_to(second).as_posix()
                for path in second.rglob("*") if path.is_file()
            )
            self.assertEqual(first_files, second_files)
            for relative in first_files:
                self.assertEqual(
                    (first / relative).read_bytes(),
                    (second / relative).read_bytes(),
                    relative,
                )

            verification = run_cli("verify", "--root", str(first))
            self.assertEqual(0, verification.returncode, verification.stderr)
            self.assertIn("Verified", verification.stdout)
            self.assertIn("artifacts", verification.stdout)

    def test_committed_fixture_set_verifies_and_register_covers_every_required_case(self):
        verification = run_cli("verify", "--root", str(COMMITTED_ROOT))
        self.assertEqual(0, verification.returncode, verification.stderr)
        register = validate_source_register(COMMITTED_ROOT)
        artifacts = {item["path"]: item for item in register["artifacts"]}

        self.assertGreaterEqual(len(artifacts), 20)
        self.assertEqual(set(DOCUMENT_EXPECTATIONS), {
            path for path, item in artifacts.items() if item["kind"] == "document_fixture"
        })
        for document, expectation in DOCUMENT_EXPECTATIONS.items():
            self.assertIn(expectation, artifacts, document)
            self.assertEqual("expectation", artifacts[expectation]["kind"])
        self.assertEqual(
            3,
            sum(item["kind"] == "provider_fixture" for item in artifacts.values()),
        )
        self.assertEqual(
            "schema", artifacts["expected/expectation-label.schema.json"]["kind"]
        )
        self.assertTrue(all(item["license"] for item in artifacts.values()))
        self.assertTrue(all(source["fictional"] for source in register["sources"]))
        self.assertTrue(all(source["license"] for source in register["sources"]))
        self.assertTrue(all(dependency["license"] for dependency in register["dependencies"]))

    def test_font_notice_composite_license_and_synthetic_identity_are_explicit(self):
        register = validate_source_register(COMMITTED_ROOT)
        artifacts = {item["path"]: item for item in register["artifacts"]}
        notice = artifacts["licenses/bitstream-vera-license.txt"]
        self.assertEqual("third_party_notice", notice["kind"])
        self.assertEqual("Bitstream-Vera", notice["license"])
        self.assertGreater(notice["bytes"], 0)
        notice_bytes = (
            COMMITTED_ROOT / "licenses" / "bitstream-vera-license.txt"
        ).read_bytes()
        self.assertIn(b"\n", notice_bytes)
        self.assertNotIn(b"\r", notice_bytes)
        for pdf in (
            "generated/native-two-page-invoice.pdf",
            "generated/encrypted.pdf",
        ):
            self.assertEqual("CC0-1.0 AND Bitstream-Vera", artifacts[pdf]["license"])

        native = json.loads(
            (
                COMMITTED_ROOT
                / "expected"
                / "native-two-page-invoice.expected.json"
            ).read_text(encoding="utf-8")
        )
        watermark = "SYNTHETIC TEST DOCUMENT - NOT A REAL TAX INVOICE"
        self.assertTrue(native["pages"])
        self.assertTrue(
            all(watermark in page["canonical_text"] for page in native["pages"])
        )
        supplier_trn = next(
            field for field in native["fields"] if field["key"] == "supplier_trn"
        )
        self.assertEqual("000000000000000", supplier_trn["value"])
        self.assertEqual("000000000000000", supplier_trn["normalized_value"])

    def test_native_evidence_polygons_match_rendered_vera_value_bounds(self):
        native = json.loads(
            (
                COMMITTED_ROOT
                / "expected"
                / "native-two-page-invoice.expected.json"
            ).read_text(encoding="utf-8")
        )
        pages = {
            page["page_number"]: page["canonical_text"] for page in native["pages"]
        }
        expected_prefixes = {
            "invoice_number": "Invoice Number: ",
            "invoice_date": "Invoice Date: ",
            "supplier_trn": "Supplier TRN: ",
            "currency": "Currency: ",
            "subtotal": "Subtotal: ",
            "vat": "VAT 5%: ",
            "total": "Total: ",
            "payment_terms": "Payment Terms: ",
        }
        self.assertEqual(set(expected_prefixes), {field["key"] for field in native["fields"]})

        geometry_font = "FixtureVeraGeometryTest"
        if geometry_font not in pdfmetrics.getRegisteredFontNames():
            vera_path = Path(reportlab_module).resolve().parent / "fonts" / "Vera.ttf"
            pdfmetrics.registerFont(TTFont(geometry_font, str(vera_path)))
        width, height = A4
        font_size = 11.0
        ascent, descent = pdfmetrics.getAscentDescent(geometry_font, font_size)

        for field in native["fields"]:
            with self.subTest(field=field["key"]):
                self.assertEqual(1, len(field["evidence"]))
                evidence = field["evidence"][0]
                quote = evidence["quote"]
                self.assertEqual(field["value"], quote)
                page = pages[evidence["page_number"]]
                page_bytes = page.encode("utf-8")
                selected = page_bytes[
                    evidence["start_utf8"]:evidence["end_utf8"]
                ].decode("utf-8")
                self.assertEqual(quote, selected)

                characters_before = page_bytes[:evidence["start_utf8"]].decode("utf-8")
                line_index = characters_before.count("\n")
                lines = page.splitlines()
                line = lines[line_index]
                prefix = expected_prefixes[field["key"]]
                self.assertEqual(f"{prefix}{quote}", line)
                self.assertEqual(1, lines.count(line))

                x_start = 54.0 + pdfmetrics.stringWidth(
                    prefix, geometry_font, font_size
                )
                x_end = x_start + pdfmetrics.stringWidth(
                    quote, geometry_font, font_size
                )
                baseline = height - 72.0 - line_index * 18.0
                top = height - (baseline + ascent)
                bottom = height - (baseline + descent)
                expected_polygon = [
                    [round(x_start / width, 6), round(top / height, 6)],
                    [round(x_end / width, 6), round(top / height, 6)],
                    [round(x_end / width, 6), round(bottom / height, 6)],
                    [round(x_start / width, 6), round(bottom / height, 6)],
                ]
                self.assertEqual(expected_polygon, evidence["polygon"])

        vat = next(field for field in native["fields"] if field["key"] == "vat")
        total = next(field for field in native["fields"] if field["key"] == "total")
        self.assertEqual("50.00", vat["evidence"][0]["quote"])
        self.assertEqual("1,050.00", total["evidence"][0]["quote"])
        self.assertNotEqual(
            vat["evidence"][0]["start_utf8"], total["evidence"][0]["start_utf8"]
        )

    def test_committed_images_have_fixed_dimensions_and_no_exif_or_gps_metadata(self):
        for filename, expected_format in (("invoice.png", "PNG"), ("invoice.jpg", "JPEG")):
            with self.subTest(filename=filename):
                with Image.open(COMMITTED_ROOT / "generated" / filename) as image:
                    self.assertEqual(expected_format, image.format)
                    self.assertEqual((2480, 3508), image.size)
                    self.assertEqual({}, dict(image.getexif()))
                    self.assertNotIn("gps", {key.lower() for key in image.info})
                    dpi = image.info.get("dpi")
                    self.assertIsNotNone(dpi)
                    self.assertAlmostEqual(300, dpi[0], delta=1)
                    self.assertAlmostEqual(300, dpi[1], delta=1)

    def test_committed_pdfs_contain_no_active_content_markers_and_encryption_is_explicit(self):
        forbidden = (
            b"/JavaScript",
            b"/JS ",
            b"/JS/",
            b"/JS\r",
            b"/JS\n",
            b"/Launch",
            b"/EmbeddedFile",
            b"/Filespec",
        )
        pdfs = sorted((COMMITTED_ROOT / "generated").glob("*.pdf"))
        self.assertEqual(5, len(pdfs))
        for path in pdfs:
            with self.subTest(path=path.name):
                content = path.read_bytes()
                self.assertTrue(content.startswith(b"%PDF-"))
                for marker in forbidden:
                    self.assertNotIn(marker, content)
        self.assertIn(b"/Encrypt", (COMMITTED_ROOT / "generated" / "encrypted.pdf").read_bytes())
        corrupt = (COMMITTED_ROOT / "generated" / "corrupt.pdf").read_bytes()
        self.assertNotIn(b"%%EOF", corrupt)

    def test_failure_fixtures_have_explicit_safe_local_provider_expectations(self):
        expected_codes = {
            "corrupt.expected.json": "corrupt_document",
            "encrypted.expected.json": "encrypted_document",
            "unsupported.expected.json": "unsupported_media_type",
        }
        for filename, code in expected_codes.items():
            with self.subTest(filename=filename):
                payload = json.loads(
                    (COMMITTED_ROOT / "expected" / filename).read_text(encoding="utf-8")
                )
                self.assertEqual("2.0", payload["schema_version"])
                self.assertEqual("safe_error", payload["expectation_kind"])
                self.assertEqual("local", payload["provider_scope"])
                self.assertEqual(code, payload["error"]["code"])
                self.assertFalse(payload["error"]["retryable"])

        unsupported = (COMMITTED_ROOT / "generated" / "unsupported.txt").read_text(
            encoding="utf-8"
        )
        self.assertEqual("Synthetic unsupported local-provider payload.\n", unsupported)


if __name__ == "__main__":
    unittest.main()
