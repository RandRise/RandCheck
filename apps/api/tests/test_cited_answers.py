import asyncio
from dataclasses import replace
import hashlib
import json
import traceback
import unittest
from unittest.mock import patch

from app.cited_answers import (AnswerBoundaryError, AnswerContext, AnswerEvidence,
                               AnswerModel, AnswerProposalEnvelope, DisabledAnswerAdapter,
                               prepare_answer_request, propose_answer_checked)


def uid(number):
    return f"00000000-0000-0000-0000-{number:012d}"


CONTEXT = AnswerContext(uid(1), uid(2), uid(3))
MODEL = AnswerModel("test-only", "fake-model", "1")
CONTENT = "é Invoice total: AED 100.00"
EVIDENCE = (AnswerEvidence(uid(1), uid(3), uid(4), (uid(5),), CONTENT,
                           hashlib.sha256(CONTENT.encode()).hexdigest()),)


class FakeAdapter:
    def __init__(self, change=lambda result: result, error=None):
        self.calls = 0
        self.change = change
        self.error = error

    async def propose(self, request):
        self.calls += 1
        if self.error is not None:
            raise self.error
        proposal = json.dumps({"schemaVersion": "1.0", "status": "Answered", "reasonCode": None,
                               "claims": [{"text": "Invoice total: AED 100.00",
                                           "citations": [{"hitId": uid(4), "spanId": uid(5)}]}],
                               "unansweredParts": []}).encode()
        return self.change(AnswerProposalEnvelope(request.model, request.request_sha256, proposal, 100, 50))


