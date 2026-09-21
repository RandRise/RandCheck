"""Freeze the existing 30 questions/canonical corpus before any new paid dispatch."""
import hashlib
import json
from pathlib import Path
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "apps/api"))
from app.cited_question_evaluation import audit_question_set
from app.cited_retrieval import lexical_plan
from app.cited_embedding_tokens import LocalEmbeddingTokenizer
from app.cited_embedding_workflow import prepare, MODEL_WIRE


def main():
    target = ROOT / "samples/phase-5/retrieval-evaluation-v0.1.0"
    if target.exists():
        raise ValueError("Frozen evaluation already exists; do not rewrite it.")
    dataset_bytes = (ROOT / "samples/phase-5/cited-questions-v0.1.0/question-set.json").read_bytes()
    dataset = json.loads(dataset_bytes)
    audit = audit_question_set(dataset, ROOT)
    corpus_bytes = (ROOT / "tmp/phase5-evaluation-corpus.json").read_bytes().rstrip(b"\r\n")
    corpus = json.loads(corpus_bytes)
    assert len(corpus) == 3 and sum(len(s["chunks"]) for s in corpus) == 60
    assert len(dataset["questions"]) == 30 and all(q["scope"]["organization_fixture"] == "org-primary" for q in dataset["questions"])
    source_map = {s["sourceId"]: s for s in corpus}
    for source in corpus:
        reference = next(s for s in dataset["sources"] if s["source_id"] == source["sourceId"])
        assert reference["sha256"] == source["documentSha256"]
        for chunk in source["chunks"]:
            assert hashlib.sha256(chunk["text"].encode()).hexdigest() == chunk["sha256"]
    operations, cases, measurements = [], [], []
    tokenizer = LocalEmbeddingTokenizer(ROOT / ".venv/share/tiktoken/cl100k_base.tiktoken")

    def add(purpose, indexes, inputs, name, question=None):
        operation = str(uuid.uuid5(uuid.NAMESPACE_URL, "retrieval-evaluation-v0.1.0/" + name))
        indexes = sorted(indexes)
        source = {"schemaVersion": "embedding-pilot-v1", "model": MODEL_WIRE,
                  "context": {"organizationId": "11111111-1111-1111-1111-111111111111",
                              "actorId": "22222222-2222-2222-2222-222222222222",
                              "operationId": operation, "purpose": purpose, "indexIds": indexes},
                  "inputs": [{"inputId": c["id"], "text": c["text"], "inputSha256": c["sha256"]} for c in inputs]}
        receipt, _ = prepare(json.dumps(source, separators=(",", ":")).encode(), tokenizer)
        measurements.append({"operationId": operation, "purpose": purpose, "inputTokens": receipt["inputTokens"]})
        operations.append({"operationId": operation, "purpose": purpose, "indexIds": indexes,
                           "chunkIds": [c["id"] for c in inputs] if purpose == "chunk" else [],
                           "inputHashes": {c["id"]: c["sha256"] for c in inputs} if purpose == "chunk" else {},
                           "question": question})
        return operation

    for source in corpus:
        chunks = sorted(source["chunks"], key=lambda c: c["id"])
        for i in range(0, len(chunks), 4):
            add("chunk", [source["indexId"]], chunks[i:i+4], f"{source['sourceId']}/batch-{i//4+1}")
    for q in dataset["questions"]:
        question, qid = q["question"], q["question_id"]
        indexes = [source_map[s]["indexId"] for s in q["scope"]["allowed_source_ids"]]
        opid = str(uuid.uuid5(uuid.NAMESPACE_URL, "retrieval-evaluation-v0.1.0/query/" + qid))
        operation = add("query", indexes, [{"id": opid, "text": question,
                        "sha256": hashlib.sha256(question.encode()).hexdigest()}], "query/" + qid, question)
        cases.append({"questionId": qid, "operationId": operation, "question": question,
                      "indexIds": sorted(indexes), "lexicalPlan": lexical_plan(question, 20)})
    assert len(operations) == 46
    plan = {"version": "retrieval-evaluation-v0.1.0", "questionSetSha256": hashlib.sha256(dataset_bytes).hexdigest(),
            "corpusSha256": hashlib.sha256(corpus_bytes).hexdigest(), "operations": operations, "cases": cases}
    target.mkdir(parents=True)
    (target / "corpus.json").write_bytes(corpus_bytes + b"\n")
    (target / "plan.json").write_text(json.dumps(plan, indent=2) + "\n", encoding="utf-8", newline="\n")
    (target / "preflight.json").write_text(json.dumps({"questionAudit": audit, "measurements": measurements,
        "totalInputTokens": sum(x["inputTokens"] for x in measurements), "plannedProviderCalls": 46,
        "maximumReservationUsd": "0.007360", "synthetic": True, "claimEligible": False,
        "foreignMirrorInRetainedDatabase": False}, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"planSha256": hashlib.sha256((target / "plan.json").read_bytes()).hexdigest(),
                      "tokens": sum(x["inputTokens"] for x in measurements), "operations": len(operations)}))


if __name__ == "__main__":
    main()
