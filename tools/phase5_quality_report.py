"""Offline evaluation report only. No provider calls, publication or semantic gate.

Manual evidence assessments are explicit inputs, not model-judged ground truth.
Structural acceptance must never be presented as semantic correctness.
"""
import json
from decimal import Decimal
from pathlib import Path

STATUSES = {0: "Answered", 1: "PartiallyAnswered", 2: "InsufficientEvidence"}


def assess_case(case, generation, operation, manual, source_ids):
    evidence = case["result"]["retrieval"]
    no_hits = len(evidence["hits"]) == 0
    accepted = generation is not None and generation.get("outcome") == "validated-unpublished"
    if accepted and (generation.get("PublicationAllowed") is not False or
                     generation["Validation"].get("SemanticSupportEstablished") is not False):
        raise ValueError("Pilot publication/semantic boundary changed")
    if no_hits and (generation is not None or operation is not None):
        raise ValueError("No-hit case must not generate")
    if operation is not None and (operation["retrieval_id"] != evidence["id"] or operation["state"] != "Settled"):
        raise ValueError("Unbound or unsettled operation")
    if not no_hits and operation is None:
        raise ValueError("Missing accounting; never assume zero cost")
    raw = operation.get("proposal") if operation else None
    actual_status = (case["result"]["answer"]["status"] if no_hits else
                     STATUSES[generation["Validation"]["Status"]] if accepted else None)
    reason = (case["result"]["answer"]["reasonCode"] if no_hits else
              generation["Validation"]["ReasonCode"] if accepted else raw.get("reasonCode") if raw else None)
    labels = [label for claim in case["expected"]["claims"] for label in claim["evidence"]]
    coverage = []
    for label in labels:
        found = any(source_ids.get(hit["indexId"]) == label["source_id"] and
                    any(span["pageNumber"] == label["page_number"] and label["quote"] in span["quote"]
                        for span in hit["spans"]) for hit in evidence["hits"])
        coverage.append(found)
    status_match = actual_status == case["expected"]["status"]
    return {"case_id": case["case_id"], "hit_count": len(evidence["hits"]),
            "expected_status": case["expected"]["status"], "accepted_status": actual_status,
            "raw_model_status": raw.get("status") if raw else None,
            "generation_validation_passed": accepted if not no_hits else None,
            "status_matches_reference": status_match,
            "reason_matches_reference": reason == case["expected"]["reason_code"],
            "expected_reason": case["expected"]["reason_code"], "returned_reason": reason,
            "labeled_quotes_found": sum(coverage), "labeled_quotes_required": len(labels),
            "full_labeled_quote_coverage": all(coverage) if coverage else None,
            "manual_assessment": manual, "end_to_end_pass": bool(
                (accepted or no_hits) and status_match and manual["supported_and_complete"]),
            "generation_token_cost_estimate_usd": str(Decimal(str(operation["settled_usd"]))) if operation else "0",
            "retrieval_id": evidence["id"]}


def main():
    root = Path(__file__).resolve().parents[1]
    directory = root / "outputs/phase-5/live-quality-20260909"
    def load(name):
        return json.loads((directory / name).read_text(encoding="utf-8-sig"))
    manifest, ledger, manual = load("prepared-cases.json"), load("ledger-after-quality.json"), load("manual-assessment.json")
    if ledger["pending_count"] or ledger["budget"]["requires_reconciliation"]:
        raise ValueError("Pilot accounting requires reconciliation")
    operations = {x["retrieval_id"]: x for x in ledger["operations"]}
    source_ids = {x["index_id"]: x["source_id"] for x in manifest["sources"]}
    results = []
    for case in manifest["cases"]:
        file = directory / (case["case_id"] + "-generation.json")
        generation = load(file.name) if file.exists() else None
        results.append(assess_case(case, generation, operations.get(case["result"]["retrieval"]["id"]),
                                   manual["cases"][case["case_id"]], source_ids))
    report = {"version": "live-quality-report-v1", "plan_sha256": manifest["plan_sha256"],
              "question_set_sha256": manifest["question_set_sha256"],
              "synthetic": True, "claim_eligible": False, "automated_semantic_gate": False,
              "case_count": len(results), "end_to_end_passes": sum(x["end_to_end_pass"] for x in results),
              "generation_validation_passes": sum(x["generation_validation_passed"] is True for x in results),
              "provider_generation_cases": sum(x["hit_count"] > 0 for x in results),
              "answerable_cases": sum(x["expected_status"] == "Answered" for x in results),
              "full_labeled_quote_coverage_cases": sum(x["full_labeled_quote_coverage"] is True for x in results),
              "new_generation_token_cost_estimate_usd": str(sum((Decimal(x["generation_token_cost_estimate_usd"]) for x in results), Decimal(0))),
              "shared_ledger_total_usd": str(Decimal(str(ledger["budget"]["settled_usd"]))),
              "results": results}
    # Exclusive creation preserves prior assessments; no silent benchmark rewrites.
    with (directory / "evaluation.json").open("x", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(json.dumps({k: v for k, v in report.items() if k != "results"}, indent=2))


if __name__ == "__main__":
    main()
