"""Generate and verify the deterministic Phase 2 synthetic fixture set."""

from __future__ import annotations

import argparse
import copy
import importlib.metadata
import io
import json
import shutil
import sys
from pathlib import Path
from typing import Any


SCRIPT_PATH = Path(__file__).resolve()
REPO_ROOT = SCRIPT_PATH.parents[1] if SCRIPT_PATH.parent.name == "tools" else Path.cwd()
API_ROOT = REPO_ROOT / "apps" / "api"
if API_ROOT.is_dir():
    sys.path.insert(0, str(API_ROOT))

from app.fixture_contract import (  # noqa: E402
    ContractValidationError,
    sha256_file,
    validate_expectation,
    validate_provider_fixture,
    validate_source_register,
)


GENERATOR_NAME = "phase2-synthetic-invoice"
GENERATOR_VERSION = "1.0.0"
SOURCE_PATH = SCRIPT_PATH.with_name("phase2_fixture_source.json")
if not SOURCE_PATH.is_file():
    SOURCE_PATH = REPO_ROOT / "tools" / "phase2_fixture_source.json"

REQUIRED_VERSIONS = {"Pillow": "9.4.0", "reportlab": "3.6.12"}
CANONICAL_IMAGE = (
    "python:3.11.15-slim-bookworm@"
    "sha256:d29f48a31a8b408ed19272ca1e7b10ebae13b240a27e862d3d4217c528e2e0c3"
)
CANONICAL_PLATFORM = "linux/amd64"
FONT_SHA256 = "c4c45690b345435b2cba52ecabe275f05e49b389b39fe68ad03afbb551288d3d"
FIXTURE_LICENSE = "CC0-1.0"
MAX_OUTPUT_BYTES = 25 * 1024 * 1024
NATIVE_TEXT_X = 54.0
NATIVE_TEXT_TOP_MARGIN = 72.0
NATIVE_FONT_NAME = "FixtureVera"
NATIVE_FONT_SIZE = 11.0
NATIVE_TEXT_LEADING = 18.0


def _tool_path(name: str) -> Path:
    repository_path = REPO_ROOT / "tools" / name
    if repository_path.is_file():
        return repository_path
    sibling = SCRIPT_PATH.with_name(name)
    if sibling.is_file():
        return sibling
    raise RuntimeError(f"Missing fixture tooling file: {name}")


def _registered_tool_path(relative: str) -> Path:
    repository_path = REPO_ROOT / Path(relative)
    if repository_path.is_file():
        return repository_path
    if relative == "apps/api/app/fixture_contract.py":
        container_path = SCRIPT_PATH.parent / "app" / "fixture_contract.py"
        if container_path.is_file():
            return container_path
    return _tool_path(Path(relative).name)


def _load_dependencies() -> tuple[Any, Any, Any, Any, Any, Path]:
    mismatches = []
    for package, required in REQUIRED_VERSIONS.items():
        actual = importlib.metadata.version(package)
        if actual != required:
            mismatches.append(f"{package}=={required} (found {actual})")
    if mismatches:
        raise RuntimeError(
            "Fixture dependency mismatch. Install tools/phase2-fixtures-requirements.txt: "
            + ", ".join(mismatches)
        )

    from PIL import Image, ImageDraw, ImageFilter, ImageFont
    from reportlab import __file__ as reportlab_file
    from reportlab.lib.pagesizes import A4
    from reportlab.lib.utils import ImageReader
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.ttfonts import TTFont
    from reportlab.pdfgen import canvas
    from reportlab.lib.pdfencrypt import StandardEncryption

    font_path = Path(reportlab_file).resolve().parent / "fonts" / "Vera.ttf"
    if sha256_file(font_path) != FONT_SHA256:
        raise RuntimeError("Pinned Bitstream Vera font hash does not match the fixture contract")
    if "FixtureVera" not in pdfmetrics.getRegisteredFontNames():
        pdfmetrics.registerFont(TTFont("FixtureVera", str(font_path)))
    return (
        (Image, ImageDraw, ImageFilter, ImageFont),
        (A4, ImageReader),
        canvas,
        StandardEncryption,
        pdfmetrics,
        font_path,
    )


