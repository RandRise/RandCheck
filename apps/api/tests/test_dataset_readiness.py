import copy
import json
from pathlib import Path
import unittest

from app.dataset_readiness import (
    audit_dataset_readiness,
    canonical_sha256,
    validate_annotation_ledger,
    validate_source_register,
)
from app.evaluation import EvaluationValidationError, validate_dataset


REPO_ROOT = Path(__file__).resolve().parents[3]
SMOKE_ROOT = REPO_ROOT / "samples" / "phase-3" / "smoke-v0.1.0"


def load_smoke() -> tuple[dict, dict, dict]:
    return tuple(
        json.loads((SMOKE_ROOT / filename).read_text(encoding="utf-8"))
        for filename in (
            "dataset.json",
            "source-register.json",
            "annotation-ledger.json",
        )
    )


def structurally_ready_inputs() -> tuple[dict, dict, dict]:
    dataset, register, ledger = copy.deepcopy(load_smoke())
    dataset["claim_eligible"] = True
    dataset.pop("claim_exclusion_reason")
    for sample in dataset["samples"]:
        sample["source"]["synthetic"] = False
    for source in register["sources"]:
        source["provenance_type"] = "written_permission"
        source["rights"]["reference"] = "test-only-permission-reference"
        source["storage"].update(
            {
                "classification": "restricted",
                "committed_to_git": False,
                "location_reference": "test-only-controlled-storage",
            }
        )
    return dataset, register, ledger


class DatasetReadinessTests(unittest.TestCase):
    def test_committed_smoke_is_valid_but_explicitly_not_claim_ready(self):
        audit = audit_dataset_readiness(*load_smoke())

        self.assertFalse(audit["claim_ready"])
        self.assertEqual(3, audit["blocker_count"])
        self.assertEqual(
            ["dataset_not_claim_eligible", "synthetic_source", "synthetic_source"],
            [blocker["code"] for blocker in audit["blockers"]],
        )
        self.assertTrue(all(audit["checks"].values()))

    def test_structurally_ready_rights_privacy_and_annotations_pass(self):
        audit = audit_dataset_readiness(*structurally_ready_inputs())

        self.assertTrue(audit["claim_ready"])
        self.assertEqual(0, audit["blocker_count"])
        self.assertEqual([], audit["blockers"])

    def test_missing_right_and_restricted_git_source_are_blockers(self):
        dataset, register, ledger = structurally_ready_inputs()
        register["sources"][0]["rights"]["publish_aggregate_results"] = False
        register["sources"][0]["storage"]["committed_to_git"] = True

        audit = audit_dataset_readiness(dataset, register, ledger)
        codes = {blocker["code"] for blocker in audit["blockers"]}

        self.assertFalse(audit["claim_ready"])
        self.assertEqual(
            {"rights_publish_aggregate_results_missing", "restricted_source_in_git"},
            codes,
        )

    def test_sensitive_data_requires_completed_redaction(self):
        dataset, register, ledger = structurally_ready_inputs()
        privacy = register["sources"][0]["privacy"]
        privacy["contains_personal_data"] = True
        privacy["redaction_status"] = "pending"

        audit = audit_dataset_readiness(dataset, register, ledger)
        codes = {blocker["code"] for blocker in audit["blockers"]}

        self.assertEqual(
            {"privacy_review_incomplete", "sensitive_data_not_redacted"}, codes
        )

    def test_annotation_requires_independent_reviewer_and_matching_final_hash(self):
        dataset, _, ledger = structurally_ready_inputs()
        validated_dataset = validate_dataset(dataset)
        record = ledger["records"][0]
        record["independent_verification"]["reviewer_id"] = record[
            "initial_annotation"
        ]["reviewer_id"]
        with self.assertRaisesRegex(EvaluationValidationError, "must be different"):
            validate_annotation_ledger(ledger, validated_dataset)

        _, _, ledger = structurally_ready_inputs()
        ledger["records"][0]["final_fields_canonical_sha256"] = "0" * 64
        with self.assertRaisesRegex(EvaluationValidationError, "does not match"):
            validate_annotation_ledger(ledger, validated_dataset)

    def test_changes_required_needs_adjudication(self):
        dataset, _, ledger = structurally_ready_inputs()
        record = ledger["records"][0]
        record["independent_verification"]["disposition"] = "changes_required"

        with self.assertRaisesRegex(EvaluationValidationError, "adjudication"):
            validate_annotation_ledger(ledger, validate_dataset(dataset))

    def test_review_timestamps_and_adjudicator_independence_are_enforced(self):
        dataset, _, ledger = structurally_ready_inputs()
        record = ledger["records"][0]
        record["independent_verification"]["completed_at_utc"] = (
            "2026-08-12T23:59:00Z"
        )
        with self.assertRaisesRegex(EvaluationValidationError, "cannot finish before"):
            validate_annotation_ledger(ledger, validate_dataset(dataset))

        dataset, _, ledger = structurally_ready_inputs()
        record = ledger["records"][0]
        record["independent_verification"]["disposition"] = "changes_required"
        record["final_label_revision"] = 2
        record["adjudication"] = {
            "reviewer_id": record["initial_annotation"]["reviewer_id"],
            "completed_at_utc": "2026-08-13T00:02:00Z",
            "reason": "Test-only adjudication.",
        }
        with self.assertRaisesRegex(EvaluationValidationError, "independent"):
            validate_annotation_ledger(ledger, validate_dataset(dataset))

    def test_adjudicated_change_requires_revision_two_or_later(self):
        dataset, _, ledger = structurally_ready_inputs()
        record = ledger["records"][0]
        record["independent_verification"]["disposition"] = "changes_required"
        record["adjudication"] = {
            "reviewer_id": "fixture-adjudicator-c",
            "completed_at_utc": "2026-08-13T00:02:00Z",
            "reason": "Test-only adjudication.",
        }

        with self.assertRaisesRegex(EvaluationValidationError, "at least 2"):
            validate_annotation_ledger(ledger, validate_dataset(dataset))

    def test_register_and_ledger_require_exact_sample_closure(self):
        dataset, register, ledger = structurally_ready_inputs()
        validated_dataset = validate_dataset(dataset)
        register["sources"].pop()
        with self.assertRaisesRegex(EvaluationValidationError, "closure mismatch"):
            validate_source_register(register, validated_dataset)

        ledger["records"].pop()
        with self.assertRaisesRegex(EvaluationValidationError, "closure mismatch"):
            validate_annotation_ledger(ledger, validated_dataset)

    def test_final_field_hash_uses_canonical_json(self):
        dataset, _, ledger = load_smoke()
        expected = canonical_sha256(dataset["samples"][0]["ground_truth"]["fields"])

        self.assertEqual(
            ledger["records"][0]["final_fields_canonical_sha256"], expected
        )


if __name__ == "__main__":
    unittest.main()
