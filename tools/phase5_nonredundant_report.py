"""Compare the new development policy with immutable three-policy results."""
import hashlib
import json
from phase5_retrieval_evaluation_report import ROOT, POLICIES, evaluate


def main():
    plan_dir = ROOT / "samples/phase-5/retrieval-evaluation-v0.1.0"
    output = ROOT / "outputs/phase-5/nonredundant-retrieval-20260910"
    plan = json.loads((plan_dir / "plan.json").read_text())
    dataset = (ROOT / "samples/phase-5/cited-questions-v0.1.0/question-set.json").read_bytes()
    corpus = (plan_dir / "corpus.json").read_bytes().rstrip(b"\r\n")
    assert hashlib.sha256(dataset).hexdigest() == plan["questionSetSha256"]
    assert hashlib.sha256(corpus).hexdigest() == plan["corpusSha256"]
    def rows(path):
        return [json.loads(x) for x in path.read_text(encoding="utf-8-sig").splitlines() if x.strip()]
    first, replay = rows(output / "results.jsonl"), rows(output / "replay.jsonl")
    assert len(first) == len(replay) == 30
    for a, b in zip(first, replay):
        assert b["result"]["replayed"] is True
        a["result"]["replayed"] = True
        assert a == b
    policies = dict(POLICIES, **{"hybrid-nonredundant": "pgvector-english-rrf60-c20-nonredundant-v1"})
    baseline = rows(ROOT / "outputs/phase-5/retrieval-evaluation-20260910/retrieval-results.jsonl")
    report = evaluate(json.loads(dataset), json.loads(corpus), baseline + first, tuple(policies), policies)
    report.update(questionSetSha256=plan["questionSetSha256"], corpusSha256=plan["corpusSha256"],
                  developmentSet=True, independentHoldout=False, replayVerified=True)
    with (output / "evaluation.json").open("x", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
        stream.write("\n")
    print(json.dumps(report["modes"], indent=2))


if __name__ == "__main__":
    main()