def _json_bytes(payload: Any) -> bytes:
    serialized = json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    return serialized.encode("utf-8")


def _write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(_json_bytes(payload))


def _source() -> dict[str, Any]:
    payload = json.loads(SOURCE_PATH.read_text(encoding="utf-8"))
    if payload.get("source_schema_version") != "1.0":
        raise RuntimeError("Unsupported synthetic source schema")
    return payload


def _canonical_pages(invoice: dict[str, str]) -> list[str]:
    return [
        (
            "SYNTHETIC TEST DOCUMENT - NOT A REAL TAX INVOICE\n"
            "DESERT HORIZON TECHNOLOGY L.L.C.\n"
            "TAX INVOICE\n"
            f"Invoice Number: {invoice['invoice_number']}\n"
            f"Invoice Date: {invoice['invoice_date']}\n"
            f"Supplier TRN: {invoice['supplier_trn']}\n"
            f"Customer: {invoice['customer']}\n"
            f"Customer Email: {invoice['customer_email']}\n"
            f"Currency: {invoice['currency']}\n\n"
            "Description | Quantity | Unit Price | Line Total\n"
            "Document Intelligence Setup | 1 | 1,000.00 | 1,000.00\n"
        ),
        (
            "SYNTHETIC TEST DOCUMENT - NOT A REAL TAX INVOICE\n"
            "TAX INVOICE - PAGE 2\n"
            f"Invoice Number: {invoice['invoice_number']}\n\n"
            f"Subtotal: {invoice['subtotal']}\n"
            f"VAT 5%: {invoice['vat']}\n"
            f"Total: {invoice['total']}\n"
            f"Payment Terms: {invoice['payment_terms']}\n"
        ),
    ]


def _canonical_single_page(invoice: dict[str, str]) -> str:
    """Complete one-page invoice used by the PNG/JPEG and degraded OCR paths."""
    return (
        "SYNTHETIC TEST DOCUMENT - NOT A REAL TAX INVOICE\n"
        "DESERT HORIZON TECHNOLOGY L.L.C.\n"
        "TAX INVOICE\n"
        f"Invoice Number: {invoice['invoice_number']}\n"
        f"Invoice Date: {invoice['invoice_date']}\n"
        f"Supplier TRN: {invoice['supplier_trn']}\n"
        f"Customer: {invoice['customer']}\n"
        f"Customer Email: {invoice['customer_email']}\n"
        f"Currency: {invoice['currency']}\n\n"
        "Description | Quantity | Unit Price | Line Total\n"
        "Document Intelligence Setup | 1 | 1,000.00 | 1,000.00\n\n"
        f"Subtotal: {invoice['subtotal']}\n"
        f"VAT 5%: {invoice['vat']}\n"
        f"Total: {invoice['total']}\n"
        f"Payment Terms: {invoice['payment_terms']}\n"
    )


def _evidence(
    pages: list[str],
    page_number: int,
    line_prefix: str,
    quote: str,
    polygon: list[list[float]],
) -> dict[str, Any]:
    page = pages[page_number - 1]
    expected_line = f"{line_prefix}{quote}"
    if page.splitlines().count(expected_line) != 1:
        raise RuntimeError(f"Evidence line must occur exactly once: {expected_line!r}")
    character_start = page.index(expected_line) + len(line_prefix)
    start_utf8 = len(page[:character_start].encode("utf-8"))
    return {
        "coordinate_unit": "normalized",
        "end_utf8": start_utf8 + len(quote.encode("utf-8")),
        "page_number": page_number,
        "polygon": polygon,
        "quote": quote,
        "start_utf8": start_utf8,
    }


def _field(
    pages: list[str],
    page_number: int,
    line_prefix: str,
    key: str,
    value: str,
    value_type: str,
    normalized_value: str,
    polygon: list[list[float]],
) -> dict[str, Any]:
    return {
        "evidence": [_evidence(pages, page_number, line_prefix, value, polygon)],
        "key": key,
        "normalized_value": normalized_value,
        "occurrence": 0,
        "value": value,
        "value_type": value_type,
    }


