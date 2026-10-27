"""Assess structured table coverage without rerunning or changing frozen OCR."""
import argparse
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
directory = Path(__file__).resolve().parents[1] / "samples/phase-7/pdf-v0.1.0"
manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
truth = json.loads((directory / "line-items.json").read_text(encoding="utf-8"))
results = []
for receipt in manifest["receipts"]:
    raw = (directory / receipt["extractionFile"]).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == receipt["extractionSha256"]
    data = json.loads(raw)
    checks = []
    for row in truth["rows"]:
        for key, expected in row.items():
            if key == "occurrence":
                continue
            matches = [f for f in data["fields"] if f["key"] == key and f["occurrence"] == row["occurrence"]]
            actual = (matches[0]["normalized_value"] or matches[0]["value"]) if len(matches) == 1 else None
            checks.append({"field": key, "occurrence": row["occurrence"], "expected": expected,
                           "actual": actual, "passed": actual == expected})
    results.append({"source": receipt["source"], "checks": checks,
                    "passed": sum(c["passed"] for c in checks), "total": len(checks)})
args.output.parent.mkdir(parents=True, exist_ok=True)
with args.output.open("x", encoding="utf-8") as stream:
    json.dump({"scope": truth["scope"], "results": results}, stream, ensure_ascii=False, indent=2)
    stream.write("\n")
print(json.dumps([{ "source": r["source"]["name"], "passed": r["passed"], "total": r["total"] } for r in results]))
