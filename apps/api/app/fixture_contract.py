"""Strict validation for Phase 2 fixture expectations and provider goldens.

The fixture format is intentionally separate from the public extraction response.
Expectations omit provider-dependent values while valid provider goldens exercise the
complete normalized response/error envelope.
"""

from __future__ import annotations

import hashlib
import json
import math
import re
import unicodedata
from pathlib import Path
from typing import Any


SCHEMA_VERSION = "2.0"
MAX_FILE_BYTES = 10 * 1024 * 1024
MAX_TOTAL_BYTES = 25 * 1024 * 1024
MAX_NORMALIZED_RESULT_BYTES = 5 * 1024 * 1024
MAX_PAGES = 50
MAX_PAGE_TEXT_BYTES = 1024 * 1024
MAX_FIELDS = 500
MAX_EVIDENCE_PER_FIELD = 10
MAX_WARNINGS = 100

ERROR_CODES = {
    "unsupported_schema_version",
    "invalid_storage_key",
    "content_integrity_mismatch",
    "input_too_large",
    "page_limit_exceeded",
    "unsupported_media_type",
    "corrupt_document",
    "encrypted_document",
    "ocr_unavailable",
    "provider_configuration_error",
    "provider_authentication_failed",
    "provider_rejected",
    "malformed_provider_result",
    "provider_network_error",
    "provider_timeout",
    "provider_throttled",
    "provider_unavailable",
    "request_cancelled",
    "internal_extraction_error",
}
RETRYABLE_ERROR_CODES = {
    "provider_network_error",
    "provider_timeout",
    "provider_throttled",
    "provider_unavailable",
}
SAFE_CODE = re.compile(r"^[a-z][a-z0-9_]{0,99}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")


class ContractValidationError(ValueError):
    """Raised when a fixture contract violates a stable Phase 2 invariant."""


def _fail(path: str, message: str) -> None:
    raise ContractValidationError(f"{path}: {message}")


