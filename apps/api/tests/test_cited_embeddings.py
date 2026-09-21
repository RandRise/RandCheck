from dataclasses import replace
import unittest

from app.cited_chunking import content_hash
from app.cited_embeddings import (DisabledEmbeddingAdapter, EmbeddedVector, EmbeddingBatch,
                                  EmbeddingError, EmbeddingInput, EmbeddingModel, embed_checked)


MODEL = EmbeddingModel("test-only", "test-model", "1", 3)
INPUTS = tuple(EmbeddingInput(f"00000000-0000-0000-0000-00000000000{i}", text, content_hash(text))
               for i, text in enumerate(["total[0]: 100.00", "TRN 100123456700003"], 1))


class TestOnlyAdapter:
    def __init__(self, change=lambda x: x):
        self.change = change
        self.calls = 0

    async def embed(self, inputs):
        self.calls += 1
        return self.change(EmbeddingBatch(MODEL, tuple(
            EmbeddedVector(x.chunk_id, x.input_sha256, (1.0, 0.0, 0.5)) for x in reversed(inputs)), 1))


class CitedEmbeddingTests(unittest.IsolatedAsyncioTestCase):
    async def test_provider_output_is_validated_and_reordered_by_identity(self):
        adapter = TestOnlyAdapter()
        result = await embed_checked(adapter, MODEL, INPUTS)
        self.assertEqual([x.chunk_id for x in INPUTS], [x.chunk_id for x in result.vectors])
        self.assertIsNone(result.estimated_cost)
        self.assertEqual(1, adapter.calls)

    async def test_default_disabled_adapter_never_generates_fake_vectors(self):
        with self.assertRaises(EmbeddingError):
            await embed_checked(DisabledEmbeddingAdapter(), MODEL, INPUTS)

    async def test_bad_inputs_are_rejected_before_provider_is_called(self):
        for inputs in ((), (INPUTS[0], INPUTS[0]), (replace(INPUTS[0], input_sha256="0" * 64),)):
            adapter = TestOnlyAdapter()
            with self.subTest(inputs=inputs), self.assertRaises(EmbeddingError):
                await embed_checked(adapter, MODEL, inputs)
            self.assertEqual(0, adapter.calls)

    async def test_invalid_model_is_rejected_before_provider_is_called(self):
        adapter = TestOnlyAdapter()
        with self.assertRaises(EmbeddingError):
            await embed_checked(adapter, replace(MODEL, dimension=0), INPUTS)
        self.assertEqual(0, adapter.calls)

    async def test_malformed_provider_batches_fail_as_a_whole(self):
        def vector_change(**kwargs):
            return lambda b: replace(b, vectors=(replace(b.vectors[0], **kwargs), b.vectors[1]))
        changes = [lambda b: replace(b, model=replace(MODEL, version="2")),
                   lambda b: replace(b, vectors=b.vectors[:1]),
                   lambda b: replace(b, vectors=(b.vectors[0], b.vectors[0])),
                   lambda b: replace(b, duration_ms=-1),
                   lambda b: replace(b, estimated_cost=float("nan"), cost_currency="AED"),
                   lambda b: replace(b, estimated_cost=1, cost_currency=None),
                   vector_change(chunk_id="00000000-0000-0000-0000-000000000099"),
                   vector_change(input_sha256="0" * 64), vector_change(values=(1.0,)),
                   vector_change(values=(0.0, 0.0, 0.0)), vector_change(values=(1.0, float("inf"), 0.0)),
                   vector_change(values=(True, 1.0, 1.0))]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(EmbeddingError):
                await embed_checked(TestOnlyAdapter(change), MODEL, INPUTS)

    async def test_cancelled_provider_propagates_without_partial_results(self):
        import asyncio

        class CancelledAdapter:
            async def embed(self, inputs):
                raise asyncio.CancelledError()

        with self.assertRaises(asyncio.CancelledError):
            await embed_checked(CancelledAdapter(), MODEL, INPUTS)
