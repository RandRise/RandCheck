"""Offline compatibility audit and source-aware packet snapshots; never dispatch.

Reads an explicitly exported synthetic database snapshot. Historical run IDs are
used only to compare packets, and must not be reused for a new generation attempt.
"""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "apps/api"))
from app.cited_answer_routes import _source
from app.cited_answers import prepare_answer_request
from app.cited_openai_protocol import prepare_openai_request


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    with path.open("x", encoding="utf-8") as output:
        json.dump(value, output, ensure_ascii=False, indent=2)
        output.write("\n")


def audit(export_path, plan_path, output):
    data = json.loads(export_path.read_text(encoding="utf-8-sig"))
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    assert data["database"] == "phase5_e2e_20260908" and plan["synthetic"] is True
    assert plan["version"] == "answer-low-fixed-v0.3.1" and len(plan["cases"]) == 30
    output.mkdir(exist_ok=False)
    prior = {}
    for saved in data["preparations"]:
        dispatch = saved["payload"]
        raw = base64.b64decode(dispatch["requestUtf8"], validate=True)
        receipt = dispatch["preparation"]
        assert digest(raw) == receipt["requestSha256"]
        args, maximum, _ = _source(raw)
        prepared = prepare_answer_request(*args, maximum)
        assert prepared.prompt_sha256 == receipt["promptSha256"]
        assert prepared.prompt_version == receipt["promptVersion"] == "cited-answer-prompt-v3"
        effort = receipt["openAi"].get("reasoningEffort", "minimal")
        wire = prepare_openai_request(prepared, effort)
        assert wire.generation_sha256 == receipt["openAi"]["generationSha256"]
        assert wire.counting_sha256 == receipt["openAi"]["countingSha256"]
        assert saved["runId"] not in prior
        prior[saved["runId"]] = json.loads(raw)
    cases = []
    for case in plan["cases"]:
        raw_source = prior[case["runId"]]
        assert raw_source["question"] == case["question"]
        assert raw_source["context"]["retrievalRunId"] == case["runId"]
        raw_source["schemaVersion"] = "1.1"
        for item in raw_source["evidence"]:
            authoritative = data["hits"][item["hitId"]]
            assert authoritative["organizationId"] == item["organizationId"]
            assert authoritative["retrievalRunId"] == item["retrievalRunId"]
            assert authoritative["contentSha256"] == item["contentSha256"]
            assert authoritative["source"]["indexId"] in case["indexIds"]
            item["source"] = authoritative["source"]
        args, maximum, _ = _source(json.dumps(raw_source, ensure_ascii=False).encode())
        prepared = prepare_answer_request(*args, maximum)
        wire = prepare_openai_request(prepared, "low")
        packet = json.loads(prepared.prompt_json)
        write_json(output / (case["caseId"] + "-packet.json"), packet)
        write_json(output / (case["caseId"] + "-request.json"), json.loads(wire.generation_utf8))
        cases.append(dict(caseId=case["caseId"], historicalRunId=case["runId"], promptSha256=prepared.prompt_sha256,
                          generationSha256=wire.generation_sha256, countingSha256=wire.counting_sha256,
                          packetBytes=len(prepared.prompt_json), generationBytes=len(wire.generation_utf8),
                          evidenceHits=len(raw_source["evidence"]),
                          distinctDocuments=len({item["source"]["documentId"] for item in raw_source["evidence"]})))
    report = dict(version="source-context-offline-audit-v1", paidCalls=0, publicationAllowed=False,
                  historicalV3PacketsExactlyRebuilt=len(prior), candidatePacketsBuilt=len(cases),
                  originalPlanSha256=digest(plan_path.read_bytes()), exportSha256=digest(export_path.read_bytes()),
                  model="gpt-5-nano-2025-08-07", reasoningEffort="low", promptVersion="cited-answer-prompt-v4",
                  sourceSchemaVersion="1.1", maximumRequestBytes=65536,
                  maximumCandidateGenerationBytes=max(case["generationBytes"] for case in cases), cases=cases,
                  limitations=["No model answers generated or scored; prior quality scores remain unchanged.",
                               "Local byte checks are not provider input-token measurements.",
                               "Historical run IDs in these snapshots are not dispatchable new operations.",
                               "Excerpt membership does not establish entailment or comparison completeness."])
    write_json(output / "audit.json", report)
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--export", type=Path, required=True)
    parser.add_argument("--plan", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = audit(args.export, args.plan, args.output)
    print(json.dumps({key: report[key] for key in ("historicalV3PacketsExactlyRebuilt", "candidatePacketsBuilt",
                                                  "maximumCandidateGenerationBytes", "paidCalls")}))
