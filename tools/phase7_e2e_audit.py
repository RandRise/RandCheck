"""Read-only audit of the disposable loopback upload/review/browser workflow."""
import argparse
import hashlib
import json
from pathlib import Path
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
API = "http://127.0.0.1:5327"


def get(path):
    with urlopen(API + path, timeout=30) as response:
        return json.load(response)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--payment-result", required=True)
    parser.add_argument("--english-result", required=True)
    parser.add_argument("--case-id", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError("Refusing to overwrite a workflow receipt")
    documents = get("/api/documents")
    by_name = {d["originalFilename"]: d for d in documents}
    manifest = json.loads((ROOT / "samples/phase-7/pdf-v0.1.0/manifest.json").read_text("utf-8"))
    english = json.loads((ROOT / "samples/phase-2/expected/native-two-page-invoice.expected.json").read_text("utf-8"))
    expectations = [("native-two-page-invoice.pdf", {
        f["key"]: f["normalized_value"] or f["value"] for f in english["fields"]},
        ROOT / "samples/phase-2/generated/native-two-page-invoice.pdf")]
    expectations.extend((r["source"]["name"], r["expected"],
                         ROOT / "output/pdf/phase-7-language-v0.1.0" / r["source"]["name"])
                        for r in manifest["receipts"])
    scored = []
    for name, expected, source in expectations:
        document = by_name[name]
        assert document["sha256"] == hashlib.sha256(source.read_bytes()).hexdigest()
        processing = get(f'/api/documents/{document["id"]}/processing')
        review = get(f'/api/documents/{document["id"]}/review')
        assert processing["status"] in {"Ready", "Partial"} and processing["attemptCount"] == 1
        actual = {f["fieldKey"]: f["machine"]["value"] for f in review["fields"]
                  if f.get("machine") and f["occurrenceIndex"] == 0}
        checks = {key: actual.get(key, "").replace(",", "") == value.replace(",", "")
                  for key, value in expected.items()}
        assert all(passed for key, passed in checks.items() if key != "payment_terms")
        scored.append({"filename": name, "documentId": document["id"], "sha256": document["sha256"],
                       "processing": processing, "actualMachineValues": actual, "expected": expected,
                       "checks": checks, "strictPassCount": sum(checks.values()), "fieldCount": len(checks),
                       "machineLineFields": sum(f.get("machine") is not None and f["fieldKey"].startswith("line_")
                                                for f in review["fields"]),
                       "review": review.get("session")})
    answers = []
    for result_id, expected_text, filename in (
        (args.payment_result, "Invoice payment terms: خلال 30 يوماً.", "mixed-native-two-page.pdf"),
        (args.english_result, "Invoice total: 1050.00 AED.", "native-two-page-invoice.pdf"),
    ):
        result = get(f"/api/cited-questions/{result_id}")
        assert result["reviewedAnswer"]["status"] == "Answered" and not result["answerGenerationEnabled"]
        assert result["reviewedAnswer"]["claims"][0]["text"] == expected_text
        evidence = []
        for hit in result["retrieval"]["hits"]:
            assert hit["documentId"] == by_name[filename]["id"]
            for span in hit["spans"]:
                detail = get(f'/api/cited-questions/{result_id}/hits/{hit["hitId"]}/evidence?spanId={span["id"]}')
                page = detail["page"]
                canonical = page["before"] + page["quote"] + page["after"]
                assert canonical.encode()[page["byteStart"]:page["byteEnd"]].decode() == page["quote"]
                if result_id == args.payment_result:
                    assert page["pageNumber"] == 2 and page["quote"] == "خلال 30 يوما"
                    assert detail["review"]["action"] == "Corrected"
                evidence.append(detail)
        assert evidence
        answers.append({"id": result_id, "text": expected_text, "exactUtf8Evidence": True, "evidence": evidence})
    histories = {name: get(f'/api/documents/{by_name[name]["id"]}/review-history')
                 for name in ("native-two-page-invoice.pdf", "mixed-native-two-page.pdf")}
    assert all(h["sessions"][0]["status"] == "Completed" for h in histories.values())
    case = get(f"/api/reconciliation-cases/{args.case_id}")
    assert case["name"] == "مقارنة تجريبية — PO-260801 / 2026-09-22"
    assert len(case["documents"]) == 3 and len(case["runs"][0]["findings"]) == 8
    finding = next(f for f in case["runs"][0]["findings"] if f["ruleCode"] == "AMT-002")
    assert finding["difference"] == 7665 and finding["tolerance"] == .01
    assert finding["resolutions"][-1]["reason"] == "اختبار عرض فقط — AED 7665.00 / PO-260801 / 2026-09-22"
    assert finding["resolutions"][-1]["action"] == "Dismissed"
    report = {"scenario": "Real HTTP uploads, durable worker, pinned local OCR, browser reviews/answers/reconciliation",
              "profile": "eng+ara/best/lines-v1", "invoices": scored, "answers": answers,
              "histories": histories, "reconciliation": case,
              "browserHistoryDownloadVerified": False,
              "limits": ["Fictional development samples, not a production failure-rate estimate",
                         "Mixed PDFs represent the same invoice", "Scanned invoice review not completed in this run",
                         "Quotation and purchase-order reviews prepared through HTTP; invoice reviews completed in browser",
                         "Reconciliation combines deliberately different fixtures for UI validation",
                         "History API works; automatic and explicit browser download events timed out"]}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
        stream.write("\n")
    print(json.dumps({"invoices": [{"name": r["filename"], "passed": r["strictPassCount"],
                                   "total": r["fieldCount"]} for r in scored],
                      "savedAnswers": len(answers), "findings": 8}))


if __name__ == "__main__":
    main()
