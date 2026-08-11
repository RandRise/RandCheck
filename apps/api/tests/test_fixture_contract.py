import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from app.fixture_contract import (
    ContractValidationError,
    validate_expectation,
    validate_normalized_result,
    validate_provider_fixture,
    validate_source_register,
)


def normalized_expectation(outcome: str = "Ready") -> dict:
    warnings = []
    if outcome == "Partial":
        warnings = [
            {
                "code": "page_ocr_degraded",
                "severity": "degraded",
                "safe_summary": "One page produced incomplete OCR text.",
                "page_number": 1,
                "field_key": None,
                "occurrence": None,
            }
        ]
    return {
        "schema_version": "2.0",
        "expectation_kind": "normalized_result",
        "fixture_id": "synthetic-native-invoice",
        "provider_scope": "local",
        "outcome": outcome,
        "document_type": "invoice",
        "language": "en",
        "pages": [
            {"page_number": 1, "canonical_text": "Aé ض", "rotation_degrees": 0}
        ],
        "fields": [
            {
                "key": "invoice_number",
                "occurrence": 0,
                "value_type": "String",
                "value": "é ض",
                "normalized_value": "é ض",
                "evidence": [
                    {
                        "page_number": 1,
                        "quote": "é ض",
                        "start_utf8": 1,
                        "end_utf8": 6,
                        "polygon": [[0.1, 0.2], [0.8, 0.2], [0.8, 0.4], [0.1, 0.4]],
                        "coordinate_unit": "normalized",
                    }
                ],
            }
        ],
        "warnings": warnings,
    }


def provider_result() -> dict:
    expectation = normalized_expectation()
    fields = copy.deepcopy(expectation["fields"])
    fields[0]["confidence"] = None
    return {
        "schema_version": "2.0",
        "outcome": "Ready",
        "document_type": "invoice",
        "language": "en",
        "provider": {
            "alias": "local",
            "adapter_version": "pinned",
            "model": "synthetic-model",
            "model_version": "pinned",
        },
        "metrics": {
            "provider_duration_ms": 0,
            "page_count": 1,
            "estimated_cost": None,
            "cost_currency": None,
            "price_basis": None,
        },
        "pages": copy.deepcopy(expectation["pages"]),
        "fields": fields,
        "warnings": [],
    }


def semantic_expectation() -> dict:
    return {
        "schema_version": "2.0",
        "expectation_kind": "semantic_result",
        "fixture_id": "synthetic-scanned-invoice",
        "provider_scope": "local",
        "outcome": "Ready",
        "document_type": "invoice",
        "language": "en",
        "expected_page_count": 1,
        "fields": [
            {
                "key": "invoice_number",
                "occurrence": 0,
                "value_type": "String",
                "value": "INV-AE-001",
                "normalized_value": "INV-AE-001",
            }
        ],
        "required_warning_codes": [],
    }


