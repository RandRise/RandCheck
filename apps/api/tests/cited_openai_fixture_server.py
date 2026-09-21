"""Test executable only: real private HTTP bridge, in-memory provider, no external IO."""
import json
import os
from unittest.mock import patch
from app.cited_openai_transport import OpenAiHttpResponse, OpenAiTransportError
from app.cited_synthetic_pilot import create_app


class FixtureTransport:
    calls = []

    async def send_counting(self, wire):
        self.calls.append("count")
        return OpenAiHttpResponse(b'{"object":"response.input_tokens","input_tokens":1000}', "req_count_fixture")

    async def send_generation(self, wire):
        self.calls.append("generate")
        mode = os.getenv("PILOT_FIXTURE_OUTCOME", "completed")
        if mode == "http_failure":
            raise OpenAiTransportError("openai_http_rejected", 429, "req_generation_fixture")
        evidence = json.loads(wire.source.evidence_json)["evidence"][0]
        source_aware = wire.source.prompt_version == "cited-answer-prompt-v4"
        proposal = {"schemaVersion": "1.1" if source_aware else "1.0", "status": "Answered", "reasonCode": None,
                    "claims": [{"text": "Synthetic fixture total is 100.00.", "citations": [{
                        "hitId": "ffffffff-ffff-ffff-ffff-ffffffffffff" if mode == "invalid_citation" else evidence["hitId"],
                        "spanId": evidence["spanIds"][0]}]}], "unansweredParts": []}
        if source_aware:
            proposal["claims"][0]["citations"][0]["supportText"] = (
                "fabricated excerpt 999999" if mode == "source_wrong_excerpt" else evidence["content"])
        if mode == "payment_claim":
            proposal["claims"][0]["text"] = "The invoice was paid on 2026-08-31."
        if mode == "insufficient":
            proposal.update(status="InsufficientEvidence", reasonCode="incomplete_evidence", claims=[])
        inputs = 1001 if mode == "overrun" else 1000
        value = {"id": "resp_generation_fixture", "object": "response", "model": wire.source.model.version,
                 "status": "incomplete" if mode == "incomplete" else "completed", "service_tier": "default",
                 "tools": [], "error": None, "incomplete_details": None,
                 "usage": {"input_tokens": inputs, "output_tokens": 50, "total_tokens": inputs + 50,
                           "input_tokens_details": {"cached_tokens": 400},
                           "output_tokens_details": {"reasoning_tokens": 20}},
                 "output": [{"type": "message", "role": "assistant", "status": "completed", "content": [
                     {"type": "refusal", "refusal": "No"} if mode == "refusal" else
                     {"type": "output_text", "text": json.dumps({"answer": proposal}), "annotations": []}]}]}
        if mode == "unknown_usage":
            value["usage"] = None
        if wire.reasoning_effort == "low":
            value["reasoning"] = {"effort": "low"}
        return OpenAiHttpResponse(json.dumps(value).encode(), "req_generation_fixture")


transport = FixtureTransport()
profile = "nano-low-v1" if os.getenv("PILOT_FIXTURE_OUTCOME", "").startswith("source_") else "nano-v1"
with patch.dict(os.environ, {"CITED_SYNTHETIC_PILOT": profile}), \
        patch("app.cited_synthetic_pilot.OpenAiResponsesTransport", return_value=transport):
    app = create_app()


@app.get("/health")
def health():
    return {"status": "test-only"}


@app.get("/probe")
def probe():
    return transport.calls
