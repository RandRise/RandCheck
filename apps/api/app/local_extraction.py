from __future__ import annotations

import math
import os
import re
import statistics
import time
import unicodedata
import hashlib
from dataclasses import asdict
from dataclasses import dataclass
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


ADAPTER_VERSION = "local-1.0.0"
PDFPLUMBER_VERSION = "0.11.10"
PDFMINER_VERSION = "20260107"
PYPDFIUM_VERSION = "5.12.1"
PYTESSERACT_VERSION = "0.3.13"
TESSERACT_VERSION = "5.3.0"
ENG_MODEL_VERSION = "4.1.0"
ENG_MODEL_SHA256 = "7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2"
OSD_MODEL_SHA256 = "9cf5d576fcc47564f11265841e5ca839001e7e6f38ff7f7aacf46d15a96b00ff"
MAX_PAGES = 50


@dataclass(frozen=True)
class _Label:
    key: str
    prefixes: tuple[str, ...]
    value_type: str


LABELS = (
    _Label("invoice_number", ("Invoice Number",), "String"),
    _Label("invoice_date", ("Invoice Date",), "Date"),
    _Label("supplier_trn", ("Supplier TRN", "TRN"), "Identifier"),
    _Label("currency", ("Currency",), "Currency"),
    _Label("subtotal", ("Subtotal",), "Decimal"),
    _Label("vat", ("VAT 5%", "VAT"), "Decimal"),
    _Label("total", ("Total",), "Decimal"),
    _Label("payment_terms", ("Payment Terms",), "String"),
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
    """Credential-free English invoice adapter for native PDFs and local OCR."""

    name = "local-invoice-v1"

    def __init__(
        self,
        *,
        language: str = "eng",
        timeout_seconds: float = 30.0,
        render_dpi: int = 300,
    ) -> None:
        if language != "eng":
            raise ValueError("The Phase 2 local provider supports OCR_LANGUAGE=eng only")
        if not 1 <= timeout_seconds <= 120:
            raise ValueError("OCR_TIMEOUT_SECONDS must be between 1 and 120")
        if not 150 <= render_dpi <= 400:
            raise ValueError("OCR_RENDER_DPI must be between 150 and 400")
        self.language = language
        self.timeout_seconds = timeout_seconds
        self.render_dpi = render_dpi

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
        fields = self._fields(pages)
        if not any(page.canonical_text.strip() for page in pages):
            raise ExtractionError(
                "document_unreadable",
                "The document contains no text that the local provider can read.",
            )
        if not fields:
            raise ExtractionError(
                "unsupported_document",
                "The readable document is not a supported English invoice.",
            )
        warnings = self._warnings(pages, fields)
        outcome = "Partial" if warnings else "Ready"
        duration_ms = max(0, round((time.perf_counter() - started) * 1000))
        used_native = any(page.source == "native" for page in pages)
        if used_ocr and used_native:
            provider = ProviderResult(
                "local",
                ADAPTER_VERSION,
                "pdfplumber-tesseract-eng-osd",
                (
                    f"pdfplumber-{PDFPLUMBER_VERSION}/pdfminer-{PDFMINER_VERSION}"
                    f"+tesseract-{TESSERACT_VERSION}/eng-osd-{ENG_MODEL_VERSION}"
                ),
            )
        elif used_ocr:
            provider = ProviderResult(
                "local",
                ADAPTER_VERSION,
                "tesseract-eng-osd",
                f"{TESSERACT_VERSION}/eng-osd-{ENG_MODEL_VERSION}",
            )
        else:
            provider = ProviderResult(
                "local",
                ADAPTER_VERSION,
                "pdfplumber",
                f"{PDFPLUMBER_VERSION}/pdfminer-{PDFMINER_VERSION}",
            )
        result = ExtractionResult(
            schema_version="2.0",
            outcome=outcome,
            document_type="invoice",
            language="en",
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

        blank_page_numbers = [
            page.page_number for page in native_pages if not page.canonical_text.strip()
        ]
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
        self, image: Any, deadline_monotonic: float | None = None
    ) -> dict[str, list[Any]]:
        try:
            import pytesseract
            from pytesseract import Output, TesseractNotFoundError

            self._verify_ocr_engine(pytesseract)
            return pytesseract.image_to_data(
                image,
                lang=self.language,
                config="--oem 1 --psm 6",
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
        if not {self.language, "osd"}.issubset(languages):
            raise self._runtime_unavailable(
                "The configured English and orientation OCR models are unavailable."
            )
        self._verify_model_hashes()

    @staticmethod
    def _verify_model_hashes() -> None:
        tessdata = Path(
            os.getenv("OCR_TESSDATA_DIR", "/usr/share/tesseract-ocr/5/tessdata")
        )
        expected = {
            "eng.traineddata": ENG_MODEL_SHA256,
            "osd.traineddata": OSD_MODEL_SHA256,
        }
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
        canonical = unicodedata.normalize("NFC", "\n".join(line.text for line in lines))
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
    def _fields(cls, pages: list[_ExtractedPage]) -> list[FieldResult]:
        results: list[FieldResult] = []
        seen: set[str] = set()
        for page in pages:
            character_offset = 0
            for line in page.lines:
                for label in LABELS:
                    if label.key in seen:
                        continue
                    found = cls._match_line(line.text, label.prefixes)
                    if found is None:
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
                    results.append(
                        FieldResult(
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
                    )
                    seen.add(label.key)
                character_offset += len(line.text) + 1
        order = {label.key: index for index, label in enumerate(LABELS)}
        return sorted(results, key=lambda field: order[field.key])

    @staticmethod
    def _match_line(text: str, prefixes: Iterable[str]) -> tuple[str, int, int] | None:
        for prefix in prefixes:
            match = re.match(rf"^\s*{re.escape(prefix)}\s*:\s*(.+?)\s*$", text, re.I)
            if match:
                value = match.group(1).strip()
                start = match.start(1)
                while text[start].isspace():
                    start += 1
                return value, start, start + len(value)
        return None

    @staticmethod
    def _normalize(value: str, value_type: str) -> str:
        if value_type == "Decimal":
            return value.replace(",", "")
        if value_type == "Currency":
            return value.upper()
        return value

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

    @staticmethod
    def _warnings(
        pages: list[_ExtractedPage], fields: list[FieldResult]
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
        required = {"invoice_number", "total"}
        missing = sorted(required - {field.key for field in fields})
        if missing:
            warnings.append(
                WarningResult(
                    "required_field_missing",
                    "degraded",
                    "A review field could not be extracted from this document.",
                )
            )
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
            "Pillow": "12.3.0",
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
