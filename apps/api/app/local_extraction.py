from __future__ import annotations

import math
import os
import re
import statistics
import time
import unicodedata
import hashlib
from dataclasses import asdict, replace
from dataclasses import dataclass
from datetime import date
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path
from typing import Any, Iterable

from .extraction import (
    EvidenceResult,
    ExtractionError,
    ExtractionResult,
    FieldResult,
    MetricsResult,
    PageResult,
    ProviderResult,
    WarningResult,
)
from .fixture_contract import ContractValidationError, validate_normalized_result
from .arabic_text import has_arabic, matching_view, normalize_value, normalize_supplier_number, script_language


ADAPTER_VERSION = "local-1.3.0"
PDFPLUMBER_VERSION = "0.11.10"
PDFMINER_VERSION = "20260107"
PYPDFIUM_VERSION = "5.12.1"
PYTESSERACT_VERSION = "0.3.13"
# Pillow 13 fixes decoder vulnerabilities; reject older runtimes before reading a source.
PILLOW_VERSION = "13.0.0"
TESSERACT_VERSION = "5.3.0"
ENG_MODEL_VERSION = "4.1.0"
ENG_MODEL_SHA256 = "7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2"
ARA_MODEL_SHA256 = "e3206d3dc87fd50c24a0fb9f01838615911d25168f4e64415244b67d2bb3e729"
ARA_BEST_MODEL_SHA256 = "ab9d157d8e38ca00e7e39c7d5363a5239e053f5b0dbdb3167dde9d8124335896"
OSD_MODEL_SHA256 = "9cf5d576fcc47564f11265841e5ca839001e7e6f38ff7f7aacf46d15a96b00ff"
MAX_PAGES = 50


@dataclass(frozen=True)
class _Label:
    key: str
    prefixes: tuple[str, ...]
    value_type: str


LABELS = (
    _Label("invoice_number", ("Invoice Number", "رقم الفاتورة"), "String"),
    _Label("quotation_number", ("Quotation Number",), "String"),
    _Label("purchase_order_number", ("Purchase Order Number", "PO Number"), "String"),
    _Label("invoice_date", ("Invoice Date", "تاريخ الفاتورة"), "Date"),
    _Label("due_date", ("Payment Due Date", "Due Date", "تاريخ الاستحقاق"), "Date"),
    _Label("supplier_trn", ("Supplier TRN", "TRN", "الرقم الضريبي للمورد", "الرقم الضريبي"), "Identifier"),
    _Label("currency", ("Currency", "العملة"), "Currency"),
    _Label("subtotal", ("Subtotal", "المجموع الفرعي"), "Decimal"),
    _Label("vat", ("VAT charged", "VAT 5%", "VAT", "ضريبة القيمة المضافة"), "Decimal"),
    _Label("total", ("Gross payable", "Total", "الإجمالي", "المجموع الكلي"), "Decimal"),
    _Label("payment_terms", ("Payment Terms", "Terms", "شروط الدفع"), "String"),
)

LINE_LABELS = (
    _Label("line_item_code", ("Item Code", "رمز الصنف"), "String"),
    _Label("line_description", ("Description", "الوصف", "وصف الصنف"), "String"),
    _Label("line_quantity", ("Quantity", "الكمية"), "Decimal"),
    _Label("line_unit_price", ("Unit Price", "سعر الوحدة"), "Decimal"),
    _Label("line_tax", ("Tax", "الضريبة", "ضريبة القيمة المضافة"), "Decimal"),
    _Label("line_total", ("Total", "الإجمالي"), "Decimal"),
)


@dataclass
class _TextLine:
    text: str
    character_boxes: list[tuple[float, float, float, float] | None]
    confidences: list[float | None]


@dataclass
class _ExtractedPage:
    page_number: int
    canonical_text: str
    lines: list[_TextLine]
    rotation_degrees: int
    ocr_confidence: float | None
    degraded: bool
    orientation_uncertain: bool = False
    source: str = "native"