def _native_value_polygon(
    pages: list[str],
    page_number: int,
    line_prefix: str,
    quote: str,
    pdfmetrics: Any,
    page_size: tuple[float, float],
) -> list[list[float]]:
    lines = pages[page_number - 1].splitlines()
    expected_line = f"{line_prefix}{quote}"
    matching = [(index, line) for index, line in enumerate(lines) if line == expected_line]
    if len(matching) != 1:
        raise RuntimeError(f"Native quote must occur on exactly one line: {quote!r}")
    line_index, line = matching[0]
    quote_start = line.index(quote)
    width, height = page_size
    x_start = NATIVE_TEXT_X + pdfmetrics.stringWidth(
        line[:quote_start], NATIVE_FONT_NAME, NATIVE_FONT_SIZE
    )
    x_end = x_start + pdfmetrics.stringWidth(quote, NATIVE_FONT_NAME, NATIVE_FONT_SIZE)
    baseline = height - NATIVE_TEXT_TOP_MARGIN - line_index * NATIVE_TEXT_LEADING
    ascent, descent = pdfmetrics.getAscentDescent(NATIVE_FONT_NAME, NATIVE_FONT_SIZE)
    top = height - (baseline + ascent)
    bottom = height - (baseline + descent)
    return [
        [round(x_start / width, 6), round(top / height, 6)],
        [round(x_end / width, 6), round(top / height, 6)],
        [round(x_end / width, 6), round(bottom / height, 6)],
        [round(x_start / width, 6), round(bottom / height, 6)],
    ]


def _exact_result_label(
    invoice: dict[str, str],
    pages: list[str],
    pdfmetrics: Any,
    page_size: tuple[float, float],
) -> dict[str, Any]:
    specifications = [
        (
            1,
            "Invoice Number: ",
            "invoice_number",
            invoice["invoice_number"],
            "String",
            invoice["invoice_number"],
        ),
        (
            1,
            "Invoice Date: ",
            "invoice_date",
            invoice["invoice_date"],
            "Date",
            invoice["invoice_date"],
        ),
        (
            1,
            "Supplier TRN: ",
            "supplier_trn",
            invoice["supplier_trn"],
            "Identifier",
            invoice["supplier_trn"],
        ),
        (1, "Currency: ", "currency", invoice["currency"], "Currency", invoice["currency"]),
        (2, "Subtotal: ", "subtotal", invoice["subtotal"], "Decimal", "1000.00"),
        (2, "VAT 5%: ", "vat", invoice["vat"], "Decimal", "50.00"),
        (2, "Total: ", "total", invoice["total"], "Decimal", "1050.00"),
        (
            2,
            "Payment Terms: ",
            "payment_terms",
            invoice["payment_terms"],
            "String",
            invoice["payment_terms"],
        ),
    ]
    fields = [
        _field(
            pages,
            page_number,
            line_prefix,
            key,
            value,
            value_type,
            normalized_value,
            _native_value_polygon(
                pages, page_number, line_prefix, value, pdfmetrics, page_size
            ),
        )
        for page_number, line_prefix, key, value, value_type, normalized_value in specifications
    ]
    return {
        "document_type": "invoice",
        "expectation_kind": "normalized_result",
        "fields": fields,
        "fixture_id": "native-two-page-invoice",
        "language": "en",
        "outcome": "Ready",
        "pages": [
            {"canonical_text": text, "page_number": index, "rotation_degrees": 0}
            for index, text in enumerate(pages, start=1)
        ],
        "provider_scope": "local",
        "schema_version": "2.0",
        "warnings": [],
    }


def _semantic_fields(invoice: dict[str, str]) -> list[dict[str, Any]]:
    return [
        {
            "key": "invoice_number",
            "normalized_value": invoice["invoice_number"],
            "occurrence": 0,
            "value": invoice["invoice_number"],
            "value_type": "String",
        },
        {
            "key": "total",
            "normalized_value": "1050.00",
            "occurrence": 0,
            "value": invoice["total"],
            "value_type": "Decimal",
        },
    ]


