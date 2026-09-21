import copy
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location("recovery_packet", ROOT / "tools/phase5_embedding_recovery_packet.py")
packet = importlib.util.module_from_spec(spec)
spec.loader.exec_module(packet)


def fixture():
    timestamp = "2026-09-10T13:43:05+00:00"
    op = {"operationId": packet.TARGET, "reservationId": "local-reservation", "state": "Uncertain",
        "createdAtUtc": timestamp, "reservedUsd": "0.000001", "actualUsd": None,
        "requestSha256": "a"*64, "provider": "openai", "model": "text-embedding-3-small",
        "events": [{"sequence": 3, "state": "Uncertain", "createdAtUtc": timestamp}],
        "artifacts": [{"kind": "Outcome", "sha256": "b"*64, "createdAtUtc": timestamp}],
        "outcome": "transport_uncertain", "providerRequestId": None, "httpStatus": None,
        "inputTokens": None, "measuredInputTokens": 16, "accountingKnown": False,
        "bodySha256": "c"*64, "bindingSha256": "d"*64, "responseSha256": None}
    known = copy.deepcopy(op)
    known.update(operationId="known-operation", state="Settled", actualUsd="0.000003",
        inputTokens=100, accountingKnown=True, providerRequestId="req_known")
    return {"capturedAtUtc": timestamp, "budget": {"ceilingUsd": "8.000000", "settledUsd": "0.002001",
        "requiresReconciliation": True}, "operations": [known, op]}


class RecoveryPacketTests(unittest.TestCase):
    def test_local_estimate_never_becomes_billing_proof(self):
        result = packet.build_packet(fixture())
        self.assertEqual(100, result["localKnownInputTokens"])
        self.assertEqual(16, result["uncertainMeasuredTokens"])
        self.assertFalse(result["billingVerified"])
        self.assertFalse(result["executable"])
        self.assertFalse(result["mayClearSharedFreeze"])
        self.assertFalse(result["mayRedispatch"])
        self.assertIsNone(result["providerProjectId"])

    def test_raw_fields_and_untrusted_request_ids_cannot_be_exported(self):
        for location in ("operation", "event", "artifact", "requestId"):
            data = fixture()
            op = data["operations"][1]
            if location == "operation": op["responseUtf8"] = "private"
            elif location == "event": op["events"][0]["body"] = "private"
            elif location == "artifact": op["artifacts"][0]["payload"] = "private"
            else: op["providerRequestId"] = "private\nheader"
            with self.subTest(location=location), self.assertRaises(ValueError): packet.build_packet(data)

    def test_changed_incident_or_duplicate_rows_requires_reassessment(self):
        for change in ("settled", "unfrozen", "duplicate", "usage"):
            data = fixture()
            if change == "settled": data["operations"][1]["state"] = "Settled"
            elif change == "unfrozen": data["budget"]["requiresReconciliation"] = False
            elif change == "duplicate": data["operations"].append(copy.deepcopy(data["operations"][0]))
            else: data["operations"][0]["inputTokens"] = None
            with self.subTest(change=change), self.assertRaises(ValueError): packet.build_packet(data)

    def test_window_covers_events_in_utc_and_rejects_ambiguous_timezone(self):
        data = fixture()
        data["operations"][0]["events"][0]["createdAtUtc"] = "2026-09-10T17:42:59+04:00"
        result = packet.build_packet(data)
        self.assertEqual("2026-09-10T13:42:00+00:00", result["usageWindow"]["startUtc"])
        self.assertEqual("2026-09-10T13:44:00+00:00", result["usageWindow"]["endUtcExclusive"])
        data["operations"][0]["events"][0]["createdAtUtc"] = "2026-09-10T17:42:59"
        with self.assertRaises(ValueError): packet.build_packet(data)

    def test_changed_plan_cannot_select_another_ledger_scope(self):
        with self.assertRaises(ValueError): packet.query(b'{"operations": []}')
        sql = packet.query((ROOT / "samples/phase-5/retrieval-evaluation-v0.1.0/plan.json").read_bytes())
        self.assertIn("REPEATABLE READ READ ONLY", sql)
        self.assertIn("ROLLBACK", sql)

