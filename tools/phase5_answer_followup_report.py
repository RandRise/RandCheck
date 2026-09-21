"""Score frozen answers using explicit, digest-bound manual source assessments."""
import hashlib
import argparse
import json
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLAN_SHA = "f342359d97a8cb176013d4aed01a1833116b0a1fe510a294162586b494b8b752"
STATUSES = {0: "Answered", 1: "PartiallyAnswered", 2: "InsufficientEvidence"}


def assess(case, raw, operation, manual, retrieval, expected_effort=None, expected_prompt="cited-answer-prompt-v3"):
    generation = json.loads(raw)
    if hashlib.sha256(raw).hexdigest() != manual["generationSha256"]:
        raise ValueError("Assessment is not bound to this exact output")
    if (operation["retrieval_id"] != case["runId"] or operation["state"] != "Settled" or
            operation["prompt_version"] != expected_prompt):
        raise ValueError("Missing, unbound or unsettled generation")
    if expected_effort is not None and operation.get("reasoning_effort") != expected_effort:
        raise ValueError("Wrong reasoning profile")
    accepted = generation["outcome"] == "validated-unpublished"
    validation = generation.get("Validation") if accepted else None
    claims = validation["Claims"] if accepted else []
    if (len(manual["claimSupport"]) != len(claims) or
            any(type(v) is not bool for v in manual["claimSupport"]) or
            type(manual["complete"]) is not bool or not manual["notes"].strip()):
        raise ValueError("Incomplete manual source assessment")
    citations = 0
    if accepted:
        if expected_prompt == "cited-answer-prompt-v4" and (
                validation["ValidatorVersion"] != "citation-excerpt-binding-v2" or
                generation["ScopePolicyVersion"] != "cited-scope-restriction-v1"):
            raise ValueError("Wrong source-context validation policy")
        if generation["PublicationAllowed"] or validation["SemanticSupportEstablished"]:
            raise ValueError("Unpublished evaluation boundary changed")
        if generation["ReservationId"] != operation["reservation_id"]:
            raise ValueError("Different reservation")
        hits = {x["hitId"]: x for x in retrieval["hits"]}
        for claim in claims:
            for cite in claim["Citations"]:
                hit = hits[cite["HitId"]]
                if hit["indexId"] != cite["IndexId"] or hit["chunkId"] != cite["ChunkId"]:
                    raise ValueError("Citation outside frozen evidence")
                original = cite["OriginalPageEvidence"]
                span = next((x for x in hit["spans"] if x["id"] == cite["SpanId"]), None)
                if original is not None and (span is None or any(
                    original[k] != span[k[0].lower()+k[1:]] for k in ("PageNumber", "ByteStart", "ByteEnd", "Quote"))):
                    raise ValueError("Changed citation quote")
                if original is None and (cite["SpanId"] is not None or hit["kind"] != "ReviewedFact"):
                    raise ValueError("Missing page evidence")
                if expected_prompt == "cited-answer-prompt-v4":
                    support = cite.get("SupportText")
                    text = hit["content"] if hit["kind"] == "ReviewedFact" else span["quote"]
                    if (type(support) is not str or not support.strip() or len(support.encode("utf-16-le")) // 2 > 1000
                            or support != support.strip() or support not in text):
                        raise ValueError("Changed or missing citation support excerpt")
                citations += 1
    status = STATUSES[validation["Status"]] if accepted else None
    reason = validation["ReasonCode"] if accepted else None
    matched = status == case["expected"]["status"]
    reason_matched = reason == case["expected"]["reason_code"]
    citation_issues = manual.get("citationIssues", [])
    if type(citation_issues) is not list or any(type(x) is not str or not x.strip() for x in citation_issues):
        raise ValueError("Invalid citation assessment")
    return dict(caseId=case["caseId"], accepted=accepted, status=status, reason=reason,
        statusMatches=matched, reasonMatches=reason_matched, claimCount=len(claims), citationCount=citations,
        supportedClaimCount=sum(manual["claimSupport"]), complete=manual["complete"], notes=manual["notes"],
        citationIssues=citation_issues,
        endToEndPass=accepted and matched and reason_matched and manual["complete"] and all(manual["claimSupport"]) and not citation_issues,
        costUsd=str(operation["settled_usd"]), retrievalId=case["runId"])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--low", action="store_true")
    parser.add_argument("--low-fixed", action="store_true")
    parser.add_argument("--source", action="store_true")
    args = parser.parse_args()
    if args.source and (args.low or args.low_fixed):
        parser.error("--source selects its own pinned low-reasoning evaluation")
    args.low = args.low or args.low_fixed or args.source
    directory = ROOT / ("outputs/phase-5/answer-source-20260911" if args.source else "outputs/phase-5/answer-low-fixed-20260910" if args.low_fixed else "outputs/phase-5/answer-low-20260910" if args.low else "outputs/phase-5/answer-followup-20260910")
    plan_bytes = (ROOT / ("samples/phase-5/answer-source-v0.4.0/plan.json" if args.source else "samples/phase-5/answer-low-fixed-v0.3.1/plan.json" if args.low_fixed else "samples/phase-5/answer-low-v0.3.0/plan.json" if args.low else "samples/phase-5/answer-followup-v0.2.0/plan.json")).read_bytes()
    plan_sha = "56fda3a2d5317cea14700f00965b4a808dc855e9d1d6934d48b8190cce7c08be" if args.source else "9695a66bdc7fc3394525d10731537f8d33c326f9a1cc3b1b00d9479cf9a44632" if args.low_fixed else "ffca5a39729d0f5a57f72212afa54618824322e0e000bbed9183ff5d41ab7321" if args.low else PLAN_SHA
    if hashlib.sha256(plan_bytes).hexdigest() != plan_sha:
        raise ValueError("Changed frozen manifest")
    plan = json.loads(plan_bytes)
    ledger = json.loads((directory / "ledger.json").read_text(encoding="utf-8-sig"))
    if ledger["pending_count"] or ledger["budget"]["requires_reconciliation"]:
        raise ValueError("Unresolved active accounting")
    operations = {x["retrieval_id"]: x for x in ledger["operations"] if x["retrieval_id"]}
    retrieval_bytes = (directory / "retrievals.jsonl" if args.low else ROOT / "outputs/phase-5/nonredundant-retrieval-20260910/results.jsonl").read_bytes()
    if hashlib.sha256(retrieval_bytes).hexdigest() != plan["retrievalResultsSha256"]:
        raise ValueError("Changed frozen retrievals")
    retrievals = {x["questionId"]: x["result"] for x in map(json.loads, retrieval_bytes.decode("utf-8-sig").splitlines())}
    manual = json.loads((directory / "manual-assessment.json").read_text())
    if set(manual["cases"]) != {x["caseId"] for x in plan["cases"]}:
        raise ValueError("Missing or extra assessments")
    results = [assess(c, (directory / (c["caseId"]+"-generation.json")).read_bytes(),
               operations[c["runId"]], manual["cases"][c["caseId"]], retrievals[c["caseId"]], "low" if args.low else None,
               "cited-answer-prompt-v4" if args.source else "cited-answer-prompt-v3") for c in plan["cases"]]
    report = dict(version="answer-source-report-v4" if args.source else "answer-low-report-v3" if args.low else "answer-followup-report-v2", planSha256=plan_sha, synthetic=True, claimEligible=False,
        reasoningEffort="low" if args.low else "minimal",
        independentHoldout=False, automatedSemanticGate=False, foreignMirrorPresent=False,
        caseCount=len(results), accepted=sum(x["accepted"] for x in results),
        endToEndPasses=sum(x["endToEndPass"] for x in results),
        failedCases=[x["caseId"] for x in results if not x["endToEndPass"]],
        claimCount=sum(x["claimCount"] for x in results), supportedClaims=sum(x["supportedClaimCount"] for x in results),
        citationCount=sum(x["citationCount"] for x in results),
        generationCostUsd=str(sum((Decimal(x["costUsd"]) for x in results), Decimal(0))),
        sharedSettledUsd=str(ledger["budget"]["settled_usd"]),
        retainedUnknownUsd=str(sum((Decimal(str(x["reserved_usd"])) for x in ledger["operations"] if x["state"] == "HeldUnknown"),Decimal(0))),
        results=results)
    with (directory / "evaluation.json").open("x", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
        stream.write("\n")
    print(json.dumps({k:v for k,v in report.items() if k != "results"}, indent=2))


if __name__ == "__main__":
    main()