def _semantic_label(
    fixture_id: str,
    invoice: dict[str, str],
    *,
    page_count: int,
    outcome: str = "Ready",
    warning_codes: list[str] | None = None,
) -> dict[str, Any]:
    return {
        "document_type": "invoice",
        "expectation_kind": "semantic_result",
        "expected_page_count": page_count,
        "fields": _semantic_fields(invoice),
        "fixture_id": fixture_id,
        "language": "en",
        "outcome": outcome,
        "provider_scope": "local",
        "required_warning_codes": warning_codes or [],
        "schema_version": "2.0",
    }


def _error_label(fixture_id: str, code: str) -> dict[str, Any]:
    return {
        "error": {"code": code, "retryable": False},
        "expectation_kind": "safe_error",
        "fixture_id": fixture_id,
        "provider_scope": "local",
        "schema_version": "2.0",
    }


def _render_page_image(
    text: str,
    pil: tuple[Any, Any, Any, Any],
    font_path: Path,
    *,
    degraded: bool = False,
) -> Any:
    Image, ImageDraw, ImageFilter, ImageFont = pil
    size = (2480, 3508)
    image = Image.new("L", size, 255)
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype(str(font_path), 38)
    heading = ImageFont.truetype(str(font_path), 50)
    y = 180
    for index, line in enumerate(text.splitlines()):
        selected_font = heading if index < 2 and line else font
        draw.text((180, y), line, fill=20, font=selected_font)
        y += 82 if index < 2 else 65
    draw.rectangle((120, 120, size[0] - 120, size[1] - 120), outline=80, width=4)
    if degraded:
        for y_position in range(400, 3100, 173):
            shade = 175 + ((y_position // 173) % 4) * 12
            draw.line((100, y_position, size[0] - 100, y_position + 24), fill=shade, width=3)
        image = image.filter(ImageFilter.GaussianBlur(radius=1.35))
        image = image.rotate(90, expand=True, fillcolor=245)
    return image


def _pdf_metadata(document: Any, title: str) -> None:
    document.setAuthor("Synthetic Phase 2 fixture generator")
    document.setCreator(f"{GENERATOR_NAME}/{GENERATOR_VERSION}")
    document.setSubject("Synthetic English invoice; no client or personal data")
    document.setTitle(title)


def _write_native_pdf(path: Path, pages: list[str], dependencies: tuple[Any, ...]) -> None:
    (_, _, _, _), (a4, _), canvas, _, _, _ = dependencies
    document = canvas.Canvas(
        str(path), pagesize=a4, bottomup=1, pageCompression=1, invariant=1, pdfVersion=(1, 4)
    )
    _pdf_metadata(document, "Synthetic two-page native invoice")
    width, height = a4
    for page_text in pages:
        document.setStrokeColorRGB(0.3, 0.3, 0.3)
        document.rect(36, 36, width - 72, height - 72)
        text = document.beginText(NATIVE_TEXT_X, height - NATIVE_TEXT_TOP_MARGIN)
        text.setFont(NATIVE_FONT_NAME, NATIVE_FONT_SIZE)
        text.setLeading(NATIVE_TEXT_LEADING)
        for line in page_text.splitlines():
            text.textLine(line)
        document.drawText(text)
        document.showPage()
    document.save()


def _write_image_pdf(
    path: Path,
    images: list[Any],
    dependencies: tuple[Any, ...],
    title: str,
) -> None:
    (_, _, _, _), (a4, image_reader), canvas, _, _, _ = dependencies
    document = canvas.Canvas(
        str(path), pagesize=a4, bottomup=1, pageCompression=1, invariant=1, pdfVersion=(1, 4)
    )
    _pdf_metadata(document, title)
    width, height = a4
    for image in images:
        buffer = io.BytesIO()
        image.save(buffer, format="PNG", compress_level=9, optimize=False)
        buffer.seek(0)
        document.drawImage(image_reader(buffer), 0, 0, width=width, height=height, mask=None)
        document.showPage()
    document.save()


def _write_encrypted_pdf(path: Path, pages: list[str], dependencies: tuple[Any, ...]) -> None:
    (_, _, _, _), (a4, _), canvas, encryption_type, _, _ = dependencies
    encryption = encryption_type(
        userPassword="synthetic-fixture-password",
        ownerPassword="synthetic-fixture-owner",
        canPrint=0,
        canModify=0,
        canCopy=0,
        canAnnotate=0,
        strength=128,
    )
    document = canvas.Canvas(
        str(path),
        pagesize=a4,
        bottomup=1,
        pageCompression=1,
        invariant=1,
        encrypt=encryption,
        pdfVersion=(1, 4),
    )
    _pdf_metadata(document, "Synthetic encrypted invoice")
    text = document.beginText(54, a4[1] - 72)
    text.setFont("FixtureVera", 11)
    text.setLeading(18)
    for line in pages[0].splitlines():
        text.textLine(line)
    document.drawText(text)
    document.showPage()
    document.save()


def _write_direct_images(generated: Path, first_page: Any) -> None:
    first_page.save(
        generated / "invoice.png",
        format="PNG",
        compress_level=9,
        optimize=False,
        dpi=(300, 300),
    )
    rgb = first_page.convert("RGB")
    rgb.save(
        generated / "invoice.jpg",
        format="JPEG",
        quality=85,
        subsampling=0,
        optimize=False,
        progressive=False,
        dpi=(300, 300),
        exif=b"",
    )


def _provider_goldens(exact_label: dict[str, Any]) -> dict[str, dict[str, Any]]:
    result = {
        key: copy.deepcopy(exact_label[key])
        for key in (
            "schema_version",
            "outcome",
            "document_type",
            "language",
            "pages",
            "fields",
            "warnings",
        )
    }
    for field in result["fields"]:
        field["confidence"] = None
    result["provider"] = {
        "adapter_version": "fixture-adapter-1",
        "alias": "local",
        "model": "synthetic-native-parser",
        "model_version": "fixture-1",
    }
    result["metrics"] = {
        "cost_currency": None,
        "estimated_cost": None,
        "page_count": 2,
        "price_basis": None,
        "provider_duration_ms": 0,
    }
    valid = {
        "expected_validation": "accept_result",
        "fixture_schema_version": "1.0",
        "payload": result,
    }
    malformed = copy.deepcopy(valid)
    malformed["expected_validation"] = "reject"
    malformed["payload"]["fields"][0]["evidence"][0]["end_utf8"] += 1
    timeout = {
        "expected_validation": "accept_error",
        "fixture_schema_version": "1.0",
        "payload": {
            "error": {
                "code": "provider_timeout",
                "correlation_id": "synthetic-correlation-001",
                "retryable": True,
                "summary": "The configured provider did not finish within this attempt.",
            },
            "schema_version": "2.0",
        },
    }
    return {
        "malformed-result.json": malformed,
        "sanitized-native-ready.json": valid,
        "sanitized-timeout.json": timeout,
    }


def _label_schema() -> dict[str, Any]:
    return {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.invalid/schemas/phase2-fixture-expectation-2.0.json",
        "description": (
            "Structural schema for Phase 2 expectations. Semantic invariants are enforced by "
            "app.fixture_contract."
        ),
        "oneOf": [
            {"properties": {"expectation_kind": {"const": "normalized_result"}}},
            {"properties": {"expectation_kind": {"const": "semantic_result"}}},
            {"properties": {"expectation_kind": {"const": "safe_error"}}},
        ],
        "properties": {
            "expectation_kind": {
                "enum": ["normalized_result", "semantic_result", "safe_error"]
            },
            "fixture_id": {"minLength": 1, "type": "string"},
            "provider_scope": {"enum": ["local", "mock"]},
            "schema_version": {"const": "2.0"},
        },
        "required": ["schema_version", "expectation_kind", "fixture_id", "provider_scope"],
        "type": "object",
    }


def _readme() -> str:
    return """# Phase 2 synthetic fixtures

This set contains only fictional English invoice data generated from
`tools/phase2_fixture_source.json`. It is for deterministic contract and failure-path
tests, not for extraction-accuracy claims.

## Verify

```powershell
python tools/phase2_fixtures.py verify --root samples/phase-2
```

## Canonical regeneration

Build the digest-pinned generator image, then mount the repository at `/workspace`:

```powershell
docker build -f tools/phase2-fixtures.Dockerfile -t uae-di-phase2-fixtures .
docker run --rm -v "${PWD}:/workspace" uae-di-phase2-fixtures `
  generate --output /workspace/samples/phase-2
```

The generator uses fixed locale/timezone settings, exact Python dependencies, fixed PDF
metadata, fixed image encoder settings, and the hash-verified Bitstream Vera font shipped
by ReportLab. Runtime downloads are not performed.

The password for `generated/encrypted.pdf` is `synthetic-fixture-password`; it is public
test data, not a credential. `generated/unsupported.txt` is expected to be rejected by the
Phase 2 `local` provider even though Phase 1's `mock` provider supports text files.

Native PDFs embed Bitstream Vera. Its required redistribution notice is registered at
`licenses/bitstream-vera-license.txt`; those PDFs carry both CC0 synthetic content and the
Bitstream Vera font license.

Exact native-text labels include canonical pages and evidence. OCR-backed labels assert
only provider-independent semantic values until P2-T04 pins the OCR/model mapping.
`provider-responses/malformed-result.json` is intentionally invalid and must be rejected.
"""


def _artifact_entry(
    root: Path,
    path: Path,
    kind: str,
    media_type: str,
    purpose: str,
    license_name: str = FIXTURE_LICENSE,
) -> dict[str, Any]:
    relative = path.relative_to(root).as_posix()
    return {
        "bytes": path.stat().st_size,
        "kind": kind,
        "license": license_name,
        "media_type": media_type,
        "path": relative,
        "purpose": purpose,
        "sha256": sha256_file(path),
    }


def _safe_clean_output(output: Path) -> None:
    resolved = output.resolve()
    if resolved == Path(resolved.anchor) or len(resolved.parts) < 3:
        raise RuntimeError(f"Refusing to replace unsafe output path: {resolved}")
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)


def generate(output: Path) -> dict[str, Any]:
    dependencies = _load_dependencies()
    pil, (page_size, _), _, _, pdfmetrics, font_path = dependencies
    source = _source()
    invoice = source["invoice"]
    pages = _canonical_pages(invoice)
    single_page = _canonical_single_page(invoice)
    exact_label = _exact_result_label(invoice, pages, pdfmetrics, page_size)
    validate_expectation(exact_label)

    _safe_clean_output(output)
    generated = output / "generated"
    expected = output / "expected"
    provider_responses = output / "provider-responses"
    licenses = output / "licenses"
    generated.mkdir()
    expected.mkdir()
    provider_responses.mkdir()
    licenses.mkdir()

    images = [_render_page_image(text, pil, font_path) for text in pages]
    single_page_image = _render_page_image(single_page, pil, font_path)
    degraded_image = _render_page_image(single_page, pil, font_path, degraded=True)
    _write_native_pdf(generated / "native-two-page-invoice.pdf", pages, dependencies)
    _write_image_pdf(
        generated / "scanned-two-page-invoice-300dpi.pdf",
        images,
        dependencies,
        "Synthetic two-page scanned invoice at 300 DPI",
    )
    _write_image_pdf(
        generated / "rotated-low-quality-invoice.pdf",
        [degraded_image],
        dependencies,
        "Synthetic rotated and degraded invoice",
    )
    _write_encrypted_pdf(generated / "encrypted.pdf", pages, dependencies)
    _write_direct_images(generated, single_page_image)
    vera_license = font_path.parent / "bitstream-vera-license.txt"
    vera_notice = vera_license.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")
    (licenses / "bitstream-vera-license.txt").write_text(
        vera_notice, encoding="utf-8", newline="\n"
    )
    (generated / "corrupt.pdf").write_bytes(b"%PDF-1.7\n% synthetic corrupt fixture\n1 0 obj\n")
    (generated / "unsupported.txt").write_text(
        "Synthetic unsupported local-provider payload.\n", encoding="utf-8", newline="\n"
    )

    labels = {
        "corrupt.expected.json": _error_label("corrupt", "corrupt_document"),
        "encrypted.expected.json": _error_label("encrypted", "encrypted_document"),
        "invoice-jpg.expected.json": _semantic_label("invoice-jpg", invoice, page_count=1),
        "invoice-png.expected.json": _semantic_label("invoice-png", invoice, page_count=1),
        "native-two-page-invoice.expected.json": exact_label,
        "rotated-low-quality-invoice.expected.json": _semantic_label(
            "rotated-low-quality-invoice",
            invoice,
            page_count=1,
            outcome="Partial",
            warning_codes=["page_ocr_degraded"],
        ),
        "scanned-two-page-invoice-300dpi.expected.json": _semantic_label(
            "scanned-two-page-invoice-300dpi", invoice, page_count=2
        ),
        "unsupported.expected.json": _error_label("unsupported", "unsupported_media_type"),
    }
    for filename, label in labels.items():
        validate_expectation(label, filename)
        _write_json(expected / filename, label)
    _write_json(expected / "expectation-label.schema.json", _label_schema())

    for filename, golden in _provider_goldens(exact_label).items():
        validate_provider_fixture(golden, filename)
        _write_json(provider_responses / filename, golden)
    (output / "README.md").write_text(_readme(), encoding="utf-8", newline="\n")

    artifacts: list[dict[str, Any]] = []
    artifacts.append(
        _artifact_entry(
            output,
            output / "README.md",
            "documentation",
            "text/markdown",
            "Fixture scope, verification, and regeneration instructions",
        )
    )
    generated_metadata = {
        "corrupt.pdf": ("application/pdf", "Safe corrupt-document failure"),
        "encrypted.pdf": ("application/pdf", "Safe encrypted-document failure"),
        "invoice.jpg": ("image/jpeg", "Direct JPEG OCR input"),
        "invoice.png": ("image/png", "Direct PNG OCR input"),
        "native-two-page-invoice.pdf": ("application/pdf", "Native-text two-page path"),
        "rotated-low-quality-invoice.pdf": (
            "application/pdf",
            "Reviewed Partial path for rotated/degraded OCR",
        ),
        "scanned-two-page-invoice-300dpi.pdf": (
            "application/pdf",
            "Two-page 300-DPI OCR path",
        ),
        "unsupported.txt": ("text/plain", "Local-provider unsupported-media failure"),
    }
    embedded_font_pdfs = {"encrypted.pdf", "native-two-page-invoice.pdf"}
    for filename, (media_type, purpose) in sorted(generated_metadata.items()):
        artifacts.append(
            _artifact_entry(
                output,
                generated / filename,
                "document_fixture",
                media_type,
                purpose,
                (
                    "CC0-1.0 AND Bitstream-Vera"
                    if filename in embedded_font_pdfs
                    else FIXTURE_LICENSE
                ),
            )
        )
    artifacts.append(
        _artifact_entry(
            output,
            licenses / "bitstream-vera-license.txt",
            "third_party_notice",
            "text/plain",
            "Required notice for the embedded Bitstream Vera font",
            "Bitstream-Vera",
        )
    )
    for filename in sorted(labels):
        artifacts.append(
            _artifact_entry(
                output,
                expected / filename,
                "expectation",
                "application/json",
                "Versioned fixture expectation",
            )
        )
    artifacts.append(
        _artifact_entry(
            output,
            expected / "expectation-label.schema.json",
            "schema",
            "application/schema+json",
            "Structural expectation-label schema",
        )
    )
    for filename in sorted(_provider_goldens(exact_label)):
        artifacts.append(
            _artifact_entry(
                output,
                provider_responses / filename,
                "provider_fixture",
                "application/json",
                "Sanitized provider contract fixture",
            )
        )
    artifacts.sort(key=lambda artifact: artifact["path"])
    total_bytes = sum(artifact["bytes"] for artifact in artifacts)
    if total_bytes > MAX_OUTPUT_BYTES:
        raise RuntimeError("Generated fixture set exceeds the 25 MiB repository budget")

    register = {
        "artifacts": artifacts,
        "canonical_environment": {
            "container_image": CANONICAL_IMAGE,
            "locale": "C.UTF-8",
            "platform": CANONICAL_PLATFORM,
            "python": "3.11.15",
            "timezone": "UTC",
        },
        "dependencies": [
            {
                "license": "HPND",
                "name": "Pillow",
                "source": "https://pypi.org/project/Pillow/9.4.0/",
                "version": REQUIRED_VERSIONS["Pillow"],
            },
            {
                "license": "BSD-3-Clause",
                "name": "ReportLab",
                "source": "https://pypi.org/project/reportlab/3.6.12/",
                "version": REQUIRED_VERSIONS["reportlab"],
            },
            {
                "license": "Bitstream-Vera",
                "name": "Bitstream Vera Sans",
                "sha256": FONT_SHA256,
                "source": "reportlab/fonts/Vera.ttf",
                "version": "1.10",
            },
        ],
        "generator": {
            "name": GENERATOR_NAME,
            "source_sha256": sha256_file(SCRIPT_PATH),
            "version": GENERATOR_VERSION,
        },
        "register_schema_version": "1.0",
        "sources": [
            {
                "fictional": True,
                "license": source["license"],
                "path": "tools/phase2_fixture_source.json",
                "sha256": sha256_file(SOURCE_PATH),
                "source_id": source["fixture_set_id"],
            }
        ],
        "tooling": [
            {
                "license": "repository",
                "path": "apps/api/app/fixture_contract.py",
                "sha256": sha256_file(
                    _registered_tool_path("apps/api/app/fixture_contract.py")
                ),
            },
            {
                "license": "repository",
                "path": "tools/phase2-fixtures.Dockerfile",
                "sha256": sha256_file(_tool_path("phase2-fixtures.Dockerfile")),
            },
            {
                "license": "repository",
                "path": "tools/phase2-fixtures-requirements.txt",
                "sha256": sha256_file(_tool_path("phase2-fixtures-requirements.txt")),
            },
            {
                "license": "repository",
                "path": "tools/phase2_fixtures.py",
                "sha256": sha256_file(SCRIPT_PATH),
            },
        ],
        "total_bytes": total_bytes,
    }
    _write_json(output / "source-license-register.json", register)
    validate_source_register(output)
    return register


def verify(root: Path) -> dict[str, Any]:
    register = validate_source_register(root)
    expected_source = register["generator"]["source_sha256"]
    if expected_source != sha256_file(SCRIPT_PATH):
        raise ContractValidationError("register.generator.source_sha256: generator source changed")
    source = register["sources"][0]
    if source["sha256"] != sha256_file(SOURCE_PATH):
        raise ContractValidationError("register.sources[0].sha256: synthetic source changed")
    for index, tooling in enumerate(register["tooling"]):
        tool_path = _registered_tool_path(tooling["path"])
        if tooling["sha256"] != sha256_file(tool_path):
            raise ContractValidationError(
                f"register.tooling[{index}].sha256: fixture tooling changed"
            )
    return register


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    generate_parser = subparsers.add_parser("generate", help="replace an output fixture set")
    generate_parser.add_argument(
        "--output", type=Path, default=Path("samples/phase-2"), help="fixture-set output directory"
    )
    verify_parser = subparsers.add_parser(
        "verify", help="verify committed fixture hashes/contracts"
    )
    verify_parser.add_argument(
        "--root", type=Path, default=Path("samples/phase-2"), help="fixture-set root"
    )
    return parser


def main() -> int:
    arguments = _parser().parse_args()
    try:
        if arguments.command == "generate":
            register = generate(arguments.output)
            print(
                f"Generated {len(register['artifacts'])} artifacts "
                f"({register['total_bytes']} bytes) in {arguments.output}"
            )
        else:
            register = verify(arguments.root)
            print(
                f"Verified {len(register['artifacts'])} artifacts "
                f"({register['total_bytes']} bytes) in {arguments.root}"
            )
    except (ContractValidationError, RuntimeError, OSError, ValueError) as error:
        print(f"Fixture validation failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
