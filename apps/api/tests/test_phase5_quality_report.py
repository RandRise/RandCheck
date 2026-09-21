"""Regression checks for evaluation accounting, not a runtime semantic validator."""
from copy import deepcopy
import importlib.util
from pathlib import Path
import unittest

_spec = importlib.util.spec_from_file_location("phase5_quality_report", Path(__file__).resolve().parents[3] / "tools/phase5_quality_report.py")
_module = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_module)
assess_case = _module.assess_case


class QualityReportTests(unittest.TestCase):
    def setUp(self):
        self.case = {"case_id": "q", "expected": {"status": "Answered", "reason_code": None, "claims": []},
                     "result": {"retrieval": {"id": "run", "hits": [{}]}}}
        self.generation = {"outcome": "validated-unpublished", "PublicationAllowed": False,
                           "Validation": {"SemanticSupportEstablished": False, "Status": 0, "ReasonCode": None}}
        self.operation = {"retrieval_id": "run", "state": "Settled", "settled_usd": .000127,
                          "proposal": {"status": "Answered", "reasonCode": None}}
        self.manual = {"supported_and_complete": True}

    def score(self):
        return assess_case(self.case, self.generation, self.operation, self.manual, {})

    def test_structural_success_does_not_override_unsupported_payment_date(self):
        self.case["expected"]["status"] = "InsufficientEvidence"
        self.manual["supported_and_complete"] = False
        result = self.score()
        self.assertTrue(result["generation_validation_passed"])
        self.assertFalse(result["end_to_end_pass"])
        self.assertEqual("0.000127", result["generation_token_cost_estimate_usd"])

    def test_correct_text_rejected_for_duplicate_claims_does_not_pass(self):
        self.generation = {"outcome": "stopped", "code": "answer_proposal_not_accepted"}
        self.assertFalse(self.score()["end_to_end_pass"])

    def test_no_hit_answerable_case_is_not_a_correct_refusal(self):
        self.case["result"] = {"retrieval": {"id": "run", "hits": []},
                               "answer": {"status": "InsufficientEvidence", "reasonCode": "no_relevant_evidence"}}
        self.generation = self.operation = None
        self.assertFalse(self.score()["end_to_end_pass"])

    def test_unknown_cost_and_foreign_operation_cannot_be_assumed_settled(self):
        for operation in (None, dict(self.operation, state="Uncertain"), dict(self.operation, retrieval_id="foreign")):
            with self.subTest(operation=operation), self.assertRaises(ValueError):
                assess_case(self.case, self.generation, operation, self.manual, {})

    def test_publication_or_semantic_flag_cannot_be_claimed_by_report(self):
        for field in ("publication", "semantic"):
            result = deepcopy(self.generation)
            if field == "publication": result["PublicationAllowed"] = True
            else: result["Validation"]["SemanticSupportEstablished"] = True
            with self.subTest(field=field), self.assertRaises(ValueError):
                assess_case(self.case, result, self.operation, self.manual, {})

    def test_valid_supported_answer_can_pass(self):
        self.assertTrue(self.score()["end_to_end_pass"])
