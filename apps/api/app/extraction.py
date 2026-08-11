import re
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol


@dataclass(frozen=True)
class FieldResult:
    name: str
    value: str
    confidence: float
    page: int = 1


@dataclass(frozen=True)
class ExtractionResult:
    document_type: str
    language: str
    extractor: str
    fields: list[FieldResult]


class ExtractionProvider(Protocol):
    def extract(self, path: Path, content_type: str) -> ExtractionResult: ...


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
        text = path.read_text(encoding="utf-8", errors="ignore") if content_type == "text/plain" else ""
        fields: list[FieldResult] = []
        for name, labels in self.labels.items():
            value = self._find(text, labels)
            if value:
                fields.append(FieldResult(name=name, value=value, confidence=0.99))
        has_arabic = bool(re.search(r"[\u0600-\u06ff]", text))
        has_latin = bool(re.search(r"[A-Za-z]", text))
        language = "ar-en" if has_arabic and has_latin else "ar" if has_arabic else "en"
        return ExtractionResult("invoice", language, self.name, fields)

    @staticmethod
    def _find(text: str, labels: tuple[str, ...]) -> str | None:
        for line in text.splitlines():
            for label in labels:
                match = re.match(rf"^{re.escape(label)}\s*:\s*(.+)$", line.strip(), re.I)
                if match:
                    return match.group(1).strip()
        return None


def get_extractor(name: str) -> ExtractionProvider:
    if name != "mock":
        raise ValueError(f"Unsupported extraction provider: {name}")
    return MockInvoiceExtractor()
