import copy
import json
from pathlib import Path
import unittest

from app.cited_question_evaluation import (
    AUDIT_SCHEMA_VERSION,
    QUESTION_SET_SCHEMA_VERSION,
    CitedQuestionValidationError,
    audit_question_set,
    validate_question_set,
)


REPO_ROOT = Path(__file__).resolve().parents[3]
PHASE5_ROOT = REPO_ROOT / "samples" / "phase-5"
DATASET_PATH = PHASE5_ROOT / "cited-questions-v0.1.0" / "question-set.json"
SCHEMA_PATH = PHASE5_ROOT / "schemas" / "question-set-1.0.schema.json"


def load_dataset() -> dict:
    return json.loads(DATASET_PATH.read_text(encoding="utf-8"))


class CitedQuestionEvaluationTests(unittest.TestCase):
    def test_committed_set_passes_source_page_and_evidence_audit(self):
        audit = audit_question_set(load_dataset(), REPO_ROOT)

        self.assertEqual(AUDIT_SCHEMA_VERSION, audit["audit_schema_version"])
        self.assertEqual(4, audit["source_count"])
        self.assertEqual(3, audit["unique_source_file_count"])
        self.assertEqual(30, audit["question_count"])
        self.assertEqual(46, audit["evidence_label_count"])
        self.assertEqual(
            {
                "adversarial_isolation": 3,
                "cross_document": 6,
                "paraphrase": 6,
                "single_document": 12,
                "unanswerable": 3,
            },
            audit["category_counts"],
        )
        self.assertEqual(
            {"Answered": 24, "InsufficientEvidence": 6},
            audit["status_counts"],
        )
        self.assertTrue(audit["source_hashes_verified"])
        self.assertTrue(audit["canonical_pages_verified"])
        self.assertTrue(audit["evidence_quotes_verified"])
        self.assertTrue(audit["passed"])

    def test_schema_uses_draft_2020_12_and_resolvable_local_refs(self):
        schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))

        self.assertEqual("https://json-schema.org/draft/2020-12/schema", schema["$schema"])
        self.assertEqual(
            "urn:uae-document-intelligence:phase5:question-set:1.0", schema["$id"]
        )
        self.assertEqual(
            QUESTION_SET_SCHEMA_VERSION,
            schema["properties"]["question_set_schema_version"]["const"],
        )
        self._assert_local_refs_resolve(schema, schema)

    def test_question_ids_and_coverage_requirements_are_closed(self):
        payload = load_dataset()
        payload["questions"][1]["question_id"] = payload["questions"][0]["question_id"]
        with self.assertRaisesRegex(CitedQuestionValidationError, "duplicate question_id"):
            validate_question_set(payload)

        payload = load_dataset()
        payload["coverage_requirements"]["single_document"] += 1
        with self.assertRaisesRegex(CitedQuestionValidationError, "category counts"):
            validate_question_set(payload)

    def test_answered_claims_require_allowed_page_evidence(self):
        payload = load_dataset()
        claim = payload["questions"][0]["expected"]["claims"][0]
        claim["evidence"] = []
        with self.assertRaisesRegex(CitedQuestionValidationError, "must not be empty"):
            validate_question_set(payload)

        payload = load_dataset()
        evidence = payload["questions"][0]["expected"]["claims"][0]["evidence"][0]
        evidence["source_id"] = "purchase-order-primary"
        with self.assertRaisesRegex(CitedQuestionValidationError, "outside the allowed corpus"):
            validate_question_set(payload)

        payload = load_dataset()
        evidence = payload["questions"][0]["expected"]["claims"][0]["evidence"][0]
        evidence["page_number"] = 99
        with self.assertRaisesRegex(CitedQuestionValidationError, "not present"):
            validate_question_set(payload)

    def test_insufficient_evidence_cannot_smuggle_a_claim(self):
        payload = load_dataset()
        refusal = payload["questions"][24]
        refusal["expected"]["claims"] = copy.deepcopy(
            payload["questions"][0]["expected"]["claims"]
        )
        with self.assertRaisesRegex(CitedQuestionValidationError, "cannot contain claims"):
            validate_question_set(payload)

    def test_cross_organization_source_cannot_enter_allowed_corpus(self):
        payload = load_dataset()
        isolation = payload["questions"][29]
        isolation["scope"]["allowed_source_ids"] = ["invoice-foreign"]
        isolation["scope"]["forbidden_source_ids"] = []
        with self.assertRaisesRegex(CitedQuestionValidationError, "another organization"):
            validate_question_set(payload)

    def test_source_and_page_hash_tampering_fails_audit(self):
        payload = load_dataset()
        payload["sources"][0]["sha256"] = "0" * 64
        with self.assertRaisesRegex(CitedQuestionValidationError, "SHA-256 does not match"):
            audit_question_set(payload, REPO_ROOT)

        payload = load_dataset()
        payload["sources"][0]["pages"][0]["canonical_text_sha256"] = "0" * 64
        with self.assertRaisesRegex(CitedQuestionValidationError, "canonical hash"):
            audit_question_set(payload, REPO_ROOT)

    def test_nonexistent_or_ambiguous_quote_fails_audit(self):
        payload = load_dataset()
        evidence = payload["questions"][0]["expected"]["claims"][0]["evidence"][0]
        evidence["quote"] = "not present in the canonical page"
        with self.assertRaisesRegex(CitedQuestionValidationError, "exactly once"):
            audit_question_set(payload, REPO_ROOT)

        payload = load_dataset()
        evidence = payload["questions"][0]["expected"]["claims"][0]["evidence"][0]
        evidence["quote"] = "0"
        with self.assertRaisesRegex(CitedQuestionValidationError, "exactly once"):
            audit_question_set(payload, REPO_ROOT)

    def _assert_local_refs_resolve(self, value: object, root: dict) -> None:
        if isinstance(value, dict):
            reference = value.get("$ref")
            if reference is not None:
                self.assertIsInstance(reference, str)
                self.assertTrue(reference.startswith("#/"), reference)
                target: object = root
                for encoded_component in reference[2:].split("/"):
                    component = encoded_component.replace("~1", "/").replace("~0", "~")
                    self.assertIsInstance(target, dict)
                    self.assertIn(component, target)
                    target = target[component]
            for child in value.values():
                self._assert_local_refs_resolve(child, root)
        elif isinstance(value, list):
            for child in value:
                self._assert_local_refs_resolve(child, root)


if __name__ == "__main__":
    unittest.main()

