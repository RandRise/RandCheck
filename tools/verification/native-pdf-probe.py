"""Probe the real private Python service with known fictional native PDFs.

This does not exercise uploads, the .NET queue, malware screening or human review.
Input copies stay below the validation output root; no retained data is replaced.
"""
import hashlib
import json
import shutil
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "outputs/scale-integration-20261023"
SHARED = OUTPUT / "native-inputs"
ORGANIZATION = "11111111111111111111111111111111"


def probe(source: Path) -> dict:
    document = uuid4()
    key = f"organizations/{ORGANIZATION}/documents/{document.hex}/source.pdf"
    target = SHARED / key
    target.parent.mkdir(parents=True, exist_ok=False)
    shutil.copyfile(source, target)
    payload = {
        "schema_version": "2.0", "job_id": str(uuid4()),
        "document_id": str(document), "extraction_run_id": str(uuid4()),
        "storage_key": key, "sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
        "bytes": target.stat().st_size, "declared_content_type": "application/pdf",
        "provider_alias": "local", "attempt": 1,
        "deadline_utc": (datetime.now(timezone.utc) + timedelta(seconds=60)).isoformat(),
        "correlation_id": "synthetic-native-probe",
    }
    request = urllib.request.Request(
        "http://127.0.0.1:8010/internal/extractions", json.dumps(payload).encode(),
        {"Content-Type": "application/json", "X-Worker-Token": "synthetic_validation_worker_only"},
    )
    try:
        with urllib.request.urlopen(request, timeout=65) as response:
            return {"status": response.status, "response": json.load(response)}
    except urllib.error.HTTPError as error:
        return {"status": error.code, "response": json.load(error)}


def main() -> None:
    receipts = []
    for name, role in [("english-invoice", "invoice"), ("english-po", "purchase_order")]:
        result = probe(ROOT / f"samples/supplier-invoice/v0.1.0/{name}.pdf")
        body = result["response"]
        assert result["status"] == 200, result
        assert body["document_type"] == role
        assert body["provider"]["adapter_version"] == "local-1.6.0"
        assert body["outcome"] == "Partial"
        assert any(w["code"] == "line_coverage_incomplete" for w in body["warnings"])
        # Frozen fixture truth distinguishes printed text from the normalized number.
        assert any(f["key"] == "total" and f["value"] == "1,260.00"
                   and f["normalized_value"] == "1260.00" for f in body["fields"])
        assert all(f["evidence"] for f in body["fields"])
        receipts.append({"fixture": name, **result})
    (OUTPUT / "native-pdf-http.json").write_text(
        json.dumps({"receipts": receipts, "ocrExecutableAvailable": shutil.which("tesseract") is not None,
                    "limits": "Native PDFs only; not upload/worker/review, OCR or live identity verification."},
                   ensure_ascii=False, indent=2), encoding="utf-8",
    )
    print("2 native PDF HTTP probes passed; expected Partial outcomes and missing-row warnings preserved.")
    print("OCR executable available:", shutil.which("tesseract") is not None)


if __name__ == "__main__":
    main()
