"""Derived Arabic matching views; source strings and byte offsets stay authoritative."""
import re
import unicodedata
from dataclasses import dataclass

POLICY_VERSION = "arabic-evidence-v1"
_DIGITS = str.maketrans("٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹", "01234567890123456789")
_ALEF = str.maketrans("أإآٱ", "اااا")


def has_arabic(text: str) -> bool:
    return any("ARABIC" in unicodedata.name(c, "") and unicodedata.category(c).startswith("L")
               for c in text)


def script_language(text: str) -> str:
    return "ar-en" if has_arabic(text) and re.search(r"[A-Za-z]", text) else (
        "ar" if has_arabic(text) else "en")


@dataclass(frozen=True)
class MatchingView:
    text: str
    starts: tuple[int, ...]
    ends: tuple[int, ...]

    def source_span(self, start: int, end: int) -> tuple[int, int]:
        if not 0 <= start < end <= len(self.text):
            raise ValueError("A nonempty matching span is required")
        return self.starts[start], self.ends[end - 1]


def matching_view(source: str) -> MatchingView:
    text, starts, ends = [], [], []
    for index, char in enumerate(source):
        # Compatibility forms may expand (for example lam-alef). Map every
        # expansion back to its original character, without rewriting evidence.
        mapped = unicodedata.normalize("NFKC", char).translate(_ALEF)
        for item in mapped:
            if item == "ـ" or (unicodedata.category(item) == "Mn"
                               and "ARABIC" in unicodedata.name(item, "")):
                if ends:
                    ends[-1] = index + 1
                continue
            text.append(item)
            starts.append(index)
            ends.append(index + 1)
    return MatchingView("".join(text), tuple(starts), tuple(ends))


def normalize_value(value: str, value_type: str) -> str:
    if value_type in {"Decimal", "Identifier", "Date"}:
        value = value.translate(_DIGITS)
    if value_type == "Decimal":
        return value.replace("٬", "").replace(",", "").replace("٫", ".")
    if value_type == "Currency":
        if value.strip() in {"درهم", "درهم إماراتي", "درهم اماراتي", "د.إ", "د.إ."}:
            return "AED"
        return value.upper()
    return value


def normalize_supplier_number(value: str) -> str | None:
    """Bounded English-digit amount syntax; never reinterpret a malformed source."""
    if not re.fullmatch(r"(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)(?:\.[0-9]{1,6})?", value):
        return None
    normalized = value.replace(",", "")
    if len(normalized.split(".", 1)[0]) > 18:
        return None
    return normalized