class CitedAnswerBoundaryTests(unittest.IsolatedAsyncioTestCase):
    async def test_default_and_explicit_disabled_adapter_never_generate(self):
        for adapter in (None, DisabledAnswerAdapter()):
            with self.subTest(adapter=adapter), self.assertRaises(AnswerBoundaryError) as failure:
                await self.propose(adapter)
            self.assertEqual("generation_disabled", failure.exception.code)

    async def test_fake_proposal_preserves_contract_but_does_not_establish_support(self):
        adapter = FakeAdapter()
        result = await self.propose(adapter)
        self.assertEqual(1, adapter.calls)
        self.assertEqual("Answered", json.loads(result.proposal_utf8)["status"])
        self.assertFalse(result.semantic_support_established)
        self.assertNotIn("Invoice total", repr(result))

    def test_preparation_is_deterministic_and_binds_scope_model_and_limits(self):
        prepared = prepare_answer_request(CONTEXT, MODEL, "total?", EVIDENCE)
        self.assertEqual(prepared, prepare_answer_request(CONTEXT, MODEL, "total?", EVIDENCE))
        variations = [prepare_answer_request(replace(CONTEXT, actor_id=uid(6)), MODEL, "total?", EVIDENCE),
                      prepare_answer_request(CONTEXT, replace(MODEL, version="2"), "total?", EVIDENCE),
                      prepare_answer_request(CONTEXT, MODEL, "total?", EVIDENCE, 100)]
        for value in variations:
            self.assertNotEqual(prepared.request_sha256, value.request_sha256)
        self.assertNotIn("Invoice total", repr(prepared))
        payload = json.loads(prepared.evidence_json)
        self.assertEqual(CONTENT, payload["evidence"][0]["content"])
        self.assertNotIn("organizationId", payload)

    async def test_foreign_scope_duplicate_hits_and_bad_evidence_fail_before_adapter(self):
        changes = [(), EVIDENCE * 2, (replace(EVIDENCE[0], organization_id=uid(9)),),
                   (replace(EVIDENCE[0], retrieval_run_id=uid(9)),),
                   (replace(EVIDENCE[0], content_sha256="0" * 64),),
                   (replace(EVIDENCE[0], hit_id=uid(0)),),
                   (replace(EVIDENCE[0], span_ids=(uid(5), uid(5))),),
                   (replace(EVIDENCE[0], span_ids=("not-an-id",)),),
                   (replace(EVIDENCE[0], content="private\ud800"),)]
        for evidence in changes:
            adapter = FakeAdapter()
            with self.subTest(evidence=evidence), self.assertRaises(AnswerBoundaryError):
                await propose_answer_checked(CONTEXT, MODEL, "total?", evidence, adapter)
            self.assertEqual(0, adapter.calls)

    async def test_question_utf16_output_limit_and_aggregate_utf8_bounds(self):
        for question in ("", " ", "x" * 4001, "😀" * 2001, "private\ud800"):
            adapter = FakeAdapter()
            with self.subTest(length=len(question)), self.assertRaises(AnswerBoundaryError):
                await propose_answer_checked(CONTEXT, MODEL, question, EVIDENCE, adapter)
            self.assertEqual(0, adapter.calls)
        for limit in (0, -1, 4001, True):
            with self.subTest(limit=limit), self.assertRaises(AnswerBoundaryError):
                prepare_answer_request(CONTEXT, MODEL, "total?", EVIDENCE, limit)
        # Each entry fits, but their serialized aggregate exceeds 64 KiB.
        content = "é" * 20000
        item = replace(EVIDENCE[0], content=content, content_sha256=hashlib.sha256(content.encode()).hexdigest())
        with self.assertRaises(AnswerBoundaryError):
            prepare_answer_request(CONTEXT, MODEL, "total?", (item, replace(item, hit_id=uid(6))))

    async def test_invalid_context_or_model_has_no_side_effects(self):
        for context, model in [(replace(CONTEXT, organization_id=uid(0)), MODEL),
                               (CONTEXT, replace(MODEL, version="")),
                               (CONTEXT, replace(MODEL, model="private\nmodel"))]:
            adapter = FakeAdapter()
            with self.assertRaises(AnswerBoundaryError):
                await propose_answer_checked(context, model, "total?", EVIDENCE, adapter)
            self.assertEqual(0, adapter.calls)

    async def test_wrong_binding_model_usage_encoding_and_size_reject_whole_response(self):
        changes = [lambda result: None, lambda r: replace(r, request_sha256="0" * 64),
                   lambda r: replace(r, model=replace(MODEL, model="premium")),
                   lambda r: replace(r, input_tokens=8001), lambda r: replace(r, input_tokens=True),
                   lambda r: replace(r, output_tokens=-1), lambda r: replace(r, output_tokens=4001),
                   lambda r: replace(r, proposal_utf8=b""), lambda r: replace(r, proposal_utf8=b"\xff"),
                   lambda r: replace(r, proposal_utf8=b"x" * 65537)]
        for change in changes:
            adapter = FakeAdapter(change)
            with self.subTest(change=change), self.assertRaises(AnswerBoundaryError) as failure:
                await self.propose(adapter)
            self.assertEqual("invalid_answer_envelope", failure.exception.code)
            self.assertEqual(1, adapter.calls)

    async def test_provider_refusal_or_truncation_is_not_an_insufficient_evidence_answer(self):
        for outcome in ("refused", "incomplete", "unknown"):
            with self.subTest(outcome=outcome), self.assertRaises(AnswerBoundaryError) as failure:
                await self.propose(FakeAdapter(lambda r: replace(r, outcome=outcome)))
            self.assertEqual("answer_provider_incomplete", failure.exception.code)

    async def test_provider_failure_is_safe_and_not_retried(self):
        for error, code in [(RuntimeError("private-invoice-value"), "answer_provider_failed"),
                            (TimeoutError("private-invoice-value"), "answer_provider_timeout"),
                            (AnswerBoundaryError("private-invoice-value"), "answer_provider_failed")]:
            adapter = FakeAdapter(error=error)
            with self.assertRaises(AnswerBoundaryError) as failure:
                await self.propose(adapter)
            self.assertEqual(code, failure.exception.code)
            self.assertNotIn("private-invoice-value", "".join(traceback.format_exception(failure.exception)))
            self.assertEqual(1, adapter.calls)

    async def test_cancellation_propagates_without_retry(self):
        adapter = FakeAdapter(error=asyncio.CancelledError())
        with self.assertRaises(asyncio.CancelledError):
            await self.propose(adapter)
        self.assertEqual(1, adapter.calls)

    async def test_deadline_cancels_a_waiting_adapter_without_retry(self):
        class WaitingAdapter:
            calls = 0
            cancelled = False

            async def propose(self, request):
                self.calls += 1
                try:
                    await asyncio.Event().wait()
                finally:
                    self.cancelled = True

        adapter = WaitingAdapter()
        short_deadline = asyncio.timeout(0.01)
        with patch("app.cited_answers.asyncio.timeout", return_value=short_deadline), self.assertRaises(AnswerBoundaryError) as failure:
            await self.propose(adapter)
        self.assertEqual("answer_provider_timeout", failure.exception.code)
        self.assertEqual(1, adapter.calls)
        self.assertTrue(adapter.cancelled)

    async def test_boundary_does_not_pretend_to_validate_json_citations_or_truth(self):
        result = await self.propose(FakeAdapter(lambda r: replace(r, proposal_utf8=b"not even JSON")))
        self.assertFalse(result.semantic_support_established)
        # This is an untrusted envelope. The existing ASP.NET validator rejects it.

    async def propose(self, adapter):
        return await propose_answer_checked(CONTEXT, MODEL, "total?", EVIDENCE, adapter)
