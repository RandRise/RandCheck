"""Read-only local incident export. This is triage, never billing proof or recovery authority."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
from uuid import UUID

ROOT = Path(__file__).resolve().parents[1]
PLAN_SHA = "d4176993fac3bc00d0bdbaf8f1ee9e8bb4f7cda6c7b7ff7d50c3366e723e8406"
ORG = "11111111-1111-1111-1111-111111111111"
TARGET = "ccaecf4e-d611-5042-aa64-84d054d42346"


def query(plan_bytes):
    if hashlib.sha256(plan_bytes).hexdigest() != PLAN_SHA:
        raise ValueError("Frozen plan changed")
    plan = json.loads(plan_bytes)
    ids = ",".join("'" + str(UUID(op["operationId"])) + "'" for op in plan["operations"])
    # No .env, ambient key, provider request, public route, or write transaction.
    # Return a strict metadata projection; raw request/response bytes stay in PG.
    return f"""
BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '10s';
SET LOCAL TIME ZONE 'UTC';
SELECT jsonb_build_object('capturedAtUtc',transaction_timestamp(),
 'budget',(SELECT jsonb_build_object('ceilingUsd',ceiling_usd::text,
   'settledUsd',settled_usd::text,'requiresReconciliation',requires_reconciliation)
   FROM cited_pilot_budgets WHERE id=1),
 'operations',(SELECT jsonb_agg(jsonb_build_object(
   'operationId',r.operation_id,'reservationId',r.id,'state',r.state,
   'createdAtUtc',r.created_at_utc,'reservedUsd',r.reserved_usd::text,'actualUsd',r.actual_usd::text,
   'requestSha256',r.request_sha256,'provider',r.provider,'model',r.model,
   'events',(SELECT jsonb_agg(jsonb_build_object('sequence',e.sequence,'state',e.state,
     'createdAtUtc',e.created_at_utc) ORDER BY e.sequence) FROM cited_spend_events e WHERE e.reservation_id=r.id),
   'artifacts',(SELECT jsonb_agg(jsonb_build_object('kind',a.kind,'sha256',a.payload_sha256,
     'createdAtUtc',a.created_at_utc) ORDER BY a.kind) FROM cited_generation_artifacts a WHERE a.reservation_id=r.id),
   'outcome',o.p->'outcome','providerRequestId',o.p->'providerRequestId',
   'httpStatus',o.p->'httpStatus','inputTokens',o.p->'inputTokens',
   'measuredInputTokens',o.p->'measuredInputTokens','accountingKnown',o.p->'accountingKnown',
   'bodySha256',o.p->'bodySha256','bindingSha256',o.p->'bindingSha256',
   'responseSha256',o.p->'responseSha256') ORDER BY r.created_at_utc)
 FROM cited_spend_reservations r
 LEFT JOIN (SELECT reservation_id,convert_from(payload_utf8,'UTF8')::jsonb p
   FROM cited_generation_artifacts WHERE kind='Outcome') o ON o.reservation_id=r.id
 WHERE r.organization_id='{ORG}' AND r.workload='Embedding' AND r.operation_id IN ({ids})));
ROLLBACK;
"""


def build_packet(snapshot):
    operations = snapshot["operations"]
    if not operations or len({o["operationId"] for o in operations}) != len(operations):
        raise ValueError("Missing or duplicate operations")
    target = next(o for o in operations if o["operationId"] == TARGET)
    if target["state"] != "Uncertain" or target["accountingKnown"] is not False or target["actualUsd"] is not None:
        raise ValueError("Incident state changed; reassess before proceeding")
    if snapshot["budget"]["requiresReconciliation"] is not True:
        raise ValueError("Incident budget state changed")
    allowed = "operationId reservationId state createdAtUtc reservedUsd actualUsd requestSha256 provider model events artifacts outcome providerRequestId httpStatus inputTokens measuredInputTokens accountingKnown bodySha256 bindingSha256 responseSha256".split()
    for op in operations:
        if set(op) != set(allowed):
            raise ValueError("Unexpected metadata; do not export raw content")
        for event in op["events"]:
            if set(event) != {"sequence", "state", "createdAtUtc"}:
                raise ValueError("Unexpected event metadata")
        for artifact in op["artifacts"]:
            if set(artifact) != {"kind", "sha256", "createdAtUtc"}:
                raise ValueError("Unexpected artifact metadata")
        request_id = op["providerRequestId"]
        if request_id is not None and (type(request_id) is not str or not re.fullmatch(r"req_[A-Za-z0-9_-]{1,196}", request_id)):
            raise ValueError("Unrecognized provider request ID")
    known = [o for o in operations if o["state"] == "Settled" and o["accountingKnown"] is True]
    if any(type(o["inputTokens"]) is not int or o["inputTokens"] < 0 for o in known):
        raise ValueError("Missing confirmed token count")
    times = [datetime.fromisoformat(e["createdAtUtc"]) for o in operations for e in o["events"]]
    if not times or any(t.tzinfo is None for t in times):
        raise ValueError("Missing timezone in event timestamps")
    start = int(min(times).timestamp()) // 60 * 60
    end = (int(max(times).timestamp()) // 60 + 1) * 60
    return {
        "version": "embedding-recovery-triage-v1", "capturedAtUtc": snapshot["capturedAtUtc"],
        "source": "local-canonical-ledger", "billingVerified": False,
        "executable": False, "mayRedispatch": False, "mayClearSharedFreeze": False,
        "targetOperationId": TARGET, "budget": snapshot["budget"],
        "localKnownRequests": len(known), "localKnownInputTokens": sum(o["inputTokens"] for o in known),
        "uncertainMeasuredTokens": target["measuredInputTokens"],
        "providerOrganizationId": None, "providerProjectId": None, "providerApiKeyId": None,
        "clientRequestIdSent": False,
        "usageWindow": {"startTime": start, "endTime": end,
            "startUtc": datetime.fromtimestamp(start, timezone.utc).isoformat(),
            "endUtcExclusive": datetime.fromtimestamp(end, timezone.utc).isoformat(), "bucketWidth": "1m"},
        "limitations": ["The local organization UUID is not an OpenAI organization ID.",
            "Project and API-key IDs must be established from the provider account, not guessed.",
            "Counts include only this frozen local evaluation; other account traffic may exist.",
            "Aggregate usage is diagnostic and cannot establish request-level billing finality.",
            "No response or client request ID was retained for the uncertain historical call."],
        "operations": operations,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("An existing incident packet will not be overwritten")
    try:
        sql = query((ROOT / "samples/phase-5/retrieval-evaluation-v0.1.0/plan.json").read_bytes())
        result = subprocess.run(["psql", "-X", "-qAt", "-w", "-h", "127.0.0.1", "-p", "25434",
            "-U", "document_intelligence", "-d", "phase5_e2e_20260908", "-v", "ON_ERROR_STOP=1"],
            input=sql, capture_output=True, text=True, timeout=15, check=True)
        packet = build_packet(json.loads(result.stdout))
        with args.output.open("x", encoding="utf-8") as stream:
            json.dump(packet, stream, indent=2, allow_nan=False)
            stream.write("\n")
        print(json.dumps({"outcome": "read-only-incident-packet-saved", "billingVerified": False,
            "knownRequests": packet["localKnownRequests"], "knownInputTokens": packet["localKnownInputTokens"],
            "usageWindow": packet["usageWindow"]}))
    except Exception:
        parser.exit(1, "Incident export failed; no recovery or provider call was performed.\n")


if __name__ == "__main__":
    main()