class LocalInvoiceExtractor:
    """Local procurement-document adapter with opt-in Arabic OCR."""

    name = "local-procurement-v1"

    def __init__(
        self,
        *,
        language: str = "eng",
        timeout_seconds: float = 30.0,
        render_dpi: int = 300,
        arabic_model: str = "fast",
        layout_profile: str = "block",
        input_profile: str = "legacy",
    ) -> None:
        if language not in {"eng", "ara", "ara+eng", "eng+ara"}:
            raise ValueError("OCR_LANGUAGE must be eng, ara, ara+eng or eng+ara")
        if not 1 <= timeout_seconds <= 120:
            raise ValueError("OCR_TIMEOUT_SECONDS must be between 1 and 120")
        if not 150 <= render_dpi <= 400:
            raise ValueError("OCR_RENDER_DPI must be between 150 and 400")
        self.language = language
        self.timeout_seconds = timeout_seconds
        self.render_dpi = render_dpi
        if arabic_model not in {"fast", "best"}:
            raise ValueError("OCR_ARABIC_MODEL must be fast or best")
        if layout_profile not in {"block", "lines-v1"}:
            raise ValueError("OCR_LAYOUT_PROFILE must be block or lines-v1")
        if layout_profile == "lines-v1" and language not in {"eng+ara", "ara+eng"}:
            raise ValueError("lines-v1 requires explicit mixed Arabic/English OCR")
        self.arabic_model = arabic_model
        self.layout_profile = layout_profile
        if input_profile not in {"legacy", "supplier-invoice-v1"}:
            raise ValueError("LOCAL_INPUT_PROFILE must be legacy or supplier-invoice-v1")
        self.input_profile = input_profile

    @classmethod
    def from_environment(cls) -> LocalInvoiceExtractor:
        try:
            timeout = float(os.getenv("OCR_TIMEOUT_SECONDS", "30"))
            dpi = int(os.getenv("OCR_RENDER_DPI", "300"))
        except ValueError as error:
            raise ValueError("OCR timeout and DPI settings must be numeric") from error
        return cls(
            language=os.getenv("OCR_LANGUAGE", "eng"),
            timeout_seconds=timeout,
            render_dpi=dpi,
            arabic_model=os.getenv("OCR_ARABIC_MODEL", "fast"),
            layout_profile=os.getenv("OCR_LAYOUT_PROFILE", "block"),
            input_profile=os.getenv("LOCAL_INPUT_PROFILE", "legacy"),
        )

    def extract(
        self, path: Path, content_type: str, deadline_monotonic: float | None = None
    ) -> ExtractionResult:
        started = time.perf_counter()
        self._remaining(deadline_monotonic)
        self._verify_python_runtime()
        self._remaining(deadline_monotonic)
        media_type = self._detect_media_type(path, content_type)
        if media_type == "application/pdf":
            pages, used_ocr = self._extract_pdf(path, deadline_monotonic)
        else:
            self._remaining(deadline_monotonic)
            image, exif_rotation = self._open_image(path)
            self._remaining(deadline_monotonic)
            pages = [self._ocr_image(
                image,
                1,
                applied_rotation=exif_rotation,
                deadline_monotonic=deadline_monotonic,
            )]
            used_ocr = True

        self._remaining(deadline_monotonic)
        supplier = self.input_profile == "supplier-invoice-v1"
        document_type = self._supplier_document_type(pages) if supplier else self._document_type(pages)
        if supplier:
            fields, profile_warnings = self._supplier_fields(pages)
        else:
            fields = self._fields(pages)
            profile_warnings = []
        if not any(page.canonical_text.strip() for page in pages):
            raise ExtractionError(
                "document_unreadable",
                "The document contains no text that the local provider can read.",
            )
        if not fields:
            raise ExtractionError(
                "unsupported_document",
                "The readable document is not a supported procurement document.",
            )
        warnings = self._warnings(pages, fields, document_type, check_due_date=not supplier)
        warnings.extend(profile_warnings)
        if supplier:
            warnings.extend(self._supplier_coverage(fields, document_type))
            unique = {(warning.code, warning.severity, warning.page_number,
                       warning.field_key, warning.occurrence): warning for warning in warnings}
            warnings = sorted(unique.values(), key=lambda warning: (
                0 if warning.severity == "info" else 1, warning.page_number or 0,
                warning.field_key or "", warning.occurrence if warning.occurrence is not None else -1,
                warning.code,
            ))
        outcome = "Partial" if warnings else "Ready"
        duration_ms = max(0, round((time.perf_counter() - started) * 1000))
        used_native = any(page.source == "native" for page in pages)
        adapter_version = ADAPTER_VERSION if self.language == "eng" else "local-1.4.0"
        if self.layout_profile != "block" or ("ara" in self.language and self.arabic_model == "best"):
            adapter_version = "local-1.5.0"
        if supplier:
            adapter_version = "local-1.6.0"
        if used_ocr and used_native:
            provider = ProviderResult(
                "local",
                adapter_version,
                f"pdfplumber-tesseract-{self.language}-osd",
                (
                    f"pdfplumber-{PDFPLUMBER_VERSION}/pdfminer-{PDFMINER_VERSION}"
                    f"+tesseract-{TESSERACT_VERSION}/{self.language}-osd-{ENG_MODEL_VERSION}"
                ),
            )
        elif used_ocr:
            provider = ProviderResult(
                "local",
                adapter_version,
                f"tesseract-{self.language}-osd",
                f"{TESSERACT_VERSION}/{self.language}-osd-{ENG_MODEL_VERSION}",
            )
        else:
            provider = ProviderResult(
                "local",
                adapter_version,
                "pdfplumber",
                f"{PDFPLUMBER_VERSION}/pdfminer-{PDFMINER_VERSION}",
            )
        if used_ocr and adapter_version == "local-1.5.0":
            provider = replace(provider, model_version=provider.model_version +
                               f"/ara-{self.arabic_model}/layout-{self.layout_profile}")
        elif used_ocr and supplier:
            arabic_suffix = f"/ara-{self.arabic_model}" if "ara" in self.language else ""
            provider = replace(provider, model_version=provider.model_version + arabic_suffix +
                               f"/layout-{self.layout_profile}")
        result = ExtractionResult(
            schema_version="2.0",
            outcome=outcome,
            document_type=document_type,
            language=script_language("\n".join(page.canonical_text for page in pages)),
            provider=provider,
            metrics=MetricsResult(duration_ms, len(pages)),
            pages=[
                PageResult(page.page_number, page.canonical_text, page.rotation_degrees)
                for page in pages
            ],
            fields=fields,
            warnings=warnings,
        )
        try:
            payload = asdict(result)
            validate_normalized_result(payload)
        except ContractValidationError as error:
            raise ExtractionError(
                "internal_extraction_error",
                "The local provider produced a result that failed contract validation.",
            ) from error
        return result

    @staticmethod
    def _detect_media_type(path: Path, claimed: str) -> str:
        with path.open("rb") as stream:
            header = stream.read(16)
        detected = None
        if header.startswith(b"%PDF-"):
            detected = "application/pdf"
        elif header.startswith(b"\x89PNG\r\n\x1a\n"):
            detected = "image/png"
        elif header.startswith(b"\xff\xd8\xff"):
            detected = "image/jpeg"
        if detected is not None:
            aliases = {"image/jpg": "image/jpeg"}
            if aliases.get(claimed, claimed) != detected:
                raise ExtractionError(
                    "unsupported_media_type",
                    "The declared media type does not match the document content.",
                )
            return detected
        if claimed in {"application/pdf", "image/png", "image/jpeg"}:
            raise ExtractionError(
                "corrupt_document",
                "The document could not be decoded as its declared media type.",
            )
        raise ExtractionError(
            "unsupported_media_type",
            "The local provider supports PDF, PNG, and JPEG documents only.",
        )

    def _extract_pdf(
        self, path: Path, deadline_monotonic: float | None = None
    ) -> tuple[list[_ExtractedPage], bool]:
        try:
            import pdfplumber
            import pypdfium2 as pdfium
            from pdfminer.pdfdocument import PDFPasswordIncorrect
            from pdfminer.pdfparser import PDFSyntaxError
        except ImportError as error:
            raise self._runtime_unavailable("Native PDF dependencies are unavailable.") from error

        try:
            self._remaining(deadline_monotonic)
            preflight = pdfium.PdfDocument(path)
            preflight.close()
            self._remaining(deadline_monotonic)
        except Exception as error:
            if getattr(error, "err_code", None) == 4:
                raise ExtractionError(
                    "encrypted_document",
                    "Password-protected documents are not supported by the local provider.",
                ) from error
            raise ExtractionError(
                "corrupt_document",
                "The PDF could not be parsed safely.",
            ) from error

        try:
            with pdfplumber.open(path) as document:
                if not 1 <= len(document.pages) <= MAX_PAGES:
                    raise ExtractionError(
                        "page_limit_exceeded",
                        f"The document must contain between 1 and {MAX_PAGES} pages.",
                    )
                native_pages = []
                for index, page in enumerate(document.pages, start=1):
                    self._remaining(deadline_monotonic)
                    native_pages.append(self._native_page(page, index))
                    self._remaining(deadline_monotonic)
        except PDFPasswordIncorrect as error:
            raise ExtractionError(
                "encrypted_document",
                "Password-protected documents are not supported by the local provider.",
            ) from error
        except ExtractionError:
            raise
        except (PDFSyntaxError, OSError, ValueError) as error:
            raise ExtractionError(
                "corrupt_document",
                "The PDF could not be parsed safely.",
            ) from error
        except Exception as error:
            if "password" in str(error).lower() or "encrypt" in str(error).lower():
                raise ExtractionError(
                    "encrypted_document",
                    "Password-protected documents are not supported by the local provider.",
                ) from error
            raise ExtractionError(
                "corrupt_document",
                "The PDF could not be parsed safely.",
            ) from error

        arabic_pages = {page.page_number for page in native_pages if has_arabic(page.canonical_text)}
        if arabic_pages and "ara" not in self.language.split("+"):
            raise ExtractionError("unsupported_document", "Arabic pages require Arabic OCR configuration.")
        blank_page_numbers = [page.page_number for page in native_pages
                              if not page.canonical_text.strip() or page.page_number in arabic_pages]
        if not blank_page_numbers:
            return native_pages, False
        ocr_results = (
            self._ocr_pdf(path, blank_page_numbers)
            if deadline_monotonic is None
            else self._ocr_pdf(path, blank_page_numbers, deadline_monotonic)
        )
        ocr_pages = {page.page_number: page for page in ocr_results}
        combined = [ocr_pages.get(page.page_number, page) for page in native_pages]
        return combined, True

    @staticmethod
    def _native_page(page: Any, page_number: int) -> _ExtractedPage:
        characters = sorted(page.chars, key=lambda item: (round(item["top"], 1), item["x0"]))
        groups: list[list[dict[str, Any]]] = []
        for character in characters:
            if not groups or abs(character["top"] - groups[-1][0]["top"]) > 1.5:
                groups.append([character])
            else:
                groups[-1].append(character)
        line_tops = [group[0]["top"] for group in groups]
        gaps = [right - left for left, right in zip(line_tops, line_tops[1:]) if right > left]
        leading = statistics.median(gaps) if gaps else 0
        lines: list[_TextLine] = []
        canonical_parts: list[str] = []
        previous_top: float | None = None
        for group in groups:
            top = group[0]["top"]
            if previous_top is not None and leading and top - previous_top > leading * 1.55:
                canonical_parts.append("")
                lines.append(_TextLine("", [], []))
            ordered = sorted(group, key=lambda item: item["x0"])
            text = "".join(item["text"] for item in ordered).rstrip()
            boxes = [
                (
                    item["x0"] / page.width,
                    item["top"] / page.height,
                    item["x1"] / page.width,
                    item["bottom"] / page.height,
                )
                for item in ordered[: len(text)]
            ]
            canonical_parts.append(text)
            lines.append(_TextLine(text, boxes, [None] * len(boxes)))
            previous_top = top
        canonical = unicodedata.normalize("NFC", "\n".join(canonical_parts))
        if canonical:
            canonical += "\n"
        rotation = int(getattr(page, "rotation", 0) or 0) % 360
        return _ExtractedPage(
            page_number, canonical, lines, rotation, None, False, False, "native"
        )

    def _ocr_pdf(
        self,
        path: Path,
        page_numbers: list[int] | None = None,
        deadline_monotonic: float | None = None,
    ) -> list[_ExtractedPage]:
        try:
            import pypdfium2 as pdfium
        except ImportError as error:
            raise self._runtime_unavailable("PDF rendering is unavailable.") from error
        try:
            self._remaining(deadline_monotonic)
            document = pdfium.PdfDocument(path)
            if not 1 <= len(document) <= MAX_PAGES:
                raise ExtractionError(
                    "page_limit_exceeded",
                    f"The document must contain between 1 and {MAX_PAGES} pages.",
                )
            selected = page_numbers or list(range(1, len(document) + 1))
            pages = []
            scale = self.render_dpi / 72
            for page_number in selected:
                self._remaining(deadline_monotonic)
                page = document[page_number - 1]
                page_width, page_height = page.get_size()
                self._check_pixel_dimensions(
                    math.ceil(page_width * scale), math.ceil(page_height * scale)
                )
                image = page.render(scale=scale).to_pil()
                self._remaining(deadline_monotonic)
                self._check_pixel_budget(image)
                pdf_rotation = int(page.get_rotation())
                pages.append(
                    self._ocr_image(
                        image,
                        page_number,
                        applied_rotation=pdf_rotation % 360,
                        deadline_monotonic=deadline_monotonic,
                    )
                )
            return pages
        except ExtractionError:
            raise
        except Exception as error:
            raise ExtractionError(
                "corrupt_document",
                "The PDF could not be rendered safely.",
            ) from error

    @staticmethod
    def _open_image(path: Path) -> tuple[Any, int]:
        try:
            from PIL import Image, ImageOps, UnidentifiedImageError
            import warnings

            with warnings.catch_warnings():
                warnings.simplefilter("error", Image.DecompressionBombWarning)
                with Image.open(path) as source:
                    LocalInvoiceExtractor._check_pixel_dimensions(
                        source.width, source.height
                    )
                    source.verify()
                with Image.open(path) as source:
                    LocalInvoiceExtractor._check_pixel_dimensions(
                        source.width, source.height
                    )
                    orientation = int(source.getexif().get(274, 1))
                    exif_rotation = {3: 180, 6: 90, 8: 270}.get(orientation, 0)
                    result = ImageOps.exif_transpose(source).convert("RGB")
                    return result, exif_rotation
        except ImportError as error:
            raise LocalInvoiceExtractor._runtime_unavailable(
                "Image decoding is unavailable."
            ) from error
        except (Image.DecompressionBombError, Image.DecompressionBombWarning) as error:
            raise ExtractionError(
                "input_too_large",
                "A decoded page exceeds the local provider pixel limit.",
            ) from error
        except (UnidentifiedImageError, OSError, ValueError) as error:
            raise ExtractionError(
                "corrupt_document",
                "The image could not be decoded safely.",
            ) from error

    def _ocr_image(
        self,
        image: Any,
        page_number: int,
        *,
        applied_rotation: int = 0,
        deadline_monotonic: float | None = None,
    ) -> _ExtractedPage:
        from PIL import ImageFilter, ImageOps

        self._remaining(deadline_monotonic)
        detected_rotation = self._ocr_orientation(image, deadline_monotonic)
        ocr_rotation = detected_rotation or 0
        corrected = (
            image.rotate(-ocr_rotation, expand=True, fillcolor="white")
            if ocr_rotation
            else image
        )
        self._check_pixel_budget(corrected)
        prepared = ImageOps.autocontrast(corrected.convert("L")).filter(ImageFilter.SHARPEN)
        self._remaining(deadline_monotonic)
        if self.layout_profile == "lines-v1":
            from .arabic_ocr_layout import refine_lines
            # Refinement shares one page budget; retries cannot reset it.
            deadline = min(deadline_monotonic or float("inf"), time.monotonic() + self.timeout_seconds)
            data = self._ocr_data(prepared, deadline)
            data = refine_lines(
                prepared, data,
                lambda crop, arabic: self._ocr_data(crop, deadline, language="ara" if arabic else self.language, psm=7),
                lambda: self._remaining(deadline),
            )
        else:
            data = self._ocr_data(prepared, deadline_monotonic)
        self._remaining(deadline_monotonic)
        orientation_uncertain = detected_rotation is None and self._label_score(data) < 2
        image_size = prepared.size
        clockwise_correction = (applied_rotation + ocr_rotation) % 360
        page = self._page_from_ocr(data, page_number, clockwise_correction, image_size)
        degraded = (
            ocr_rotation != 0
            or orientation_uncertain
            or (page.ocr_confidence or 0) < 0.75
        )
        return _ExtractedPage(
            page.page_number,
            page.canonical_text,
            page.lines,
            clockwise_correction,
            page.ocr_confidence,
            degraded,
            orientation_uncertain,
            "ocr",
        )

    def _ocr_orientation(
        self, image: Any, deadline_monotonic: float | None = None
    ) -> int | None:
        try:
            import pytesseract
            from pytesseract import Output

            self._verify_ocr_engine(pytesseract)
            result = pytesseract.image_to_osd(
                image,
                output_type=Output.DICT,
                timeout=min(self.timeout_seconds, 15, self._remaining(deadline_monotonic)),
            )
            rotation = int(result.get("rotate", 0))
            confidence = float(result.get("orientation_conf", 0))
            if rotation not in {0, 90, 180, 270} or confidence < 5:
                return None
            return rotation
        except ExtractionError:
            raise
        except RuntimeError as error:
            if "timeout" in str(error).lower():
                raise ExtractionError(
                    "provider_timeout",
                    "The local OCR engine did not finish within this attempt.",
                    retryable=True,
                ) from error
            return None
        except Exception:
            return None

    def _ocr_data(
        self, image: Any, deadline_monotonic: float | None = None,
        *, language: str | None = None, psm: int = 6,
    ) -> dict[str, list[Any]]:
        try:
            import pytesseract
            from pytesseract import Output, TesseractNotFoundError

            self._verify_ocr_engine(pytesseract)
            return pytesseract.image_to_data(
                image,
                lang=language or self.language,
                config=f"--oem 1 --psm {psm}",
                output_type=Output.DICT,
                timeout=min(self.timeout_seconds, self._remaining(deadline_monotonic)),
            )
        except ImportError as error:
            raise self._runtime_unavailable("The OCR adapter is unavailable.") from error
        except TesseractNotFoundError as error:
            raise self._runtime_unavailable("The OCR engine is unavailable.") from error
        except RuntimeError as error:
            if "timeout" in str(error).lower():
                raise ExtractionError(
                    "provider_timeout",
                    "The local OCR engine did not finish within this attempt.",
                    retryable=True,
                ) from error
            raise ExtractionError(
                "provider_unavailable",
                "The local OCR engine could not process this page.",
                retryable=True,
            ) from error

    @staticmethod
    def _remaining(deadline_monotonic: float | None) -> float:
        if deadline_monotonic is None:
            return 120.0
        remaining = deadline_monotonic - time.monotonic()
        if remaining <= 0:
            raise ExtractionError(
                "provider_timeout",
                "The extraction provider did not finish within this attempt.",
                retryable=True,
            )
        return remaining

    def _verify_ocr_engine(self, pytesseract: Any) -> None:
        languages = pytesseract.get_languages(config="")
        actual_version = str(pytesseract.get_tesseract_version()).split()[0]
        if actual_version != TESSERACT_VERSION:
            raise self._runtime_unavailable(
                "The OCR engine version does not match the approved runtime."
            )
        if not {*self.language.split("+"), "osd"}.issubset(languages):
            raise self._runtime_unavailable(
                "The configured language and orientation OCR models are unavailable."
            )
        self._verify_model_hashes(self.language, self.arabic_model)

    @staticmethod
    def _verify_model_hashes(language: str = "eng", arabic_model: str = "fast") -> None:
        tessdata = Path(
            os.getenv("OCR_TESSDATA_DIR", "/usr/share/tesseract-ocr/5/tessdata")
        )
        expected = {
            "eng.traineddata": ENG_MODEL_SHA256,
            "osd.traineddata": OSD_MODEL_SHA256,
        }
        if "ara" in language.split("+"):
            expected["ara.traineddata"] = ARA_BEST_MODEL_SHA256 if arabic_model == "best" else ARA_MODEL_SHA256
        try:
            actual = {
                name: hashlib.sha256((tessdata / name).read_bytes()).hexdigest()
                for name in expected
            }
        except OSError as error:
            raise LocalInvoiceExtractor._runtime_unavailable(
                "The pinned OCR model data is unavailable."
            ) from error
        if actual != expected:
            raise LocalInvoiceExtractor._runtime_unavailable(
                "The OCR model data does not match the approved runtime."
            )

    @staticmethod
    def _page_from_ocr(
        data: dict[str, list[Any]],
        page_number: int,
        rotation: int,
        image_size: tuple[int, int],
    ) -> _ExtractedPage:
        width, height = image_size
        grouped: dict[tuple[int, int, int], list[int]] = {}
        for index, raw_text in enumerate(data["text"]):
            if not str(raw_text).strip():
                continue
            key = (
                int(data["block_num"][index]),
                int(data["par_num"][index]),
                int(data["line_num"][index]),
            )
            grouped.setdefault(key, []).append(index)
        lines: list[_TextLine] = []
        all_confidences: list[float] = []
        for key in sorted(grouped):
            indices = grouped[key]
            # Tesseract emits logical token order. Sorting x reverses RTL words.
            if not any(has_arabic(str(data["text"][index])) for index in indices):
                indices.sort(key=lambda index: int(data["left"][index]))
            text_parts: list[str] = []
            boxes: list[tuple[float, float, float, float] | None] = []
            confidences: list[float | None] = []
            for token_position, index in enumerate(indices):
                if token_position:
                    text_parts.append(" ")
                    boxes.append(None)
                    confidences.append(None)
                token = unicodedata.normalize("NFC", str(data["text"][index]).strip())
                confidence = max(0.0, min(1.0, float(data["conf"][index]) / 100))
                box = (
                    int(data["left"][index]) / width,
                    int(data["top"][index]) / height,
                    (int(data["left"][index]) + int(data["width"][index])) / width,
                    (int(data["top"][index]) + int(data["height"][index])) / height,
                )
                text_parts.append(token)
                boxes.extend([box] * len(token))
                confidences.extend([confidence] * len(token))
                all_confidences.extend([confidence] * len(token))
            lines.append(_TextLine("".join(text_parts), boxes, confidences))
        canonical = "\n".join(line.text for line in lines)
        if canonical:
            canonical += "\n"
        mean = statistics.fmean(all_confidences) if all_confidences else 0.0
        return _ExtractedPage(
            page_number, canonical, lines, rotation, mean, False, False, "ocr"
        )

    @staticmethod
    def _label_score(data: dict[str, list[Any]]) -> int:
        text = " ".join(str(item) for item in data.get("text", [])).lower()
        return sum(label.lower() in text for item in LABELS for label in item.prefixes)

    @staticmethod
    def _mean_confidence(data: dict[str, list[Any]]) -> float:
        values = []
        for raw in data.get("conf", []):
            try:
                value = float(raw)
            except (TypeError, ValueError):
                continue
            if math.isfinite(value) and value >= 0:
                values.append(value)
        return statistics.fmean(values) if values else 0.0

    @classmethod
    def _fields(cls, pages: list[_ExtractedPage], input_profile: str = "legacy", *,
                excluded_lines: set[tuple[int, int]] | None = None,
                labels: tuple[_Label, ...] = LABELS,
                include_lines: bool = True) -> list[FieldResult]:
        if input_profile == "supplier-invoice-v1":
            return cls._supplier_fields(pages)[0]
        results: list[FieldResult] = []
        for label in labels:
            if label.key == "due_date":
                due_date, _ = cls._due_date(pages, excluded_lines=excluded_lines)
                if due_date is not None:
                    results.append(due_date)
                continue
            result: FieldResult | None = None
            # Prefix order is semantic priority. UAE invoices may contain both
            # invoice-currency VAT and the AED "VAT charged" amount, so the
            # more specific label must win even when it occurs later.
            for prefix in label.prefixes:
                for page in pages:
                    character_offset = 0
                    for line_index, line in enumerate(page.lines):
                        if excluded_lines and (page.page_number, line_index) in excluded_lines:
                            character_offset += len(line.text) + 1
                            continue
                        if excluded_lines is not None and not re.match(
                            rf"^\s*{re.escape(matching_view(prefix).text)}\s*(?:\([^:\n]*\))?\s*:",
                            matching_view(line.text).text, re.I,
                        ):
                            character_offset += len(line.text) + 1
                            continue
                        found = cls._match_line(line.text, (prefix,))
                        if found is None:
                            character_offset += len(line.text) + 1
                            continue
                        value, start, end = found
                        quote_start = character_offset + start
                        page_prefix = page.canonical_text[:quote_start].encode("utf-8")
                        start_utf8 = len(page_prefix)
                        end_utf8 = start_utf8 + len(value.encode("utf-8"))
                        boxes = [box for box in line.character_boxes[start:end] if box is not None]
                        confidences = [
                            confidence
                            for confidence in line.confidences[start:end]
                            if confidence is not None
                        ]
                        confidence = statistics.fmean(confidences) if confidences else None
                        result = FieldResult(
                            key=label.key,
                            occurrence=0,
                            value_type=label.value_type,
                            value=value,
                            normalized_value=cls._normalize(value, label.value_type),
                            confidence=confidence,
                            evidence=[
                                EvidenceResult(
                                    page.page_number,
                                    value,
                                    start_utf8,
                                    end_utf8,
                                    cls._polygon(boxes),
                                    "normalized" if boxes else None,
                                )
                            ],
                        )
                        break
                    if result is not None:
                        break
                if result is not None:
                    break
            if result is not None:
                results.append(result)
        order = {label.key: index for index, label in enumerate(LABELS)}
        line_order = {label.key: index for index, label in enumerate(LINE_LABELS)}
        if include_lines:
            results.extend(cls._line_fields(pages))
        return sorted(
            results,
            key=lambda field: (
                0 if field.occurrence == 0 else 1,
                order.get(field.key, line_order.get(field.key, len(LINE_LABELS))),
                field.occurrence,
            ),
        )

    @classmethod
    def _due_date(cls, pages: list[_ExtractedPage], *,
                  excluded_lines: set[tuple[int, int]] | None = None) -> tuple[FieldResult | None, str | None]:
        """Only explicit, valid ISO dates; inspect every label before choosing a value."""
        candidates: list[tuple[str, EvidenceResult, list[float]]] = []
        invalid = False
        prefixes = next(label.prefixes for label in LABELS if label.key == "due_date")
        pattern = re.compile(r"(?<!\w)(?:" + "|".join(re.escape(p) for p in prefixes) + r")\s*:", re.I)
        for page in pages:
            offset = 0
            for line_index, line in enumerate(page.lines):
                if excluded_lines is not None and (
                    (page.page_number, line_index) in excluded_lines or not re.match(
                        r"^\s*(?:" + "|".join(re.escape(matching_view(prefix).text) for prefix in prefixes) + r")\s*:",
                        matching_view(line.text).text, re.I)
                ):
                    offset += len(line.text) + 1
                    continue
                view = matching_view(line.text)
                for match in pattern.finditer(view.text):
                    source_start = view.starts[match.start()]
                    found = cls._match_line(line.text[source_start:], prefixes)
                    if found is None:
                        invalid = True
                        continue
                    value, start, end = found
                    start += source_start
                    end += source_start
                    try:
                        date_value = normalize_value(value, "Date")
                        if not re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}", date_value):
                            raise ValueError()
                        normalized = date.fromisoformat(date_value).isoformat()
                    except ValueError:
                        invalid = True
                        continue
                    start_utf8 = len(page.canonical_text[:offset + start].encode("utf-8"))
                    boxes = [b for b in line.character_boxes[start:end] if b is not None]
                    confidence = [c for c in line.confidences[start:end] if c is not None]
                    candidates.append((normalized, EvidenceResult(
                        page.page_number, value, start_utf8, start_utf8 + len(value.encode("utf-8")),
                        cls._polygon(boxes), "normalized" if boxes else None), confidence))
                offset += len(line.text) + 1
        if len({value for value, _, _ in candidates}) > 1:
            return None, "due_date_conflicting"
        if invalid:
            return None, "due_date_requires_review"
        if not candidates:
            return None, None
        confidences = [c for _, _, values in candidates for c in values]
        return FieldResult("due_date", 0, "Date", candidates[0][1].quote, candidates[0][0],
            statistics.fmean(confidences) if confidences else None,
            [evidence for _, evidence, _ in candidates[:10]]), None

    @classmethod
    def _line_fields(cls, pages: list[_ExtractedPage]) -> list[FieldResult]:
        results: list[FieldResult] = []
        labels = {matching_view(prefix).text.lower(): label
                  for label in LINE_LABELS for prefix in label.prefixes}
        names = "|".join(re.escape(prefix) for prefix in labels)
        pattern = re.compile(
            rf"^\s*(?:Line|البند|السطر)\s+(\d+)\s+({names})\s*:\s*(.+?)\s*$",
            re.I,
        )
        for page in pages:
            character_offset = 0
            for line in page.lines:
                view = matching_view(line.text) if has_arabic(line.text) else None
                match = pattern.match(view.text if view else line.text)
                if match is None:
                    character_offset += len(line.text) + 1
                    continue
                occurrence = int(match.group(1))
                if occurrence <= 0:
                    character_offset += len(line.text) + 1
                    continue
                label = labels[match.group(2).lower()]
                value = match.group(3).strip()
                start = match.start(3)
                end = start + len(value)
                if view is not None:
                    start, end = view.source_span(start, end)
                    value = line.text[start:end]
                quote_start = character_offset + start
                start_utf8 = len(page.canonical_text[:quote_start].encode("utf-8"))
                end_utf8 = start_utf8 + len(value.encode("utf-8"))
                boxes = [box for box in line.character_boxes[start:end] if box is not None]
                confidences = [
                    confidence for confidence in line.confidences[start:end]
                    if confidence is not None
                ]
                results.append(FieldResult(
                    key=label.key,
                    occurrence=occurrence,
                    value_type=label.value_type,
                    value=value,
                    normalized_value=cls._normalize(value, label.value_type),
                    confidence=statistics.fmean(confidences) if confidences else None,
                    evidence=[EvidenceResult(
                        page.page_number,
                        value,
                        start_utf8,
                        end_utf8,
                        cls._polygon(boxes),
                        "normalized" if boxes else None,
                    )],
                ))
                character_offset += len(line.text) + 1
        return results

    @staticmethod
    def _document_type(pages: list[_ExtractedPage], input_profile: str = "legacy") -> str:
        if input_profile == "supplier-invoice-v1":
            return LocalInvoiceExtractor._supplier_document_type(pages)
        text = "\n".join(page.canonical_text for page in pages)
        if re.search(r"\bPURCHASE\s+ORDER\b", text, re.I):
            return "purchase_order"
        if re.search(r"\bQUOTATION\b", text, re.I):
            return "quotation"
        return "invoice"

    @staticmethod
    def _supplier_document_type(pages: list[_ExtractedPage]) -> str:
        titles = {
            "invoice": ("invoice", "tax invoice", "فاتورة", "فاتورة ضريبية"),
            "purchase_order": ("purchase order", "امر شراء", "طلب شراء"),
            "quotation": ("quotation", "quote", "عرض سعر", "عرض اسعار"),
        }
        found = set()
        for page in pages:
            for line in page.canonical_text.splitlines():
                text = matching_view(line).text.strip().casefold()
                if any(text == title for title in titles["invoice"]):
                    found.add("invoice")
                for role in ("purchase_order", "quotation"):
                    if text in titles[role]:
                        found.add(role)
        return next(iter(found)) if len(found) == 1 else "unknown"

    @staticmethod
    def _supplier_normalize(value: str, value_type: str) -> str | None:
        if value_type == "Decimal":
            return normalize_supplier_number(value)
        if value_type in {"Identifier", "Date"}:
            if any(char.isdecimal() and char not in "0123456789" for char in value):
                return None
            if value_type == "Date":
                try:
                    if not re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}", value):
                        return None
                    return date.fromisoformat(value).isoformat()
                except ValueError:
                    return None
            return value
        return normalize_value(value, value_type)

    @classmethod
    def _supplier_fields(cls, pages: list[_ExtractedPage]) -> tuple[list[FieldResult], list[WarningResult]]:
        tables, warnings, excluded = cls._table_fields(pages)
        for page in pages:
            for index, line in enumerate(page.lines):
                if re.match(r"^\s*(?:Line|البند|السطر)\s+\d+\s+", matching_view(line.text).text, re.I):
                    excluded.add((page.page_number, index))
        labels = tuple(replace(label, prefixes=label.prefixes + (
            ("رقم امر الشراء", "رقم أمر الشراء", "رقم طلب الشراء") if label.key == "purchase_order_number"
            else ("رقم عرض السعر", "رقم عرض الاسعار") if label.key == "quotation_number" else ()
        )) for label in LABELS)
        fields = cls._fields(pages, excluded_lines=excluded, labels=labels, include_lines=False)
        _, due_issue = cls._due_date(pages, excluded_lines=excluded)
        if due_issue:
            warnings.append(WarningResult(due_issue, "degraded",
                "Explicit due dates conflict; review the source before adding a due date."
                if due_issue == "due_date_conflicting" else
                "The explicit due date is invalid or outside the supported ISO format; review the source."))
        labeled = cls._line_fields(pages)
        if tables and labeled:
            warnings.append(WarningResult("line_table_requires_review", "degraded",
                "Numbered fields and ordinary tables cannot be combined into inferred row identities.",
                tables[0].evidence[0].page_number))
        else:
            fields.extend(labeled)
            fields.extend(tables)
        grouped: dict[tuple[str, int], list[FieldResult]] = {}
        for field in fields:
            grouped.setdefault((field.key, field.occurrence), []).append(field)
        result = []
        for (key, occurrence), candidates in grouped.items():
            if len(candidates) != 1:
                warnings.append(WarningResult("field_identity_ambiguous", "degraded",
                    "Repeated field identity requires review; no value was selected.",
                    candidates[0].evidence[0].page_number))
                continue
            field = candidates[0]
            normalized = cls._supplier_normalize(field.value, field.value_type)
            if field.key in {"invoice_number", "purchase_order_number", "quotation_number", "line_item_code"}:
                if any(char.isdecimal() and char not in "0123456789" for char in field.value):
                    normalized = None
            result.append(replace(field, normalized_value=normalized))
            if normalized is None:
                warnings.append(WarningResult("numeric_requires_review", "degraded",
                    "The source value is outside the English-digit numeric/date profile; correct it explicitly.",
                    field.evidence[0].page_number, key, occurrence))
        return sorted(result, key=lambda field: (field.occurrence, field.key)), warnings

    @staticmethod
    def _supplier_coverage(fields: list[FieldResult], document_type: str) -> list[WarningResult]:
        warnings = []
        if document_type == "unknown":
            warnings.append(WarningResult("document_role_requires_review", "degraded",
                "An unambiguous supported document title could not be established."))
        required = {"supplier_trn", "currency", "total"}
        required.add({"invoice": "invoice_number", "purchase_order": "purchase_order_number",
                      "quotation": "quotation_number"}.get(document_type, "document_number"))
        present = {field.key for field in fields if field.occurrence == 0 and field.normalized_value is not None}
        if required - present:
            warnings.append(WarningResult("supplier_required_fields_missing", "degraded",
                "Required supplier, currency, document identity or total coverage is missing or invalid."))
        rows = {field.occurrence for field in fields if field.occurrence > 0}
        if not rows:
            warnings.append(WarningResult("line_coverage_missing", "degraded",
                "No supported line rows were extracted; complete line agreement cannot be established."))
        else:
            for occurrence in sorted(rows):
                available = {field.key for field in fields if field.occurrence == occurrence
                             and field.normalized_value is not None}
                if not ({"line_item_code", "line_description"} & available) or not {
                    "line_quantity", "line_unit_price", "line_unit", "line_price_basis", "line_total"
                } <= available:
                    warnings.append(WarningResult("line_coverage_incomplete", "degraded",
                        "A line lacks supported values, unit, price basis or explicit gross total; affected checks remain unavailable."))
                    break
        return warnings

    @classmethod
    def _table_fields(cls, pages: list[_ExtractedPage]) -> tuple[
            list[FieldResult], list[WarningResult], set[tuple[int, int]]]:
        """Read a small, explicitly headed table using original character geometry."""
        aliases = {
            "line_item_code": ("Item code", "رمز الصنف"),
            "line_description": ("Description", "الوصف", "وصف الصنف"),
            "line_quantity": ("Quantity", "Qty", "الكمية"),
            "line_unit_price": ("Unit price", "سعر الوحدة"),
            "line_unit": ("Unit", "الوحدة"),
            "line_price_basis": ("Price basis", "اساس السعر"),
            "line_tax": ("Tax", "VAT", "الضريبة"),
            "line_total": ("Gross total", "Gross AED", "اجمالي شامل الضريبة"),
            # Net amounts and ambiguous Total columns delimit cells but never become gross totals.
            None: ("Net AED", "Net amount", "Total", "المبلغ الصافي", "الاجمالي"),
        }
        lookup = {matching_view(alias).text.casefold(): key for key, names in aliases.items() for alias in names}
        pattern = re.compile("|".join(re.escape(name) for name in sorted(lookup, key=len, reverse=True)), re.I)
        fields, warnings, excluded = [], [], set()
        occurrence = 0
        for page in pages:
            columns = None
            blocked_table = False
            character_offset = 0
            for line_index, line in enumerate(page.lines):
                view = matching_view(line.text)
                matches = list(pattern.finditer(view.text))
                remainder = pattern.sub("", view.text)
                keys = [lookup[match.group().casefold()] for match in matches]
                is_header = ("line_description" in keys and "line_quantity" in keys
                    and "line_unit_price" in keys and not remainder.strip(" |\t"))
                if is_header:
                    blocked_table = False
                    excluded.add((page.page_number, line_index))
                    headers = []
                    for match, key in zip(matches, keys, strict=True):
                        start, end = view.source_span(match.start(), match.end())
                        if len(line.character_boxes) != len(line.text) or any(
                            not line.text[index].isspace() and line.character_boxes[index] is None
                            for index in range(start, end)
                        ):
                            break
                        boxes = [box for box in line.character_boxes[start:end] if box is not None]
                        if not boxes:
                            break
                        headers.append((min(box[0] for box in boxes), key))
                    columns = sorted(headers, key=lambda item: item[0]) if len(headers) == len(matches) and len(set(keys)) == len(keys) else None
                    if columns is None or any(b[0] - a[0] < .015 for a, b in zip(columns, columns[1:])):
                        columns = None
                        blocked_table = True
                        warnings.append(WarningResult("line_table_requires_review", "degraded",
                            "Table columns lack distinct reliable geometry; no row values were inferred.", page.page_number))
                elif columns is not None and line.text.strip():
                    # Explicit totals/notes end the table. A description-only continuation is unsupported.
                    if re.match(r"^(?:Amounts are|Invoice totals|Row amounts are|Unit and price basis require|"
                                r"(?:Subtotal|Total|VAT|Tax|Currency|الإجمالي|المجموع|الضريبة)\s*:)",
                                line.text.strip(), re.I):
                        columns = None
                    else:
                        excluded.add((page.page_number, line_index))
                        cells = []
                        reliable = len(line.character_boxes) == len(line.text)
                        accounted: set[int] = set()
                        for column_index, (anchor, key) in enumerate(columns):
                            left = anchor - .015
                            right = columns[column_index + 1][0] - .015 if column_index + 1 < len(columns) else 1.0
                            positions = [index for index, (char, box) in enumerate(zip(line.text, line.character_boxes))
                                if not char.isspace() and box is not None and left <= (box[0] + box[2]) / 2 < right]
                            if not positions:
                                reliable = False
                                break
                            start, end = min(positions), max(positions) + 1
                            if any(not line.text[index].isspace() and index not in positions for index in range(start, end)):
                                reliable = False
                                break
                            accounted.update(positions)
                            boxes = [box for box in line.character_boxes[start:end] if box is not None]
                            if any(box[0] < left or box[2] > right for box in boxes):
                                reliable = False
                                break
                            if key is not None:
                                value = line.text[start:end]
                                confidence = [c for c in line.confidences[start:end] if c is not None]
                                start_utf8 = len(page.canonical_text[:character_offset + start].encode("utf-8"))
                                cells.append((key, value, start_utf8, boxes, confidence))
                        if accounted != {index for index, char in enumerate(line.text) if not char.isspace()}:
                            reliable = False
                        if not reliable or not cells:
                            warnings.append(WarningResult("line_table_requires_review", "degraded",
                                "A table row is blank, wrapped or crosses column boundaries; no row values were inferred.", page.page_number))
                            columns = None
                            blocked_table = True
                        else:
                            occurrence += 1
                            for key, value, start_utf8, boxes, confidence in cells:
                                kind = "Decimal" if key in {"line_quantity", "line_unit_price", "line_tax", "line_total"} else "String"
                                fields.append(FieldResult(key, occurrence, kind, value,
                                    cls._supplier_normalize(value, kind),
                                    statistics.fmean(confidence) if confidence else None,
                                    [EvidenceResult(page.page_number, value, start_utf8,
                                        start_utf8 + len(value.encode("utf-8")), cls._polygon(boxes), "normalized")]))
                elif blocked_table:
                    # A refused table cannot donate amounts from its descriptions to document headers.
                    excluded.add((page.page_number, line_index))
                character_offset += len(line.text) + 1
        return fields, warnings, excluded

    @staticmethod
    def _match_line(text: str, prefixes: Iterable[str]) -> tuple[str, int, int] | None:
        # Match a derived view, then map its value span to the original text.
        # Keep the existing English path byte-for-byte stable.
        if has_arabic(text):
            view = matching_view(text)
            found = LocalInvoiceExtractor._match_view(
                view.text, (matching_view(prefix).text for prefix in prefixes))
            if found is None:
                return None
            _, start, end = found
            start, end = view.source_span(start, end)
            return text[start:end], start, end
        return LocalInvoiceExtractor._match_view(text, prefixes)

    @staticmethod
    def _match_view(text: str, prefixes: Iterable[str]) -> tuple[str, int, int] | None:
        for prefix in prefixes:
            start_boundary = r"(?<![A-Za-z])" if prefix == "Total" else ""
            match = re.search(
                rf"{start_boundary}{re.escape(prefix)}\s*(?:\([^:\n]*\))?\s*:\s*",
                text,
                re.I,
            )
            if match is None:
                continue
            start = match.end()
            remainder = text[start:]
            boundary_labels = [
                *(prefix for label in LABELS for prefix in label.prefixes),
                "Discount",
                "Due date",
                "Taxable amount",
                "Method",
                "Invoice VAT",
                "Amount due",
                "TIN",
            ]
            boundary_names = "|".join(
                re.escape(matching_view(name).text) for name in sorted(boundary_labels, key=len, reverse=True)
            )
            boundary = re.search(
                rf"\s*\||\s*(?=(?:{boundary_names})"
                r"\s*(?:\([^:\n]*\))?\s*(?::|\s))",
                remainder,
                re.I,
            )
            end = start + (boundary.start() if boundary else len(remainder))
            # OCR may emit direction marks beside Latin/numeric values. Exclude
            # only boundary marks from the selected span; keep page bytes intact.
            while start < end and (text[start].isspace() or text[start] in "\u061c\u200e\u200f"):
                start += 1
            while end > start and (text[end - 1].isspace() or text[end - 1] in "\u061c\u200e\u200f"):
                end -= 1
            if end > start:
                return text[start:end], start, end
        return None

    @staticmethod
    def _normalize(value: str, value_type: str) -> str:
        return normalize_value(value, value_type)

    @staticmethod
    def _polygon(boxes: list[tuple[float, float, float, float]]) -> list[list[float]] | None:
        if not boxes:
            return None
        left = min(box[0] for box in boxes)
        top = min(box[1] for box in boxes)
        right = max(box[2] for box in boxes)
        bottom = max(box[3] for box in boxes)
        return [
            [round(left, 6), round(top, 6)],
            [round(right, 6), round(top, 6)],
            [round(right, 6), round(bottom, 6)],
            [round(left, 6), round(bottom, 6)],
        ]

    @classmethod
    def _warnings(
        cls, pages: list[_ExtractedPage], fields: list[FieldResult], document_type: str = "invoice", *,
        check_due_date: bool = True,
    ) -> list[WarningResult]:
        warnings = [
            WarningResult(
                "page_ocr_degraded",
                "degraded",
                "OCR quality or orientation required review for this page.",
                page_number=page.page_number,
            )
            for page in pages
            if page.degraded
        ]
        warnings.extend(
            WarningResult(
                "page_orientation_uncertain",
                "degraded",
                "Page orientation could not be established reliably.",
                page_number=page.page_number,
            )
            for page in pages
            if page.orientation_uncertain
        )
        if any(page.orientation_uncertain for page in pages):
            warnings = [
                warning
                for warning in warnings
                if not (
                    warning.code == "page_ocr_degraded"
                    and any(
                        page.page_number == warning.page_number
                        and page.orientation_uncertain
                        for page in pages
                    )
                )
            ]
        number_key = {
            "quotation": "quotation_number",
            "purchase_order": "purchase_order_number",
            "invoice": "invoice_number",
        }.get(document_type, "invoice_number")
        required = {number_key, "total"}
        missing = sorted(required - {field.key for field in fields})
        if missing:
            warnings.append(
                WarningResult(
                    "required_field_missing",
                    "degraded",
                    "A review field could not be extracted from this document.",
                )
            )
        if any(page.source == "ocr" and has_arabic(page.canonical_text) for page in pages):
            warnings.append(WarningResult("arabic_ocr_requires_review", "degraded",
                "Arabic OCR is experimental. Verify values against the original source."))
        _, due_issue = cls._due_date(pages) if check_due_date else (None, None)
        if due_issue:
            warnings.append(WarningResult(due_issue, "degraded",
                "Explicit due dates conflict; review the source before adding a due date."
                if due_issue == "due_date_conflicting" else
                "The explicit due date is empty, invalid or not in supported YYYY-MM-DD format; review the source."))
        return sorted(
            warnings,
            key=lambda warning: (
                0 if warning.severity == "info" else 1,
                warning.page_number or 0,
                warning.field_key or "",
                warning.occurrence if warning.occurrence is not None else -1,
                warning.code,
            ),
        )

    @staticmethod
    def _check_pixel_budget(image: Any) -> None:
        LocalInvoiceExtractor._check_pixel_dimensions(image.width, image.height)

    @staticmethod
    def _check_pixel_dimensions(width: int, height: int) -> None:
        if width <= 0 or height <= 0 or width * height > 25_000_000:
            raise ExtractionError(
                "input_too_large",
                "A decoded page exceeds the local provider pixel limit.",
            )

    @staticmethod
    def _verify_python_runtime() -> None:
        expected = {
            "pdfplumber": PDFPLUMBER_VERSION,
            "pdfminer.six": PDFMINER_VERSION,
            "pypdfium2": PYPDFIUM_VERSION,
            "pytesseract": PYTESSERACT_VERSION,
            "Pillow": PILLOW_VERSION,
        }
        try:
            actual = {package: version(package) for package in expected}
        except PackageNotFoundError as error:
            raise LocalInvoiceExtractor._runtime_unavailable(
                "A pinned local extraction dependency is unavailable."
            ) from error
        if actual != expected:
            raise LocalInvoiceExtractor._runtime_unavailable(
                "The local extraction dependency versions do not match the approved runtime."
            )

    @staticmethod
    def _runtime_unavailable(summary: str) -> ExtractionError:
        return ExtractionError("ocr_unavailable", summary, retryable=False)
