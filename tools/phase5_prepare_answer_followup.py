"""Freeze the 30-case answer evaluation before any provider output is observed."""
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    base = ROOT / "samples/phase-5/retrieval-evaluation-v0.1.0"
    dataset_bytes = (ROOT / "samples/phase-5/cited-questions-v0.1.0/question-set.json").read_bytes()
    dataset = json.loads(dataset_bytes)
    plan = json.loads((base / "plan.json").read_bytes())
    assert hashlib.sha256(dataset_bytes).hexdigest() == plan["questionSetSha256"]
    retrieval_bytes = (ROOT / "outputs/phase-5/nonredundant-retrieval-20260910/results.jsonl").read_bytes()
    rows = [json.loads(x) for x in retrieval_bytes.decode("utf-8-sig").splitlines()]
    queries = {x["questionId"]: x for x in plan["cases"]}
    questions = {x["question_id"]: x for x in dataset["questions"]}
    cases = []
    for row in rows:
        qid, result = row["questionId"], row["result"]
        assert result["policyVersion"] == "pgvector-english-rrf60-c20-nonredundant-v1"
        assert result["hits"] and result["status"] == "Retrieved"
        expected = copy.deepcopy(questions[qid]["expected"])
        # Versioned, evidence-conditioned reason policy. Original labels stay intact.
        if expected["reason_code"] == "no_relevant_evidence":
            expected["reason_code"] = "unsupported_scope" if qid == "p5-q-030" else "incomplete_evidence"
        cases.append(dict(caseId=qid, runId=result["id"], question=queries[qid]["question"],
            requestKey=f"retrieval-evaluation-v0.1.0-{qid}-hybrid-nonredundant",
            indexIds=queries[qid]["indexIds"], policyVersion=result["policyVersion"], expected=expected))
    assert len(cases) == len({x["caseId"] for x in cases}) == 30
    manifest = dict(version="answer-followup-v0.2.0", synthetic=True, claimEligible=False,
        promptVersion="cited-answer-prompt-v3", model="gpt-5-nano-2025-08-07",
        questionSetSha256=plan["questionSetSha256"], corpusSha256=plan["corpusSha256"],
        retrievalResultsSha256=hashlib.sha256(retrieval_bytes).hexdigest(),
        referencePolicy="evidence-conditioned-refusals-v1", foreignMirrorPresent=False,
        maximumGenerationAttempts=30, maximumReservedUsd="0.060000", sharedBudgetUsd="8.00", cases=cases)
    output = ROOT / "samples/phase-5/answer-followup-v0.2.0"
    output.mkdir(exist_ok=True)
    raw = (json.dumps(manifest, indent=2) + "\n").encode()
    with (output / "plan.json").open("xb") as stream:
        stream.write(raw)
    print(hashlib.sha256(raw).hexdigest())


if __name__ == "__main__":
    main()
