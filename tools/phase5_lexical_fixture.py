"""Prepare versioned page-only inputs for the real PostgreSQL lexical evaluation test."""
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "apps/api"))
from app.cited_question_evaluation import audit_question_set
from app.local_extraction import LocalInvoiceExtractor
from app.cited_retrieval import lexical_plan
import pdfplumber


if __name__ == "__main__":
    dataset = json.loads((ROOT / "samples/phase-5/cited-questions-v0.1.0/question-set.json").read_text("utf-8"))
    audit = audit_question_set(dataset, ROOT)
    sources = []
    for source in dataset["sources"]:
        with pdfplumber.open(ROOT / source["path"]) as pdf:
            pages = [{"pageNumber": n, "canonicalText": LocalInvoiceExtractor._native_page(p, n).canonical_text}
                     for n, p in enumerate(pdf.pages, 1)]
        sources.append({"id": source["source_id"], "organization": source["organization_fixture"],
                        "sha256": source["sha256"], "pages": pages})
    questions = [{"id": q["question_id"], "category": q["category"], "question": q["question"],
                  "organization": q["scope"]["organization_fixture"],
                  "allowed": q["scope"]["allowed_source_ids"], "expected": q["expected"],
                  "plan": lexical_plan(q["question"], 5)} for q in dataset["questions"]]
    result = {"schemaVersion": "1.0", "synthetic": True, "claimEligible": False,
              "questionSetSha256": audit["dataset_canonical_sha256"], "sources": sources, "questions": questions}
    output = ROOT / "samples/phase-5/lexical-v1/evaluation-input.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", "utf-8")
    print(f"Prepared {len(sources)} sources and {len(questions)} questions: {output}")
