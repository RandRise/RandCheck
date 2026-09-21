"""Explicit provisioning of the public, hash-pinned tokenizer vocabulary. No API key."""
import argparse
import hashlib
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "apps/api"))
from app.cited_embedding_tokens import VOCABULARY_SHA256, MAX_VOCABULARY_BYTES, LocalEmbeddingTokenizer

DESTINATION = ROOT / ".venv/share/tiktoken/cl100k_base.tiktoken"
URL = "https://openaipublic.blob.core.windows.net/encodings/cl100k_base.tiktoken"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--download", action="store_true", help="Explicitly download if the local vocabulary is absent")
    args = parser.parse_args()
    try:
        if not DESTINATION.exists():
            if not args.download:
                raise ValueError()
            import httpx
            with httpx.Client(trust_env=False, follow_redirects=False, timeout=20) as client:
                with client.stream("GET", URL, headers={"Accept-Encoding": "identity"}) as response:
                    if response.status_code != 200 or response.headers.get("content-encoding", "identity") != "identity":
                        raise ValueError()
                    raw = bytearray()
                    for chunk in response.iter_raw():
                        if len(raw) + len(chunk) > MAX_VOCABULARY_BYTES:
                            raise ValueError()
                        raw.extend(chunk)
            if hashlib.sha256(raw).hexdigest() != VOCABULARY_SHA256:
                raise ValueError()
            DESTINATION.parent.mkdir(parents=True, exist_ok=True)
            with DESTINATION.open("xb") as output:
                output.write(raw)  # Provisioned public asset, never application/user data.
        LocalEmbeddingTokenizer(DESTINATION)  # Missing/corrupt/version mismatch fails closed.
        print("tokenizer-ready: tiktoken 0.12.0 / cl100k_base / SHA256 " + VOCABULARY_SHA256)
        return 0
    except Exception:
        print("tokenizer-unavailable: no overwrite or automatic retry", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
