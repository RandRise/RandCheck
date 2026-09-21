"""Audit chunk evidence coverage on the existing synthetic question set; no retrieval/model call."""
from __future__ import annotations

import argparse
from hashlib import sha256
import json
from pathlib import Path
import sys
from uuid import NAMESPACE_URL, uuid5

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "apps" / "api"))

from app.cited_chunking import chunk_document
from app.cited_question_evaluation import audit_question_set


def audit_chunks(dataset: dict, maximum: int = 2048, overlap: int = 256) -> dict:
    import pdfplumber
    from app.local_extraction import LocalInvoiceExtractor

    source_audit = audit_question_set(dataset, ROOT)
    indexes, pages_by_path, summaries = {}, {}, []
    seen_ids = set()
    for source in dataset["sources"]:
        path = source["path"]
        if path not in pages_by_path:
            with pdfplumber.open(ROOT / path) as document:
                pages_by_path[path] = [{"pageNumber": number, "canonicalText":
                    LocalInvoiceExtractor._native_page(page, number).canonical_text}
                    for number, page in enumerate(document.pages, 1)]
        source_id = source["source_id"]

        def identity(kind, value):
            return str(uuid5(NAMESPACE_URL, f"phase5-chunk-audit:{kind}:{value}"))

        snapshot = {
            "schemaVersion": "1.0", "indexId": identity("index", f"{source_id}:{maximum}:{overlap}"),
            "organizationId": identity("org", source["organization_fixture"]),
            "documentId": identity("document", source_id), "extractionRunId": identity("run", source_id),
            "reviewSessionId": identity("review", source["review_fixture_id"]), "reviewStatus": "Completed",
            "documentSha256": source["sha256"], "profile": {"name": "page-lines",
                "version": f"1.0.0-b{maximum}-o{overlap}", "maxBytes": maximum, "overlapBytes": overlap},
            "pages": pages_by_path[path], "facts": []}
        result = chunk_document(snapshot)
        if result != chunk_document(snapshot):
            raise ValueError("Chunk replay changed")
        ids = {chunk["id"] for chunk in result["chunks"]}
        if seen_ids & ids:
            raise ValueError("Source indexes share chunk identities")
        seen_ids.update(ids)
        # Independently prove source-byte coverage, including gaps between emitted chunks.
        for page in snapshot["pages"]:
            raw = page["canonicalText"].encode("utf-8")
            end = 0
            for chunk in result["chunks"]:
                if chunk["pageNumber"] != page["pageNumber"]:
                    continue
                start, stop = chunk["byteStart"], chunk["byteEnd"]
                if raw[start:stop] != chunk["content"].encode("utf-8") or raw[end:start].decode("utf-8").strip():
                    raise ValueError("Page coverage or exact bytes changed")
                end = max(end, stop)
            if raw[end:].decode("utf-8").strip():
                raise ValueError("Page tail omitted")
        indexes[source_id] = result
        summaries.append({"source_id": source_id, "chunk_count": len(result["chunks"]),
                          "index_manifest_sha256": sha256(json.dumps(result, sort_keys=True,
                              ensure_ascii=False, separators=(",", ":")).encode("utf-8")).hexdigest()})
    labels, covered, misses = 0, 0, []
    for question in dataset["questions"]:
        for claim in question["expected"]["claims"]:
            for evidence in claim["evidence"]:
                labels += 1
                candidates = indexes[evidence["source_id"]]["chunks"]
                if any(chunk["pageNumber"] == evidence["page_number"] and
                       evidence["quote"] in chunk["content"] for chunk in candidates):
                    covered += 1
                else:
                    misses.append({"question_id": question["question_id"], "source_id": evidence["source_id"],
                                   "page_number": evidence["page_number"], "quote": evidence["quote"]})
    return {"audit_schema_version": "1.0", "dataset_id": dataset["dataset_id"],
            "dataset_version": dataset["dataset_version"], "synthetic": True, "claim_eligible": False,
            "question_set_sha256": source_audit["dataset_canonical_sha256"],
            "profile": {"name": "page-lines", "version": f"1.0.0-b{maximum}-o{overlap}",
                        "maxBytes": maximum, "overlapBytes": overlap},
            "sources": summaries, "total_chunks": len(seen_ids), "evidence_labels": labels,
            "labels_contained_in_one_chunk": covered, "misses": misses,
            "byte_coverage_verified": True, "deterministic_replay_verified": True,
            "source_index_ids_disjoint": True, "passed": not misses,
            "limitations": "Page chunk coverage only; fixture review IDs are synthetic. No retrieval recall, "
                           "model quality, live database indexing, real-tenant authorization, or compliance claim."}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--max-bytes", type=int, default=2048)
    parser.add_argument("--overlap-bytes", type=int, default=256)
    args = parser.parse_args()
    dataset = json.loads((ROOT / "samples/phase-5/cited-questions-v0.1.0/question-set.json").read_text("utf-8"))
    report = audit_chunks(dataset, args.max_bytes, args.overlap_bytes)
    serialized = json.dumps(report, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(serialized, encoding="utf-8")
    print(serialized)
    raise SystemExit(0 if report["passed"] else 1)
