"""Frozen evidence-label scoring; no answer judging, provider calls or policy changes."""
import argparse
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
POLICIES = {"lexical": "lexical-or-english-v1", "vector": "pgvector-exact-cosine-v1",
            "hybrid": "pgvector-english-rrf60-c20-v1"}


def labels_for(question):
    return sorted({(e["source_id"], e["page_number"], e["quote"])
                   for c in question["expected"]["claims"] for e in c["evidence"]})


def covers(hit, label, source_id):
    return source_id == label[0] and any(s["pageNumber"] == label[1] and label[2] in s["quote"] for s in hit["spans"])


def evaluate(dataset, corpus, rows, modes=tuple(POLICIES), policy_versions=None):
    policies = POLICIES if policy_versions is None else policy_versions
    if not modes or len(set(modes)) != len(modes) or any(m not in policies for m in modes):
        raise ValueError("Invalid requested modes")
    sources = {s["sourceId"]: s for s in corpus}
    by_index = {s["indexId"]: s["sourceId"] for s in corpus}
    chunks = {c["id"]: (s["sourceId"], s["indexId"], c) for s in corpus for c in s["chunks"]}
    questions = {q["question_id"]: q for q in dataset["questions"]}
    assert len(questions) == len(dataset["questions"])
    # Establish label reachability independently of observed rankings.
    for q in questions.values():
        for label in labels_for(q):
            if not any(covers(c, label, label[0]) for c in sources[label[0]]["chunks"]):
                raise ValueError("A reference label is absent from the canonical corpus")
    results, seen, run_ids = [], set(), set()
    for row in rows:
        qid, mode, view = row["questionId"], row["mode"], row["result"]
        if row["outcome"] != "evaluation-retrieval-complete" or mode not in modes or qid not in questions:
            raise ValueError("Unexpected evaluation output")
        if (qid, mode) in seen or view["id"] in run_ids:
            raise ValueError("Duplicate question/mode or run")
        seen.add((qid, mode))
        run_ids.add(view["id"])
        if view["status"] != "Retrieved" or view["policyVersion"] != policies[mode] or view["failureCode"] is not None:
            raise ValueError("Incomplete retrieval or changed policy")
        if type(view["retrievalDurationMs"]) is not int or view["retrievalDurationMs"] < 0:
            raise ValueError("Missing retrieval duration")
        q = questions[qid]
        hits = view["hits"]
        if len(hits) > 5 or [h["rank"] for h in hits] != list(range(1, len(hits)+1)) or len({h["chunkId"] for h in hits}) != len(hits):
            raise ValueError("Invalid candidate bounds/order")
        for h in hits:
            if h["chunkId"] not in chunks:
                raise ValueError("Unrecognized chunk")
            source, index, canonical = chunks[h["chunkId"]]
            if source not in q["scope"]["allowed_source_ids"] or source in q["scope"]["forbidden_source_ids"] or index != h["indexId"]:
                raise ValueError("Out-of-scope evidence")
            if h["content"] != canonical["text"] or not math.isfinite(h["score"]):
                raise ValueError("Changed content or invalid score")
            spans = [(s["pageNumber"], s["byteStart"], s["byteEnd"], s["quote"]) for s in h["spans"]]
            expected = [(s["pageNumber"], s["byteStart"], s["byteEnd"], s["quote"]) for s in canonical["spans"]]
            if sorted(spans) != sorted(expected):
                raise ValueError("Changed canonical spans")
        labels = labels_for(q)
        ranks = [next((h["rank"] for h in hits if covers(h, label, by_index[h["indexId"]])), None) for label in labels]
        found = [rank for rank in ranks if rank is not None]
        results.append({"questionId": qid, "category": q["category"], "mode": mode, "runId": view["id"],
            "hitCount": len(hits), "labelCount": len(labels), "labelFirstRanks": ranks,
            "recallAt1": sum(r == 1 for r in ranks)/len(labels) if labels else None,
            "recallAt5": len(found)/len(labels) if labels else None,
            "fullCoverageAt5": len(found) == len(labels) if labels else None,
            "hitAt1": min(found) == 1 if found else (False if labels else None),
            "hitAt5": bool(found) if labels else None,
            "reciprocalRank": 1/min(found) if found else (0 if labels else None),
            "missingLabels": [dict(sourceId=l[0], pageNumber=l[1], quote=l[2]) for l,r in zip(labels,ranks) if r is None],
            "retrievalDurationMs": view["retrievalDurationMs"], "outOfScopeHits": 0})
    if seen != {(qid, mode) for qid in questions for mode in modes}:
        raise ValueError("Missing question/mode results")

    def summarize(group):
        scored = [x for x in group if x["labelCount"]]
        return {"questions": len(group), "answerableQuestions": len(scored),
            **{name: sum(x[name] for x in scored)/len(scored) if scored else None
               for name in ("recallAt1", "recallAt5", "fullCoverageAt5", "hitAt1", "hitAt5", "reciprocalRank")},
            "meanRetrievalMs": sum(x["retrievalDurationMs"] for x in group)/len(group),
            "missingOrPartialQuestions": [x["questionId"] for x in scored if not x["fullCoverageAt5"]],
            "unlabeledCasesWithHits": sum(x["hitCount"] > 0 for x in group if not x["labelCount"])}

    return {"version": "retrieval-evaluation-report-v0.1.0", "synthetic": True, "claimEligible": False,
        "comparisonComplete": set(modes) == set(policies), "pendingModes": [m for m in policies if m not in modes],
        "answerGenerationEvaluated": False, "foreignMirrorPresent": False,
        "modes": {m: summarize([x for x in results if x["mode"] == m]) for m in modes},
        "categories": {m: {c: summarize([x for x in results if x["mode"] == m and x["category"] == c])
            for c in sorted({q["category"] for q in questions.values()})} for m in modes},
        "results": sorted(results, key=lambda x: (x["questionId"], x["mode"]))}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--lexical-only", action="store_true", help="Explicit partial baseline; vector/hybrid remain pending")
    args = parser.parse_args()
    plan_dir = ROOT / "samples/phase-5/retrieval-evaluation-v0.1.0"
    directory = ROOT / "outputs/phase-5/retrieval-evaluation-20260910"
    plan = json.loads((plan_dir / "plan.json").read_text())
    dataset_bytes = (ROOT / "samples/phase-5/cited-questions-v0.1.0/question-set.json").read_bytes()
    corpus_bytes = (plan_dir / "corpus.json").read_bytes().rstrip(b"\r\n")
    if hashlib.sha256(dataset_bytes).hexdigest() != plan["questionSetSha256"] or hashlib.sha256(corpus_bytes).hexdigest() != plan["corpusSha256"]:
        raise ValueError("Frozen inputs changed")
    input_name = "lexical-results.jsonl" if args.lexical_only else "retrieval-results.jsonl"
    rows = [json.loads(line) for line in (directory / input_name).read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    report = evaluate(json.loads(dataset_bytes), json.loads(corpus_bytes), rows, ("lexical",) if args.lexical_only else tuple(POLICIES))
    report["questionSetSha256"] = plan["questionSetSha256"]
    report["corpusSha256"] = plan["corpusSha256"]
    with (directory / ("lexical-evaluation.json" if args.lexical_only else "evaluation.json")).open("x", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
        stream.write("\n")
    print(json.dumps(report["modes"], indent=2))


if __name__ == "__main__":
    main()
