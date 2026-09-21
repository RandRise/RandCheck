import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location("retrieval_report", ROOT / "tools/phase5_retrieval_evaluation_report.py")
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)


def fixture():
    span = {"pageNumber": 1, "byteStart": 0, "byteEnd": 9, "quote": "Total 100"}
    corpus = [{"sourceId": "s", "indexId": "index", "chunks": [
        {"id": "one", "text": "Total 100", "spans": [span]},
        {"id": "two", "text": "Tax 200", "spans": [{"pageNumber": 1, "byteStart": 10, "byteEnd": 17, "quote": "Tax 200"}]}]}]
    questions = [{"question_id": "q", "category": "cross_document", "scope": {"allowed_source_ids": ["s"], "forbidden_source_ids": []},
                  "expected": {"claims": [{"evidence": [{"source_id": "s", "page_number": 1, "quote": "Total 100"},
                                                        {"source_id": "s", "page_number": 1, "quote": "Tax 200"}]}]}},
                 {"question_id": "unknown", "category": "unanswerable", "scope": {"allowed_source_ids": ["s"], "forbidden_source_ids": []},
                  "expected": {"claims": []}}]
    rows = [{"questionId": q["question_id"], "mode": m, "outcome": "evaluation-retrieval-complete",
             "result": {"id": q["question_id"]+m, "status": "Retrieved", "policyVersion": policy, "failureCode": None,
                        "retrievalDurationMs": 3, "hits": [{"chunkId": "one", "indexId": "index", "content": "Total 100",
                                                            "score": .4, "rank": 1, "spans": [copy.deepcopy(span)]}]}}
            for q in questions for m,policy in report.POLICIES.items()]
    return {"questions": questions}, corpus, rows


class RetrievalReportTests(unittest.TestCase):
    def test_separate_policy_requires_its_exact_version_and_complete_set(self):
        d,c,r = fixture()
        policies = dict(report.POLICIES, revised="revised-v1")
        for row in list(r):
            if row["mode"] == "hybrid":
                new = copy.deepcopy(row)
                new["mode"] = "revised"
                new["result"].update(id=row["questionId"]+"revised", policyVersion="revised-v1")
                r.append(new)
        self.assertTrue(report.evaluate(d,c,r,tuple(policies),policies)["comparisonComplete"])
        with self.assertRaises(ValueError): report.evaluate(d,c,r[:-1],tuple(policies),policies)
        r[-1]["result"]["policyVersion"] = "wrong"
        with self.assertRaises(ValueError): report.evaluate(d,c,r,tuple(policies),policies)

    def test_partial_baseline_is_explicit_and_still_requires_all_questions(self):
        d,c,r = fixture()
        lexical = [row for row in r if row["mode"] == "lexical"]
        result = report.evaluate(d,c,lexical,("lexical",))
        self.assertFalse(result["comparisonComplete"])
        self.assertEqual(["vector", "hybrid"], result["pendingModes"])
        with self.assertRaises(ValueError): report.evaluate(d,c,lexical)
        with self.assertRaises(ValueError): report.evaluate(d,c,lexical[:-1],("lexical",))
        with self.assertRaises(ValueError): report.evaluate(d,c,r,("lexical",))

    def test_partial_labels_are_not_full_recall_and_unanswerable_cases_are_excluded(self):
        result = report.evaluate(*fixture())
        for mode in result["modes"].values():
            self.assertEqual(2, mode["questions"])
            self.assertEqual(1, mode["answerableQuestions"])
            self.assertEqual(.5, mode["recallAt5"])
            self.assertEqual(0, mode["fullCoverageAt5"])
            self.assertEqual(1, mode["reciprocalRank"])
            self.assertEqual(1, mode["unlabeledCasesWithHits"])

    def test_duplicate_labels_are_counted_once(self):
        dataset, corpus, rows = fixture()
        evidence = dataset["questions"][0]["expected"]["claims"][0]["evidence"]
        evidence.append(copy.deepcopy(evidence[0]))
        self.assertEqual(.5, report.evaluate(dataset, corpus, rows)["modes"]["vector"]["recallAt5"])

    def test_missing_duplicate_or_incomplete_results_fail(self):
        for variant in ("missing", "duplicate", "failed"):
            d,c,r = fixture()
            if variant == "missing": r.pop()
            elif variant == "duplicate": r.append(copy.deepcopy(r[0]))
            else: r[0]["result"]["status"] = "Failed"
            with self.subTest(variant=variant), self.assertRaises(ValueError): report.evaluate(d,c,r)

    def test_foreign_scope_and_tampered_source_or_spans_fail(self):
        for variant in ("scope", "index", "content", "spans", "score"):
            d,c,r = fixture()
            if variant == "scope": d["questions"][0]["scope"]["forbidden_source_ids"] = ["s"]
            elif variant == "index": r[0]["result"]["hits"][0]["indexId"] = "foreign"
            elif variant == "content": r[0]["result"]["hits"][0]["content"] = "Forged"
            elif variant == "spans": r[0]["result"]["hits"][0]["spans"][0]["quote"] = "Tax 200"
            else: r[0]["result"]["hits"][0]["score"] = float("nan")
            with self.subTest(variant=variant), self.assertRaises(ValueError): report.evaluate(d,c,r)

    def test_absent_reference_label_fails_before_scoring(self):
        d,c,r = fixture()
        d["questions"][0]["expected"]["claims"][0]["evidence"][0]["quote"] = "Missing"
        with self.assertRaises(ValueError): report.evaluate(d,c,r)

    def test_no_hits_is_zero_recall_for_answerable_only(self):
        d,c,r = fixture()
        for row in r: row["result"]["hits"] = []
        result = report.evaluate(d,c,r)
        self.assertEqual(0, result["modes"]["vector"]["recallAt5"])
        self.assertEqual(0, result["modes"]["vector"]["reciprocalRank"])
        self.assertIsNone(next(x for x in result["results"] if x["questionId"] == "unknown")["recallAt5"])
