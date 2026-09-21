from dataclasses import replace
import math
import unittest

from app.cited_embedding_protocol import prepare_embedding_request, inspect_embedding_reply
from app.cited_embeddings import EmbeddingModel
from app.cited_vector_ranking import VectorCandidate, VectorRankingError, exact_cosine, reciprocal_rank_fusion
from test_cited_embedding_protocol import uid, SCOPE, INPUTS, MODEL as OPENAI_MODEL, response
import json

MODEL = EmbeddingModel("test-only", "fixture", "1", 3)
ORG, INDEX = uid(1), uid(4)


def candidate(i, values):
    return VectorCandidate(ORG, INDEX, uid(i), "a" * 64, MODEL, values)


class VectorRankingTests(unittest.TestCase):
    def test_exact_cosine_known_geometry_and_deterministic_ties(self):
        candidates = (candidate(8, (0, 1, 0)), candidate(6, (2, 0, 0)),
                      candidate(7, (-1, 0, 0)), candidate(5, (1, 0, 0)))
        ranked = exact_cosine(ORG, (INDEX,), MODEL, (1, 0, 0), candidates)
        self.assertEqual([uid(i) for i in (5, 6, 8, 7)], [x.chunk_id for x in ranked])
        self.assertEqual([1, 1, 0, -1], [x.score for x in ranked])
        self.assertEqual([1, 2, 3, 4], [x.vector_rank for x in ranked])
        self.assertEqual(ranked, exact_cosine(ORG, (INDEX,), MODEL, (1, 0, 0), tuple(reversed(candidates))))

    def test_large_and_small_finite_values_do_not_overflow_norm(self):
        for scale in (1e308, 1e-308):
            result = exact_cosine(ORG, (INDEX,), MODEL, (scale, scale, 0), (candidate(5, (scale, scale, 0)),))
            self.assertAlmostEqual(1, result[0].score)

    def test_foreign_source_space_or_invalid_vector_anywhere_rejects_whole_corpus(self):
        good = candidate(5, (1, 0, 0))
        bad = candidate(6, (0, 1, 0))
        variants = (replace(bad, organization_id=uid(99)), replace(bad, index_id=uid(99)),
                    replace(bad, model=replace(MODEL, version="2")), replace(bad, input_sha256="bad"),
                    replace(bad, chunk_id="bad"), replace(bad, values=(0, 0, 0)),
                    replace(bad, values=(1,)), replace(bad, values=(True, 0, 0)),
                    replace(bad, values=(float("inf"), 0, 0)), replace(bad, values=(float("nan"), 0, 0)), good)
        for invalid in variants:
            with self.subTest(invalid=invalid), self.assertRaises(VectorRankingError):
                exact_cosine(ORG, (INDEX,), MODEL, (1, 0, 0), (good, invalid), limit=1)

    def test_invalid_query_scope_and_limits_fail_closed(self):
        for query in ((0, 0, 0), (1,), (True, 0, 0), (math.nan, 0, 0)):
            with self.assertRaises(VectorRankingError):
                exact_cosine(ORG, (INDEX,), MODEL, query, ())
        for limit in (0, True, 2001):
            with self.assertRaises(VectorRankingError):
                exact_cosine(ORG, (INDEX,), MODEL, (1, 0, 0), (), limit)
        self.assertEqual((), exact_cosine(ORG, (INDEX,), MODEL, (1, 0, 0), ()))

    def test_total_component_bound_and_untrimmed_model_fail_before_vector_work(self):
        for model, candidates in ((replace(MODEL, dimension=65536), (candidate(5, (1, 0, 0)),) * 47),
                                  (replace(MODEL, version=" 1"), ())):
            with self.assertRaises(VectorRankingError):
                exact_cosine(ORG, (INDEX,), model, (), candidates)

    def test_rrf_combines_ranks_not_scores_and_preserves_both_positions(self):
        a, b, c = uid(5), uid(6), uid(7)
        ranked = reciprocal_rank_fusion(frozenset((a, b, c)), (a, b), (b, c))
        self.assertEqual([b, a, c], [x.chunk_id for x in ranked])
        self.assertAlmostEqual(1 / 62 + 1 / 61, ranked[0].score)
        self.assertEqual((2, 1), (ranked[0].lexical_rank, ranked[0].vector_rank))
        self.assertIsNone(ranked[2].lexical_rank)

    def test_rrf_ties_empty_lists_and_pure_modes_are_deterministic(self):
        a, b = uid(5), uid(6)
        scope = frozenset((a, b))
        self.assertEqual([a, b], [x.chunk_id for x in reciprocal_rank_fusion(scope, (a, b), (b, a))])
        self.assertEqual([b, a], [x.chunk_id for x in reciprocal_rank_fusion(scope, (), (b, a))])
        self.assertEqual((), reciprocal_rank_fusion(scope, (), ()))

    def test_rrf_foreign_duplicate_and_invalid_limits_are_rejected_before_top_k(self):
        a, b = uid(5), uid(6)
        for ranking in ((a, a), (a, b)):
            with self.assertRaises(VectorRankingError):
                reciprocal_rank_fusion(frozenset((a,)), ranking, (), top_k=1)
        for kwargs in ({"top_k": 0}, {"top_k": True}, {"rrf_k": 0}, {"rrf_k": 1001}):
            with self.assertRaises(VectorRankingError):
                reciprocal_rank_fusion(frozenset((a,)), (a,), (), **kwargs)

    def test_mock_provider_vectors_flow_into_scoped_ranking_without_content_rewrite(self):
        prepared = prepare_embedding_request(SCOPE, INPUTS)
        outcome = inspect_embedding_reply(prepared, json.dumps(response()).encode(), "req_fixture")
        candidates = tuple(VectorCandidate(ORG, INDEX, vector.chunk_id, vector.input_sha256,
                                           OPENAI_MODEL, vector.values) for vector in outcome.vectors)
        ranked = exact_cosine(ORG, (INDEX,), OPENAI_MODEL, outcome.vectors[0].values, candidates)
        self.assertEqual([item.chunk_id for item in INPUTS], [rank.chunk_id for rank in ranked])
        self.assertFalse(outcome.publication_allowed)
