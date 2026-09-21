"""Generate a deterministic cross-language fixture with local tokenization and fake vectors.

No HTTP transport, API key, database or provider is used. The artifact tests the
actual Python wire contract against the independent .NET storage verifier.
"""
import asyncio
import base64
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "apps/api"))
from app.cited_embedding_tokens import LocalEmbeddingTokenizer  # noqa: E402
from app.cited_embedding_transport import EmbeddingHttpResponse  # noqa: E402
from app.cited_embedding_workflow import prepare, propose, MODEL_WIRE, SCHEMA_VERSION  # noqa: E402


def encode(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


async def main():
    text = "Synthetic invoice: Total AED 100.00. شركة تجريبية"
    source = encode({"schemaVersion": SCHEMA_VERSION, "model": MODEL_WIRE,
                     "context": {"organizationId": "11111111-1111-1111-1111-111111111111",
                                 "actorId": "22222222-2222-2222-2222-222222222222",
                                 "operationId": "33333333-3333-3333-3333-333333333333", "purpose": "chunk",
                                 "indexIds": ["44444444-4444-4444-4444-444444444444"]},
                     "inputs": [{"inputId": "55555555-5555-5555-5555-555555555555", "text": text,
                                 "inputSha256": hashlib.sha256(text.encode()).hexdigest()}]})
    tokenizer = LocalEmbeddingTokenizer(ROOT / ".venv/share/tiktoken/cl100k_base.tiktoken")
    receipt, measured = prepare(source, tokenizer)
    dispatch = {"requestUtf8": base64.b64encode(source).decode(), "preparation": receipt}

    class FakeTransport:
        async def send(self, request):
            assert request == measured
            raw = encode({"object": "list", "model": MODEL_WIRE["model"],
                          "usage": {"prompt_tokens": measured.input_tokens, "total_tokens": measured.input_tokens},
                          "data": [{"object": "embedding", "index": 0, "embedding": [1.0] + [0.0] * 1535}]})
            return EmbeddingHttpResponse(raw, "req_python_storage_fixture")

    outcome = await propose(encode(dispatch), tokenizer, FakeTransport())
    result = {"provenance": "Python embedding-pilot-v1; real offline tokenizer; fake vectors; no provider calls",
              "dispatch": dispatch, "outcome": outcome}
    path = ROOT / "apps/backend/tests/Fixtures/embedding-python-v1.json"
    path.write_bytes(encode(result) + b"\n")
    print(f"Wrote offline fixture: {measured.input_tokens} tokens, no paid calls")


if __name__ == "__main__":
    asyncio.run(main())
