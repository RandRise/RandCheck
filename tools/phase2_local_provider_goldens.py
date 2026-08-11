"""Generate or verify duration-neutral P2-T04 outputs in the pinned OCR container."""

from __future__ import annotations

import argparse
from dataclasses import asdict
import json
from pathlib import Path
import sys


REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "apps" / "api"))

from app.fixture_contract import validate_normalized_result  # noqa: E402
from app.local_extraction import LocalInvoiceExtractor  # noqa: E402


CASES = {
    "native-two-page-invoice": ("native-two-page-invoice.pdf", "application/pdf"),
    "scanned-two-page-invoice-300dpi": (
        "scanned-two-page-invoice-300dpi.pdf",
        "application/pdf",
    ),
    "invoice-png": ("invoice.png", "image/png"),
    "invoice-jpg": ("invoice.jpg", "image/jpeg"),
    "rotated-low-quality-invoice": (
        "rotated-low-quality-invoice.pdf",
        "application/pdf",
    ),
}


def payloads() -> dict[str, bytes]:
    extractor = LocalInvoiceExtractor()
    generated = REPO_ROOT / "samples" / "phase-2" / "generated"
    results: dict[str, bytes] = {}
    for name, (filename, content_type) in CASES.items():
        payload = asdict(extractor.extract(generated / filename, content_type))
        payload["metrics"]["provider_duration_ms"] = 0
        validate_normalized_result(payload)
        results[f"{name}.json"] = (
            json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
        ).encode("utf-8")
    return results


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=("generate", "verify"))
    parser.add_argument(
        "--root",
        type=Path,
        default=REPO_ROOT / "apps" / "api" / "tests" / "goldens" / "local-provider",
    )
    arguments = parser.parse_args()
    expected = payloads()
    if arguments.command == "generate":
        arguments.root.mkdir(parents=True, exist_ok=True)
        for filename, content in expected.items():
            (arguments.root / filename).write_bytes(content)
        print(f"Generated {len(expected)} local-provider goldens in {arguments.root}")
        return 0
    actual_files = {path.name for path in arguments.root.glob("*.json")}
    if actual_files != set(expected):
        raise RuntimeError("Local-provider golden file set does not match the approved cases")
    for filename, content in expected.items():
        if (arguments.root / filename).read_bytes() != content:
            raise RuntimeError(f"Local-provider output drifted: {filename}")
    print(f"Verified {len(expected)} duration-neutral local-provider goldens")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
