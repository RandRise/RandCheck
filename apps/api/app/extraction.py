import re
import time
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol


@dataclass(frozen=True)
class FieldResult:
    key: str
    occurrence: int
    value_type: str
    value: str
    normalized_value: str | None
    confidence: float | None
    evidence: list["EvidenceResult"]


@dataclass(frozen=True)
class EvidenceResult:
    page_number: int
    quote: str | None
    start_utf8: int | None
    end_utf8: int | None
    polygon: list[list[float]] | None = None
    coordinate_unit: str | None = None


@dataclass(frozen=True)
class PageResult:
    page_number: int
    canonical_text: str
    rotation_degrees: int


@dataclass(frozen=True)
class ProviderResult:
    alias: str
    adapter_version: str
    model: str
    model_version: str


@dataclass(frozen=True)
class MetricsResult:
    provider_duration_ms: int
    page_count: int
    estimated_cost: float | None = None
    cost_currency: str | None = None
    price_basis: str | None = None


@dataclass(frozen=True)
class WarningResult:
    code: str
    severity: str
    safe_summary: str
    page_number: int | None = None
    field_key: str | None = None
    occurrence: int | None = None


@dataclass(frozen=True)
class ExtractionResult:
    schema_version: str
    outcome: str
    document_type: str
    language: str
    provider: ProviderResult
    metrics: MetricsResult
    pages: list[PageResult]
    fields: list[FieldResult]
    warnings: list[WarningResult]


class ExtractionProvider(Protocol):
    def extract(self, path: Path, content_type: str) -> ExtractionResult: ...


class ExtractionError(Exception):
    """Expected provider failure with a deliberately safe public message."""

    def __init__(self, code: str, summary: str, *, retryable: bool = False) -> None:
        super().__init__(summary)
        self.code = code
        self.summary = summary
        self.retryable = retryable


class MockInvoiceExtractor:
    """Deterministic local parser for text-like demos; not production OCR."""

    name = "mock-invoice-v1"
    labels = {
        "invoice_number": ("Invoice Number", "رقم الفاتورة"),
        "supplier": ("Supplier", "المورد"),
        "supplier_ar": ("Supplier Arabic", "اسم المورد بالعربية"),
        "invoice_date": ("Invoice Date", "تاريخ الفاتورة"),
        "currency": ("Currency", "العملة"),
        "subtotal": ("Subtotal", "المجموع الفرعي"),
        "vat": ("VAT", "ضريبة القيمة المضافة"),
        "total": ("Total", "الإجمالي"),
        "trn": ("TRN", "الرقم الضريبي"),
    }

    def extract(self, path: Path, content_type: str) -> ExtractionResult:
        started = time.perf_counter()
        raw_text = path.read_text(encoding="utf-8", errors="ignore") if content_type == "text/plain" else ""
        text = unicodedata.normalize("NFC", raw_text.replace("\r\n", "\n").replace("\r", "\n"))
        fields: list[FieldResult] = []
        for name, labels in self.labels.items():
            match = self._find(text, labels)
            if match:
                value, start_utf8, end_utf8 = match
                fields.append(FieldResult(
                    key=name,
                    occurrence=0,
                    value_type="String",
                    value=value,
                    normalized_value=value,
                    confidence=0.99,
                    evidence=[EvidenceResult(1, value, start_utf8, end_utf8)],
                ))
        has_arabic = bool(re.search(r"[\u0600-\u06ff]", text))
        has_latin = bool(re.search(r"[A-Za-z]", text))
        language = "ar-en" if has_arabic and has_latin else "ar" if has_arabic else "en"
        duration_ms = max(0, round((time.perf_counter() - started) * 1000))
        return ExtractionResult(
            schema_version="2.0",
            outcome="Ready",
            document_type="invoice",
            language=language,
            provider=ProviderResult("mock", "2.0", "mock-invoice", "v1"),
            metrics=MetricsResult(duration_ms, 1),
            pages=[PageResult(1, text, 0)],
            fields=fields,
            warnings=[],
        )

    @staticmethod
    def _find(text: str, labels: tuple[str, ...]) -> tuple[str, int, int] | None:
        character_offset = 0
        for line in text.splitlines(keepends=True):
            line_without_ending = line.rstrip("\n")
            for label in labels:
                leading = len(line_without_ending) - len(line_without_ending.lstrip())
                match = re.match(rf"^{re.escape(label)}\s*:\s*(.+)$", line_without_ending.strip(), re.I)
                if match:
                    value = match.group(1).strip()
                    value_start = character_offset + leading + match.start(1)
                    prefix = text[:value_start].encode("utf-8")
                    return value, len(prefix), len(prefix) + len(value.encode("utf-8"))
            character_offset += len(line)
        return None


def get_extractor(name: str) -> ExtractionProvider:
    if name == "mock":
        return MockInvoiceExtractor()
    if name == "local":
        from .local_extraction import LocalInvoiceExtractor

        return LocalInvoiceExtractor.from_environment()
    raise ValueError(f"Unsupported extraction provider: {name}")