class FixtureContractTests(unittest.TestCase):
    @staticmethod
    def _provider_result_with_compact_size(target_bytes: int) -> dict:
        max_page_bytes = 1024 * 1024
        payload = provider_result()
        first_text = payload["pages"][0]["canonical_text"]
        payload["pages"] = [
            {
                "page_number": 1,
                "canonical_text": first_text
                + "x" * (max_page_bytes - len(first_text.encode("utf-8"))),
                "rotation_degrees": 0,
            },
            *[
                {
                    "page_number": page_number,
                    "canonical_text": "x" * max_page_bytes,
                    "rotation_degrees": 0,
                }
                for page_number in range(2, 5)
            ],
            {"page_number": 5, "canonical_text": "", "rotation_degrees": 0},
        ]
        payload["metrics"]["page_count"] = 5
        def compact(value: dict) -> bytes:
            return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode(
                "utf-8"
            )
        remaining = target_bytes - len(compact(payload))
        if not 0 <= remaining <= max_page_bytes:
            raise AssertionError("target cannot be represented by a valid result")
        payload["pages"][-1]["canonical_text"] = "x" * remaining
        if len(compact(payload)) != target_bytes:
            raise AssertionError("payload size tuning was not exact")
        return payload

    def test_normalized_expectation_accepts_multibyte_utf8_byte_offsets(self):
        validate_expectation(normalized_expectation())

    def test_expectation_rejects_unknown_schema_version(self):
        payload = normalized_expectation()
        payload["schema_version"] = "3.0"

        with self.assertRaisesRegex(ContractValidationError, "unsupported version"):
            validate_expectation(payload)

    def test_expectation_rejects_decomposed_non_nfc_canonical_text(self):
        payload = normalized_expectation()
        payload["pages"][0]["canonical_text"] = "Ae\u0301 ض"

        with self.assertRaisesRegex(ContractValidationError, "Unicode NFC"):
            validate_expectation(payload)

    def test_expectation_rejects_utf8_quote_offset_mismatch_and_split_code_point(self):
        mismatch = normalized_expectation()
        mismatch["fields"][0]["evidence"][0]["quote"] = "wrong"
        with self.assertRaisesRegex(ContractValidationError, "quote must exactly match"):
            validate_expectation(mismatch)

        split = normalized_expectation()
        split["fields"][0]["evidence"][0].update(
            {"quote": "é", "start_utf8": 2, "end_utf8": 3}
        )
        with self.assertRaisesRegex(ContractValidationError, "code-point boundaries"):
            validate_expectation(split)

    def test_expectation_rejects_malformed_or_noncanonical_polygon(self):
        three_points = normalized_expectation()
        three_points["fields"][0]["evidence"][0]["polygon"] = [
            [0, 0], [1, 0], [1, 1]
        ]
        with self.assertRaisesRegex(ContractValidationError, "exactly four points"):
            validate_expectation(three_points)

        counter_clockwise = normalized_expectation()
        counter_clockwise["fields"][0]["evidence"][0]["polygon"] = [
            [0, 0], [0, 1], [1, 1], [1, 0]
        ]
        with self.assertRaisesRegex(ContractValidationError, "clockwise"):
            validate_expectation(counter_clockwise)

    def test_ready_and_partial_warning_rules_are_enforced(self):
        ready = normalized_expectation()
        ready["warnings"] = normalized_expectation("Partial")["warnings"]
        with self.assertRaisesRegex(ContractValidationError, "Ready expectations"):
            validate_expectation(ready)

        partial = normalized_expectation("Partial")
        partial["warnings"] = []
        with self.assertRaisesRegex(ContractValidationError, "Partial expectations"):
            validate_expectation(partial)

        validate_expectation(normalized_expectation("Partial"))

    def test_expectation_rejects_non_deterministic_warning_order(self):
        payload = normalized_expectation("Partial")
        payload["warnings"].append(
            {
                "code": "document_note",
                "severity": "info",
                "safe_summary": "Document-level informational warning.",
                "page_number": None,
                "field_key": None,
                "occurrence": None,
            }
        )

        with self.assertRaisesRegex(ContractValidationError, "deterministic"):
            validate_expectation(payload)

    def test_exact_expectation_value_types_match_dotnet_field_value_type(self):
        for value_type in ("String", "Decimal", "Date", "Currency", "Identifier"):
            with self.subTest(value_type=value_type):
                payload = normalized_expectation()
                payload["fields"][0]["value_type"] = value_type
                validate_expectation(payload)

        for value_type in ("Integer", "Boolean"):
            with self.subTest(value_type=value_type):
                payload = normalized_expectation()
                payload["fields"][0]["value_type"] = value_type
                with self.assertRaisesRegex(
                    ContractValidationError, "supported value type"
                ):
                    validate_expectation(payload)

    def test_semantic_expectation_value_types_match_dotnet_field_value_type(self):
        for value_type in ("String", "Decimal", "Date", "Currency", "Identifier"):
            with self.subTest(value_type=value_type):
                payload = semantic_expectation()
                payload["fields"][0]["value_type"] = value_type
                validate_expectation(payload)

        for value_type in ("Integer", "Boolean"):
            with self.subTest(value_type=value_type):
                payload = semantic_expectation()
                payload["fields"][0]["value_type"] = value_type
                with self.assertRaisesRegex(
                    ContractValidationError, "supported value type"
                ):
                    validate_expectation(payload)

    def test_intentionally_invalid_provider_fixture_must_reject_only_invalid_payload(self):
        invalid = provider_result()
        invalid["fields"][0]["evidence"][0]["end_utf8"] = 5
        validate_provider_fixture(
            {
                "fixture_schema_version": "1.0",
                "expected_validation": "reject",
                "payload": invalid,
            }
        )

        with self.assertRaisesRegex(ContractValidationError, "expected to fail"):
            validate_provider_fixture(
                {
                    "fixture_schema_version": "1.0",
                    "expected_validation": "reject",
                    "payload": provider_result(),
                }
            )

    def test_accept_result_size_limit_counts_compact_payload_not_wrapper(self):
        limit = 5 * 1024 * 1024
        at_limit = self._provider_result_with_compact_size(limit)
        wrapper = {
            "fixture_schema_version": "1.0",
            "expected_validation": "accept_result",
            "payload": at_limit,
        }
        compact_wrapper_size = len(
            json.dumps(wrapper, ensure_ascii=False, separators=(",", ":")).encode(
                "utf-8"
            )
        )
        self.assertGreater(compact_wrapper_size, limit)
        validate_provider_fixture(wrapper)

        above_limit = copy.deepcopy(at_limit)
        above_limit["pages"][-1]["canonical_text"] += "x"
        wrapper["payload"] = above_limit
        with self.assertRaisesRegex(ContractValidationError, "5 MiB"):
            validate_provider_fixture(wrapper)

    def test_provider_metrics_reject_non_finite_numbers(self):
        for non_finite in (float("nan"), float("inf"), float("-inf")):
            with self.subTest(value=non_finite):
                payload = provider_result()
                payload["metrics"].update(
                    {"estimated_cost": non_finite, "cost_currency": "AED"}
                )
                with self.assertRaisesRegex(ContractValidationError, "finite"):
                    validate_normalized_result(payload)

    def test_provider_metrics_require_bounded_currency_when_cost_is_present(self):
        valid = provider_result()
        valid["metrics"].update({"estimated_cost": 0.01, "cost_currency": "a"})
        validate_normalized_result(valid)

        for currency in (None, "   ", "AEDX"):
            with self.subTest(currency=currency):
                payload = provider_result()
                payload["metrics"].update(
                    {"estimated_cost": 0.01, "cost_currency": currency}
                )
                with self.assertRaisesRegex(ContractValidationError, "cost_currency"):
                    validate_normalized_result(payload)

    def test_provider_metrics_forbid_price_metadata_without_cost(self):
        for metadata in (
            {"cost_currency": "AED"},
            {"price_basis": "per page"},
        ):
            with self.subTest(metadata=metadata):
                payload = provider_result()
                payload["metrics"].update(metadata)
                with self.assertRaisesRegex(
                    ContractValidationError, "require estimated_cost"
                ):
                    validate_normalized_result(payload)

    def test_provider_metrics_bound_optional_price_basis(self):
        valid = provider_result()
        valid["metrics"].update(
            {
                "estimated_cost": 0.01,
                "cost_currency": "AED",
                "price_basis": "x" * 100,
            }
        )
        validate_normalized_result(valid)

        invalid = provider_result()
        invalid["metrics"].update(
            {
                "estimated_cost": 0.01,
                "cost_currency": "AED",
                "price_basis": "x" * 101,
            }
        )
        with self.assertRaisesRegex(ContractValidationError, "price_basis"):
            validate_normalized_result(invalid)

    def test_source_register_validates_license_size_and_sha256(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            label = root / "expected.json"
            label.write_text(json.dumps(normalized_expectation()), encoding="utf-8")
            register = self._write_register(root, label)

            validated = validate_source_register(root)

            self.assertEqual(register["total_bytes"], validated["total_bytes"])

    def test_source_register_rejects_missing_license_and_artifact_tampering(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            label = root / "expected.json"
            label.write_text(json.dumps(normalized_expectation()), encoding="utf-8")
            register = self._write_register(root, label)
            register["artifacts"][0]["license"] = ""
            (root / "source-license-register.json").write_text(
                json.dumps(register), encoding="utf-8"
            )
            with self.assertRaisesRegex(ContractValidationError, "license"):
                validate_source_register(root)

            register = self._write_register(root, label)
            label.write_text(label.read_text(encoding="utf-8") + " ", encoding="utf-8")
            with self.assertRaisesRegex(ContractValidationError, "does not match the artifact"):
                validate_source_register(root)

    def test_source_register_rejects_unregistered_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            label = root / "expected.json"
            label.write_text(json.dumps(normalized_expectation()), encoding="utf-8")
            self._write_register(root, label)

            validate_source_register(root)
            (root / "unregistered.txt").write_text("not registered", encoding="utf-8")

            with self.assertRaisesRegex(ContractValidationError, "unregistered"):
                validate_source_register(root)

    def test_source_register_rejects_symlinks_beneath_fixture_root(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            label = root / "expected.json"
            label.write_text(json.dumps(normalized_expectation()), encoding="utf-8")
            self._write_register(root, label)
            link = root / "linked.expected.json"
            try:
                link.symlink_to(label.name)
            except OSError:
                # Standard Windows accounts cannot create symlinks unless Developer
                # Mode is enabled. Exercise the same traversal branch in that case.
                with (
                    patch.object(Path, "rglob", return_value=[link]),
                    patch.object(
                        Path,
                        "is_symlink",
                        autospec=True,
                        side_effect=lambda path: path == link,
                    ),
                    self.assertRaisesRegex(ContractValidationError, "symlink"),
                ):
                    validate_source_register(root)
            else:
                with self.assertRaisesRegex(ContractValidationError, "symlink"):
                    validate_source_register(root)

    @staticmethod
    def _write_register(root: Path, artifact: Path) -> dict:
        content = artifact.read_bytes()
        register = {
            "register_schema_version": "1.0",
            "generator": {
                "name": "phase2-fixtures",
                "version": "1.0",
                "source_sha256": "0" * 64,
            },
            "canonical_environment": {
                "container_image": "python:3.11-slim",
                "platform": "linux/amd64",
                "python": "3.11",
                "timezone": "UTC",
                "locale": "C.UTF-8",
            },
            "sources": [
                {
                    "source_id": "fixture-test-source",
                    "path": "synthetic-test-source.json",
                    "sha256": "0" * 64,
                    "license": "CC0-1.0",
                    "fictional": True,
                }
            ],
            "tooling": [
                {
                    "path": "tools/fixture-test-tool.py",
                    "sha256": "0" * 64,
                    "license": "CC0-1.0",
                }
            ],
            "dependencies": [
                {
                    "name": "python",
                    "version": "3.11",
                    "source": "https://www.python.org/",
                    "license": "PSF-2.0",
                }
            ],
            "artifacts": [
                {
                    "path": artifact.name,
                    "kind": "expectation",
                    "media_type": "application/json",
                    "purpose": "Schema 2.0 contract expectation",
                    "bytes": len(content),
                    "sha256": hashlib.sha256(content).hexdigest(),
                    "license": "CC0-1.0",
                }
            ],
            "total_bytes": len(content),
        }
        (root / "source-license-register.json").write_text(
            json.dumps(register), encoding="utf-8"
        )
        return register


if __name__ == "__main__":
    unittest.main()
