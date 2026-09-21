"""Report the frozen source-v4 batch, keeping unknown charges and failed attempts visible."""
import hashlib
import json
from decimal import Decimal
from pathlib import Path
from phase5_answer_followup_report import assess

ROOT = Path(__file__).resolve().parents[1]
DIRECTORY = ROOT / "outputs/phase-5/answer-source-20260911"
PLAN_SHA = "56fda3a2d5317cea14700f00965b4a808dc855e9d1d6934d48b8190cce7c08be"


def main():
    plan_bytes = (ROOT / "samples/phase-5/answer-source-v0.4.0/plan.json").read_bytes()
    if hashlib.sha256(plan_bytes).hexdigest() != PLAN_SHA:
        raise ValueError("Changed evaluation plan")
    plan = json.loads(plan_bytes)
    ledger_bytes = (DIRECTORY / "ledger-after-recovery.json").read_bytes()
    ledger = json.loads(ledger_bytes)
    operations = {x["retrieval_id"]: x for x in ledger["operations"] if x["retrieval_id"]}
    retrieval_bytes = (DIRECTORY / "retrievals.jsonl").read_bytes()
    if hashlib.sha256(retrieval_bytes).hexdigest() != plan["retrievalResultsSha256"]:
        raise ValueError("Changed evidence")
    retrievals = {x["questionId"]: x["result"] for x in map(json.loads, retrieval_bytes.decode("utf-8-sig").splitlines())}
    manual = json.loads((DIRECTORY / "manual-assessment-recovered.json").read_bytes())
    if set(manual["cases"]) != {c["caseId"] for c in plan["cases"]}:
        raise ValueError("Missing source review")
    results = []
    known = Decimal(0)
    held = Decimal(0)
    for case in plan["cases"]:
        raw = (DIRECTORY / (case["caseId"] + "-generation.json")).read_bytes()
        review = manual["cases"][case["caseId"]]
        operation = operations[case["runId"]]
        if operation["prompt_version"] != "cited-answer-prompt-v4" or operation["reasoning_effort"] != "low":
            raise ValueError("Different prompt or reasoning profile")
        if operation["state"] == "Settled":
            result = assess(case, raw, operation, review, retrievals[case["caseId"]], "low", "cited-answer-prompt-v4")
            known += Decimal(str(operation["settled_usd"]))
        else:
            if (operation["state"] not in ("HeldUnknown", "Uncertain") or operation["settled_usd"] is not None or
                    Decimal(str(operation["reserved_usd"])) != Decimal(".002") or
                    hashlib.sha256(raw).hexdigest() != review["generationSha256"] or
                    json.loads(raw) != {"outcome": "stopped", "code": "generation_outcome_uncertain"} or
                    review["claimSupport"] != [] or review["complete"] is not False or not review["notes"].strip()):
                raise ValueError("Invalid unresolved attempt")
            held += Decimal(str(operation["reserved_usd"]))
            result = dict(caseId=case["caseId"], accepted=False, endToEndPass=False, claimCount=0,
                citationCount=0, supportedClaimCount=0, complete=False, notes=review["notes"],
                costUsd=None, retainedUnknownUsd=str(operation["reserved_usd"]), accountingState=operation["state"])
        result["generationSha256"] = hashlib.sha256(raw).hexdigest()
        results.append(result)
    if known + held > Decimal(".06"):
        raise ValueError("Authorized batch budget exceeded")
    report = dict(version="source-recovered-report-v1", synthetic=True, claimEligible=False,
        independentHoldout=False, automatedSemanticGate=False, publicationAllowed=False,
        planSha256=PLAN_SHA, ledgerSha256=hashlib.sha256(ledger_bytes).hexdigest(),
        caseCount=len(results), accepted=sum(x["accepted"] for x in results),
        endToEndPasses=sum(x["endToEndPass"] for x in results),
        failedCases=[x["caseId"] for x in results if not x["endToEndPass"]],
        claimCount=sum(x["claimCount"] for x in results),
        supportedClaims=sum(x["supportedClaimCount"] for x in results),
        citationCount=sum(x["citationCount"] for x in results),
        generationCostUsd=None if held else str(known), knownGenerationCostUsd=str(known),
        runRetainedUnknownUsd=str(held), accountedUpperBoundUsd=str(known + held),
        pendingCount=ledger["pending_count"], reconciliationRequired=ledger["budget"]["requires_reconciliation"],
        results=results)
    with (DIRECTORY / "evaluation-recovered.json").open("x", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
    print(json.dumps({k: v for k, v in report.items() if k != "results"}, indent=2))


if __name__ == "__main__":
    main()