def _mapping(value: Any, path: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        _fail(path, "must be an object")
    return value


def _list(value: Any, path: str) -> list[Any]:
    if not isinstance(value, list):
        _fail(path, "must be an array")
    return value


def _string(value: Any, path: str, *, non_empty: bool = True) -> str:
    if not isinstance(value, str) or (non_empty and not value):
        _fail(path, "must be a non-empty string")
    return value


def _integer(value: Any, path: str, *, minimum: int = 0) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        _fail(path, f"must be an integer >= {minimum}")
    return value


def _number(value: Any, path: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        _fail(path, "must be a number")
    numeric = float(value)
    if not math.isfinite(numeric):
        _fail(path, "must be finite")
    return numeric


def _required(payload: dict[str, Any], required: set[str], path: str) -> None:
    missing = required.difference(payload)
    if missing:
        _fail(path, f"missing required properties: {', '.join(sorted(missing))}")


def _only(payload: dict[str, Any], allowed: set[str], path: str) -> None:
    unknown = set(payload).difference(allowed)
    if unknown:
        _fail(path, f"unknown properties: {', '.join(sorted(unknown))}")


def _schema_version(payload: dict[str, Any], path: str) -> None:
    version = _string(payload.get("schema_version"), f"{path}.schema_version")
    if version != SCHEMA_VERSION:
        _fail(f"{path}.schema_version", f"unsupported version {version!r}")


def _validate_page(page: Any, path: str) -> tuple[int, str]:
    value = _mapping(page, path)
    required = {"page_number", "canonical_text", "rotation_degrees"}
    _required(value, required, path)
    _only(value, required, path)
    number = _integer(value["page_number"], f"{path}.page_number", minimum=1)
    text = _string(value["canonical_text"], f"{path}.canonical_text", non_empty=False)
    if unicodedata.normalize("NFC", text) != text:
        _fail(f"{path}.canonical_text", "must use Unicode NFC")
    if text != text.replace("\r\n", "\n").replace("\r", "\n"):
        _fail(f"{path}.canonical_text", "must use LF newlines")
    if len(text.encode("utf-8")) > MAX_PAGE_TEXT_BYTES:
        _fail(f"{path}.canonical_text", "exceeds the 1 MiB UTF-8 limit")
    rotation = _integer(value["rotation_degrees"], f"{path}.rotation_degrees")
    if rotation not in {0, 90, 180, 270}:
        _fail(f"{path}.rotation_degrees", "must be 0, 90, 180, or 270")
    return number, text


def _validate_polygon(polygon: Any, path: str) -> None:
    points = _list(polygon, path)
    if len(points) != 4:
        _fail(path, "must contain exactly four points")
    normalized: list[tuple[float, float]] = []
    for index, point in enumerate(points):
        pair = _list(point, f"{path}[{index}]")
        if len(pair) != 2:
            _fail(f"{path}[{index}]", "must contain x and y")
        x = _number(pair[0], f"{path}[{index}][0]")
        y = _number(pair[1], f"{path}[{index}][1]")
        if not 0 <= x <= 1 or not 0 <= y <= 1:
            _fail(f"{path}[{index}]", "coordinates must be within [0,1]")
        normalized.append((x, y))

    top_left = normalized[0]
    if any((y < top_left[1] or (y == top_left[1] and x < top_left[0])) for x, y in normalized):
        _fail(path, "first point must be top-left")
    signed_area = sum(
        x1 * y2 - y1 * x2
        for (x1, y1), (x2, y2) in zip(normalized, normalized[1:] + normalized[:1])
    )
    if signed_area <= 0:
        _fail(path, "points must be clockwise in top-left-origin coordinates")


def _validate_evidence(evidence: Any, pages: dict[int, str], path: str) -> None:
    value = _mapping(evidence, path)
    allowed = {
        "page_number",
        "quote",
        "start_utf8",
        "end_utf8",
        "polygon",
        "coordinate_unit",
    }
    _required(value, allowed, path)
    _only(value, allowed, path)
    page_number = _integer(value["page_number"], f"{path}.page_number", minimum=1)
    if page_number not in pages:
        _fail(f"{path}.page_number", "must reference an expected page")
    quote = _string(value["quote"], f"{path}.quote")
    start = _integer(value["start_utf8"], f"{path}.start_utf8")
    end = _integer(value["end_utf8"], f"{path}.end_utf8")
    if end <= start:
        _fail(f"{path}.end_utf8", "must be greater than start_utf8")
    page_bytes = pages[page_number].encode("utf-8")
    try:
        selected = page_bytes[start:end].decode("utf-8")
    except UnicodeDecodeError as error:
        _fail(path, f"offsets must land on UTF-8 code-point boundaries: {error}")
    if selected != quote:
        _fail(path, "quote must exactly match the UTF-8 byte range")
    if value["coordinate_unit"] != "normalized":
        _fail(f"{path}.coordinate_unit", "must be 'normalized'")
    _validate_polygon(value["polygon"], f"{path}.polygon")


def _validate_field(field: Any, pages: dict[int, str], path: str) -> tuple[str, int]:
    value = _mapping(field, path)
    required = {
        "key",
        "occurrence",
        "value_type",
        "value",
        "normalized_value",
        "evidence",
    }
    _required(value, required, path)
    _only(value, required, path)
    key = _string(value["key"], f"{path}.key")
    if not SAFE_CODE.fullmatch(key):
        _fail(f"{path}.key", "must be a bounded snake-case key")
    occurrence = _integer(value["occurrence"], f"{path}.occurrence")
    if value["value_type"] not in {"String", "Decimal", "Date", "Currency", "Identifier"}:
        _fail(f"{path}.value_type", "is not a supported value type")
    _string(value["value"], f"{path}.value")
    normalized = value["normalized_value"]
    if normalized is not None:
        _string(normalized, f"{path}.normalized_value")
    evidence = _list(value["evidence"], f"{path}.evidence")
    if not 1 <= len(evidence) <= MAX_EVIDENCE_PER_FIELD:
        _fail(f"{path}.evidence", "must contain between 1 and 10 items")
    for index, item in enumerate(evidence):
        _validate_evidence(item, pages, f"{path}.evidence[{index}]")
    return key, occurrence


def _validate_warning(
    warning: Any,
    pages: dict[int, str],
    fields: set[tuple[str, int]],
    path: str,
) -> tuple[Any, ...]:
    value = _mapping(warning, path)
    required = {"code", "severity", "safe_summary", "page_number", "field_key", "occurrence"}
    _required(value, required, path)
    _only(value, required, path)
    code = _string(value["code"], f"{path}.code")
    if not SAFE_CODE.fullmatch(code):
        _fail(f"{path}.code", "must be a bounded snake-case code")
    severity = value["severity"]
    if severity not in {"info", "degraded"}:
        _fail(f"{path}.severity", "must be 'info' or 'degraded'")
    summary = _string(value["safe_summary"], f"{path}.safe_summary")
    if len(summary) > 300:
        _fail(f"{path}.safe_summary", "exceeds 300 characters")

    page_number = value["page_number"]
    if page_number is not None:
        page_number = _integer(page_number, f"{path}.page_number", minimum=1)
        if page_number not in pages:
            _fail(f"{path}.page_number", "must reference an expected page")
    field_key = value["field_key"]
    occurrence = value["occurrence"]
    if (field_key is None) != (occurrence is None):
        _fail(path, "field_key and occurrence must be supplied together")
    if field_key is not None:
        _string(field_key, f"{path}.field_key")
        occurrence = _integer(occurrence, f"{path}.occurrence")
        if (field_key, occurrence) not in fields:
            _fail(path, "field scope must reference an expected field")
    return code, severity, page_number, field_key, occurrence


def validate_result_expectation(payload: Any, path: str = "expectation") -> None:
    value = _mapping(payload, path)
    required = {
        "schema_version",
        "expectation_kind",
        "fixture_id",
        "provider_scope",
        "outcome",
        "document_type",
        "language",
        "pages",
        "fields",
        "warnings",
    }
    _required(value, required, path)
    _only(value, required, path)
    _schema_version(value, path)
    if value["expectation_kind"] != "normalized_result":
        _fail(f"{path}.expectation_kind", "must be 'normalized_result'")
    _string(value["fixture_id"], f"{path}.fixture_id")
    if value["provider_scope"] not in {"local", "mock"}:
        _fail(f"{path}.provider_scope", "must be 'local' or 'mock'")
    if value["outcome"] not in {"Ready", "Partial"}:
        _fail(f"{path}.outcome", "must be 'Ready' or 'Partial'")
    _string(value["document_type"], f"{path}.document_type")
    _string(value["language"], f"{path}.language")

    page_items = _list(value["pages"], f"{path}.pages")
    if not 1 <= len(page_items) <= MAX_PAGES:
        _fail(f"{path}.pages", "must contain between 1 and 50 pages")
    pages: dict[int, str] = {}
    for index, page in enumerate(page_items):
        number, text = _validate_page(page, f"{path}.pages[{index}]")
        if number in pages:
            _fail(f"{path}.pages[{index}].page_number", "must be unique")
        pages[number] = text
    if sorted(pages) != list(range(1, len(pages) + 1)):
        _fail(f"{path}.pages", "page numbers must be contiguous from 1")

    field_items = _list(value["fields"], f"{path}.fields")
    if not 1 <= len(field_items) <= MAX_FIELDS:
        _fail(f"{path}.fields", "must contain between 1 and 500 fields")
    fields: set[tuple[str, int]] = set()
    for index, field in enumerate(field_items):
        identity = _validate_field(field, pages, f"{path}.fields[{index}]")
        if identity in fields:
            _fail(f"{path}.fields[{index}]", "field identity must be unique")
        fields.add(identity)

    warning_items = _list(value["warnings"], f"{path}.warnings")
    if len(warning_items) > MAX_WARNINGS:
        _fail(f"{path}.warnings", "must contain at most 100 warnings")
    warnings: set[tuple[Any, ...]] = set()
    warning_order: list[tuple[Any, ...]] = []
    degraded = False
    for index, warning in enumerate(warning_items):
        identity = _validate_warning(warning, pages, fields, f"{path}.warnings[{index}]")
        if identity in warnings:
            _fail(f"{path}.warnings[{index}]", "warning identity must be unique")
        warnings.add(identity)
        warning_order.append(
            (
                0 if identity[1] == "info" else 1,
                identity[2] or 0,
                identity[3] or "",
                identity[4] if identity[4] is not None else -1,
                identity[0],
            )
        )
        degraded = degraded or identity[1] == "degraded"
    if warning_order != sorted(warning_order):
        _fail(f"{path}.warnings", "must use deterministic contract ordering")
    if value["outcome"] == "Ready" and degraded:
        _fail(path, "Ready expectations cannot contain degraded warnings")
    if value["outcome"] == "Partial" and not degraded:
        _fail(path, "Partial expectations require a degraded warning")


def validate_error_expectation(payload: Any, path: str = "expectation") -> None:
    value = _mapping(payload, path)
    required = {
        "schema_version",
        "expectation_kind",
        "fixture_id",
        "provider_scope",
        "error",
    }
    _required(value, required, path)
    _only(value, required, path)
    _schema_version(value, path)
    if value["expectation_kind"] != "safe_error":
        _fail(f"{path}.expectation_kind", "must be 'safe_error'")
    _string(value["fixture_id"], f"{path}.fixture_id")
    if value["provider_scope"] not in {"local", "mock"}:
        _fail(f"{path}.provider_scope", "must be 'local' or 'mock'")
    error = _mapping(value["error"], f"{path}.error")
    required_error = {"code", "retryable"}
    _required(error, required_error, f"{path}.error")
    _only(error, required_error, f"{path}.error")
    code = _string(error["code"], f"{path}.error.code")
    if code not in ERROR_CODES:
        _fail(f"{path}.error.code", "must be a stable Phase 2 error code")
    if not isinstance(error["retryable"], bool):
        _fail(f"{path}.error.retryable", "must be a boolean")
    if error["retryable"] != (code in RETRYABLE_ERROR_CODES):
        _fail(f"{path}.error.retryable", "does not match the error classification")


def validate_semantic_expectation(payload: Any, path: str = "expectation") -> None:
    """Validate provider-independent assertions for OCR-backed fixtures.

    OCR canonical text, confidence, offsets, and polygons are deliberately absent until
    P2-T04 pins the local OCR runtime and mapping implementation.
    """

    value = _mapping(payload, path)
    required = {
        "schema_version",
        "expectation_kind",
        "fixture_id",
        "provider_scope",
        "outcome",
        "document_type",
        "language",
        "expected_page_count",
        "fields",
        "required_warning_codes",
    }
    _required(value, required, path)
    _only(value, required, path)
    _schema_version(value, path)
    if value["expectation_kind"] != "semantic_result":
        _fail(f"{path}.expectation_kind", "must be 'semantic_result'")
    _string(value["fixture_id"], f"{path}.fixture_id")
    if value["provider_scope"] != "local":
        _fail(f"{path}.provider_scope", "semantic OCR expectations must target 'local'")
    if value["outcome"] not in {"Ready", "Partial"}:
        _fail(f"{path}.outcome", "must be 'Ready' or 'Partial'")
    _string(value["document_type"], f"{path}.document_type")
    _string(value["language"], f"{path}.language")
    page_count = _integer(
        value["expected_page_count"], f"{path}.expected_page_count", minimum=1
    )
    if page_count > MAX_PAGES:
        _fail(f"{path}.expected_page_count", "exceeds the 50 page limit")

    fields = _list(value["fields"], f"{path}.fields")
    if not 1 <= len(fields) <= MAX_FIELDS:
        _fail(f"{path}.fields", "must contain between 1 and 500 fields")
    identities: set[tuple[str, int]] = set()
    required_field = {"key", "occurrence", "value_type", "value", "normalized_value"}
    for index, field in enumerate(fields):
        field_path = f"{path}.fields[{index}]"
        item = _mapping(field, field_path)
        _required(item, required_field, field_path)
        _only(item, required_field, field_path)
        key = _string(item["key"], f"{field_path}.key")
        if not SAFE_CODE.fullmatch(key):
            _fail(f"{field_path}.key", "must be a bounded snake-case key")
        occurrence = _integer(item["occurrence"], f"{field_path}.occurrence")
        if (key, occurrence) in identities:
            _fail(field_path, "field identity must be unique")
        identities.add((key, occurrence))
        if item["value_type"] not in {
            "String",
            "Decimal",
            "Date",
            "Currency",
            "Identifier",
        }:
            _fail(f"{field_path}.value_type", "is not a supported value type")
        _string(item["value"], f"{field_path}.value")
        if item["normalized_value"] is not None:
            _string(item["normalized_value"], f"{field_path}.normalized_value")

    warning_codes = _list(value["required_warning_codes"], f"{path}.required_warning_codes")
    seen_codes: set[str] = set()
    for index, warning_code in enumerate(warning_codes):
        code = _string(warning_code, f"{path}.required_warning_codes[{index}]")
        if not SAFE_CODE.fullmatch(code) or code in seen_codes:
            _fail(f"{path}.required_warning_codes[{index}]", "must be a unique safe code")
        seen_codes.add(code)
    if value["outcome"] == "Ready" and warning_codes:
        _fail(path, "Ready semantic expectations cannot require degraded warnings")
    if value["outcome"] == "Partial" and not warning_codes:
        _fail(path, "Partial semantic expectations require a warning code")


def validate_expectation(payload: Any, path: str = "expectation") -> None:
    value = _mapping(payload, path)
    kind = value.get("expectation_kind")
    if kind == "normalized_result":
        validate_result_expectation(value, path)
    elif kind == "semantic_result":
        validate_semantic_expectation(value, path)
    elif kind == "safe_error":
        validate_error_expectation(value, path)
    else:
        _fail(
            f"{path}.expectation_kind",
            "must be 'normalized_result', 'semantic_result', or 'safe_error'",
        )


def _validate_provider(payload: Any, path: str) -> None:
    value = _mapping(payload, path)
    required = {"alias", "adapter_version", "model", "model_version"}
    _required(value, required, path)
    _only(value, required, path)
    for key in sorted(required):
        _string(value[key], f"{path}.{key}")


def _validate_metrics(payload: Any, expected_pages: int, path: str) -> None:
    value = _mapping(payload, path)
    required = {
        "provider_duration_ms",
        "page_count",
        "estimated_cost",
        "cost_currency",
        "price_basis",
    }
    _required(value, required, path)
    _only(value, required, path)
    _integer(value["provider_duration_ms"], f"{path}.provider_duration_ms")
    if _integer(value["page_count"], f"{path}.page_count", minimum=1) != expected_pages:
        _fail(f"{path}.page_count", "must match the result page count")
    estimated = value["estimated_cost"]
    if estimated is not None:
        if _number(estimated, f"{path}.estimated_cost") < 0:
            _fail(f"{path}.estimated_cost", "must be non-negative")
        currency = _string(value["cost_currency"], f"{path}.cost_currency")
        if not currency.strip() or len(currency) > 3:
            _fail(f"{path}.cost_currency", "must contain 1 to 3 non-whitespace characters")
        if value["price_basis"] is not None:
            price_basis = _string(value["price_basis"], f"{path}.price_basis")
            if not price_basis.strip() or len(price_basis) > 100:
                _fail(
                    f"{path}.price_basis",
                    "must contain 1 to 100 non-whitespace characters",
                )
    has_price_metadata = value["cost_currency"] is not None or value["price_basis"] is not None
    if estimated is None and has_price_metadata:
        _fail(path, "currency and price_basis require estimated_cost")


def validate_normalized_result(payload: Any, path: str = "result") -> None:
    value = _mapping(payload, path)
    required = {
        "schema_version",
        "outcome",
        "document_type",
        "language",
        "provider",
        "metrics",
        "pages",
        "fields",
        "warnings",
    }
    _required(value, required, path)
    _only(value, required, path)
    normalized_fields = []
    for index, field in enumerate(_list(value["fields"], f"{path}.fields")):
        field_value = _mapping(field, f"{path}.fields[{index}]")
        normalized_fields.append(
            {key: field_value[key] for key in field_value if key != "confidence"}
        )
    expectation = {
        "schema_version": value["schema_version"],
        "expectation_kind": "normalized_result",
        "fixture_id": "provider-golden",
        "provider_scope": "local",
        "outcome": value["outcome"],
        "document_type": value["document_type"],
        "language": value["language"],
        "pages": value["pages"],
        "fields": normalized_fields,
        "warnings": value["warnings"],
    }
    validate_result_expectation(expectation, path)
    for index, field in enumerate(value["fields"]):
        if "confidence" not in field:
            _fail(f"{path}.fields[{index}]", "missing required property: confidence")
        confidence = field["confidence"]
        if confidence is not None and not 0 <= _number(
            confidence, f"{path}.fields[{index}].confidence"
        ) <= 1:
            _fail(f"{path}.fields[{index}].confidence", "must be within [0,1]")
    _validate_provider(value["provider"], f"{path}.provider")
    _validate_metrics(value["metrics"], len(value["pages"]), f"{path}.metrics")


def validate_error_envelope(payload: Any, path: str = "error_envelope") -> None:
    value = _mapping(payload, path)
    required = {"schema_version", "error"}
    _required(value, required, path)
    _only(value, required, path)
    _schema_version(value, path)
    error = _mapping(value["error"], f"{path}.error")
    required_error = {"code", "retryable", "summary", "correlation_id"}
    _required(error, required_error, f"{path}.error")
    _only(error, required_error, f"{path}.error")
    code = _string(error["code"], f"{path}.error.code")
    if code not in ERROR_CODES:
        _fail(f"{path}.error.code", "must be a stable Phase 2 error code")
    if not isinstance(error["retryable"], bool):
        _fail(f"{path}.error.retryable", "must be a boolean")
    if error["retryable"] != (code in RETRYABLE_ERROR_CODES):
        _fail(f"{path}.error.retryable", "does not match the error classification")
    summary = _string(error["summary"], f"{path}.error.summary")
    if len(summary) > 300:
        _fail(f"{path}.error.summary", "exceeds 300 characters")
    correlation = _string(error["correlation_id"], f"{path}.error.correlation_id")
    if len(correlation) > 100 or not re.fullmatch(r"[A-Za-z0-9._:-]+", correlation):
        _fail(f"{path}.error.correlation_id", "must be a bounded safe identifier")


def validate_provider_fixture(payload: Any, path: str = "provider_fixture") -> None:
    value = _mapping(payload, path)
    required = {"fixture_schema_version", "expected_validation", "payload"}
    _required(value, required, path)
    _only(value, required, path)
    if value["fixture_schema_version"] != "1.0":
        _fail(f"{path}.fixture_schema_version", "unsupported fixture wrapper version")
    expected = value["expected_validation"]
    if expected == "accept_result":
        try:
            payload_bytes = json.dumps(
                value["payload"],
                ensure_ascii=False,
                allow_nan=False,
                separators=(",", ":"),
            ).encode("utf-8")
        except (TypeError, ValueError) as error:
            _fail(f"{path}.payload", f"must be finite JSON: {error}")
        if len(payload_bytes) > MAX_NORMALIZED_RESULT_BYTES:
            _fail(f"{path}.payload", "exceeds the 5 MiB normalized-result limit")
        validate_normalized_result(value["payload"], f"{path}.payload")
    elif expected == "accept_error":
        validate_error_envelope(value["payload"], f"{path}.payload")
    elif expected == "reject":
        try:
            validate_normalized_result(value["payload"], f"{path}.payload")
        except ContractValidationError:
            return
        _fail(f"{path}.payload", "was expected to fail normalized-result validation")
    else:
        _fail(
            f"{path}.expected_validation",
            "must be 'accept_result', 'accept_error', or 'reject'",
        )


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def validate_source_register(root: Path) -> dict[str, Any]:
    register_path = root / "source-license-register.json"
    register = _mapping(json.loads(register_path.read_text(encoding="utf-8")), "register")
    required = {
        "register_schema_version",
        "generator",
        "canonical_environment",
        "sources",
        "tooling",
        "dependencies",
        "artifacts",
        "total_bytes",
    }
    _required(register, required, "register")
    _only(register, required, "register")
    if register["register_schema_version"] != "1.0":
        _fail("register.register_schema_version", "unsupported register version")
    generator = _mapping(register["generator"], "register.generator")
    _required(generator, {"name", "version", "source_sha256"}, "register.generator")
    _only(generator, {"name", "version", "source_sha256"}, "register.generator")
    for key in ("name", "version"):
        _string(generator[key], f"register.generator.{key}")
    generator_hash = _string(generator["source_sha256"], "register.generator.source_sha256")
    if not SHA256.fullmatch(generator_hash):
        _fail("register.generator.source_sha256", "must be a lowercase SHA-256")
    environment = _mapping(register["canonical_environment"], "register.canonical_environment")
    environment_fields = {"container_image", "platform", "python", "locale", "timezone"}
    _required(environment, environment_fields, "register.canonical_environment")
    _only(environment, environment_fields, "register.canonical_environment")
    for key in sorted(environment_fields):
        _string(environment[key], f"register.canonical_environment.{key}")
    sources = _list(register["sources"], "register.sources")
    dependencies = _list(register["dependencies"], "register.dependencies")
    tooling = _list(register["tooling"], "register.tooling")
    if not sources or not tooling or not dependencies:
        _fail("register", "sources, tooling, and dependencies must not be empty")
    for index, source in enumerate(sources):
        source_path = f"register.sources[{index}]"
        item = _mapping(source, source_path)
        fields = {"source_id", "path", "sha256", "license", "fictional"}
        _required(item, fields, source_path)
        _only(item, fields, source_path)
        for key in ("source_id", "path", "license"):
            _string(item[key], f"{source_path}.{key}")
        if not SHA256.fullmatch(_string(item["sha256"], f"{source_path}.sha256")):
            _fail(f"{source_path}.sha256", "must be a lowercase SHA-256")
        if item["fictional"] is not True:
            _fail(f"{source_path}.fictional", "must explicitly be true")
    for index, tool in enumerate(tooling):
        tool_path = f"register.tooling[{index}]"
        item = _mapping(tool, tool_path)
        fields = {"path", "sha256", "license"}
        _required(item, fields, tool_path)
        _only(item, fields, tool_path)
        _string(item["path"], f"{tool_path}.path")
        _string(item["license"], f"{tool_path}.license")
        if not SHA256.fullmatch(_string(item["sha256"], f"{tool_path}.sha256")):
            _fail(f"{tool_path}.sha256", "must be a lowercase SHA-256")
    for index, dependency in enumerate(dependencies):
        dependency_path = f"register.dependencies[{index}]"
        item = _mapping(dependency, dependency_path)
        required_dependency = {"name", "version", "source", "license"}
        _required(item, required_dependency, dependency_path)
        _only(item, required_dependency | {"sha256"}, dependency_path)
        for key in sorted(required_dependency):
            _string(item[key], f"{dependency_path}.{key}")
        if "sha256" in item and not SHA256.fullmatch(
            _string(item["sha256"], f"{dependency_path}.sha256")
        ):
            _fail(f"{dependency_path}.sha256", "must be a lowercase SHA-256")

    artifacts = _list(register["artifacts"], "register.artifacts")
    if not artifacts:
        _fail("register.artifacts", "must not be empty")
    seen: set[str] = set()
    total_bytes = 0
    for index, artifact in enumerate(artifacts):
        path = f"register.artifacts[{index}]"
        item = _mapping(artifact, path)
        required_artifact = {
            "path",
            "kind",
            "media_type",
            "purpose",
            "bytes",
            "sha256",
            "license",
        }
        _required(item, required_artifact, path)
        _only(item, required_artifact, path)
        relative = _string(item["path"], f"{path}.path")
        relative_path = Path(relative)
        if relative_path.is_absolute() or ".." in relative_path.parts or "\\" in relative:
            _fail(f"{path}.path", "must be a safe POSIX relative path")
        if relative in seen:
            _fail(f"{path}.path", "must be unique")
        seen.add(relative)
        file_path = (root / relative_path).resolve()
        if root.resolve() not in file_path.parents:
            _fail(f"{path}.path", "must remain beneath the fixture root")
        if not file_path.is_file() or file_path.is_symlink():
            _fail(f"{path}.path", "must reference a regular non-symlink file")
        expected_bytes = _integer(item["bytes"], f"{path}.bytes", minimum=1)
        if expected_bytes > MAX_FILE_BYTES:
            _fail(f"{path}.bytes", "exceeds the 10 MiB fixture limit")
        if file_path.stat().st_size != expected_bytes:
            _fail(f"{path}.bytes", "does not match the artifact")
        expected_hash = _string(item["sha256"], f"{path}.sha256")
        if not SHA256.fullmatch(expected_hash) or sha256_file(file_path) != expected_hash:
            _fail(f"{path}.sha256", "does not match the artifact")
        _string(item["kind"], f"{path}.kind")
        _string(item["media_type"], f"{path}.media_type")
        _string(item["purpose"], f"{path}.purpose")
        _string(item["license"], f"{path}.license")
        total_bytes += expected_bytes

        if item["kind"] == "expectation":
            validate_expectation(json.loads(file_path.read_text(encoding="utf-8")), relative)
        elif item["kind"] == "provider_fixture":
            validate_provider_fixture(json.loads(file_path.read_text(encoding="utf-8")), relative)
        elif item["kind"] == "document_fixture" and item["media_type"] == "application/pdf":
            content = file_path.read_bytes()
            if not content.startswith(b"%PDF-"):
                _fail(f"{path}.path", "PDF fixture must have a PDF signature")
            forbidden = (
                b"/JavaScript",
                b"/JS ",
                b"/Launch",
                b"/EmbeddedFile",
                b"/OpenAction",
                b"/RichMedia",
            )
            if any(marker in content for marker in forbidden):
                _fail(f"{path}.path", "PDF fixtures cannot contain active content")

    if total_bytes > MAX_TOTAL_BYTES:
        _fail("register.artifacts", "combined artifacts exceed the 25 MiB repository budget")
    if register["total_bytes"] != total_bytes:
        _fail("register.total_bytes", "does not match the artifact total")

    expected_files = seen | {"source-license-register.json"}
    actual_files: set[str] = set()
    for entry in root.rglob("*"):
        relative = entry.relative_to(root).as_posix()
        if entry.is_symlink():
            _fail("register.artifacts", f"symlink is not allowed: {relative}")
        if entry.is_file():
            actual_files.add(relative)
    extra_files = actual_files.difference(expected_files)
    missing_files = expected_files.difference(actual_files)
    if extra_files:
        _fail(
            "register.artifacts",
            f"unregistered files are not allowed: {', '.join(sorted(extra_files))}",
        )
    if missing_files:
        _fail(
            "register.artifacts",
            f"registered files are missing: {', '.join(sorted(missing_files))}",
        )
    return register
