"""Deterministic, provider-free chunking of an ASP.NET-pinned completed review.

This module does not fetch sources, authorize callers, normalize canonical text,
or infer facts. All source identities must be revalidated by the public backend.
"""
from __future__ import annotations

from bisect import bisect_left, bisect_right
from hashlib import sha256
import re
import unicodedata
from uuid import UUID


class ChunkingError(ValueError):
    pass


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ChunkingError(message)


def _id(value: object) -> str:
    try:
        result = UUID(str(value))
        _require(result.int != 0, "Empty source identity")
        return str(result)
    except (ValueError, TypeError, AttributeError) as error:
        raise ChunkingError("Invalid source identity") from error


def _integer(value: object, low: int, high: int) -> bool:
    return type(value) is int and low <= value <= high


def content_hash(text: str) -> str:
    return sha256(text.encode("utf-8")).hexdigest()


def chunk_id(index_id: str, ordinal: int, kind: str, digest: str) -> str:
    name = f"chunk:{UUID(index_id).hex}:{ordinal}:{kind}:{digest}"
    return str(UUID(hex=content_hash(name)[:32]))


def _page_ranges(text: str, maximum: int, overlap: int):
    # UTF-8 offsets are computed once. Never split a code point or rewrite text.
    offsets = [0]
    for character in text:
        offsets.append(offsets[-1] + len(character.encode("utf-8")))
    start = 0
    while start < len(text):
        end = bisect_right(offsets, offsets[start] + maximum) - 1
        if end < len(text):
            middle = bisect_left(offsets, offsets[start] + maximum // 2)
            newline = text.rfind("\n", middle, end)
            if newline >= middle:
                end = newline + 1
        yield offsets[start], offsets[end], text[start:end]
        if end == len(text):
            break
        start = max(start + 1, bisect_left(offsets, offsets[end] - overlap))


def chunk_document(snapshot: dict) -> dict:
    """Wire contract 1.0; output order is page number, then field key/occurrence."""
    _require(isinstance(snapshot, dict), "Snapshot must be an object")
    _require(snapshot.get("schemaVersion") == "1.0", "Unsupported chunking schema")
    _require(snapshot.get("reviewStatus") == "Completed", "Review must be completed")
    identities = {key: _id(snapshot.get(key)) for key in
                  ("indexId", "organizationId", "documentId", "extractionRunId", "reviewSessionId")}
    source_hash = snapshot.get("documentSha256")
    _require(isinstance(source_hash, str) and re.fullmatch(r"[0-9a-f]{64}", source_hash) is not None,
             "Invalid source hash")
    profile = snapshot.get("profile")
    _require(isinstance(profile, dict), "Missing profile")
    maximum, overlap = profile.get("maxBytes"), profile.get("overlapBytes")
    _require(_integer(maximum, 32, 65536), "Invalid chunk byte limit")
    _require(_integer(overlap, 0, maximum // 4), "Overlap must be at most one quarter")
    expected_profile = {"name": "page-lines", "version": f"1.0.0-b{maximum}-o{overlap}",
                        "maxBytes": maximum, "overlapBytes": overlap}
    _require(profile == expected_profile, "Profile identity does not match parameters")
    pages, facts = snapshot.get("pages"), snapshot.get("facts")
    _require(isinstance(pages, list) and len(pages) <= 2000, "Invalid page collection")
    _require(isinstance(facts, list) and len(facts) <= 5000, "Invalid fact collection")
    texts = {}
    total_bytes = 0
    for page in pages:
        _require(isinstance(page, dict), "Invalid page")
        number, text = page.get("pageNumber"), page.get("canonicalText")
        _require(_integer(number, 1, 100000) and number not in texts, "Duplicate or invalid page")
        _require(isinstance(text, str), "Invalid canonical text")
        _require("\r" not in text and unicodedata.normalize("NFC", text) == text,
                 "Canonical text must already use NFC and LF")
        try:
            length = len(text.encode("utf-8"))
        except UnicodeEncodeError as error:
            raise ChunkingError("Canonical text contains invalid Unicode") from error
        total_bytes += length
        _require(length <= 1048576 and total_bytes <= 8388608, "Source text byte limit exceeded")
        texts[number] = text

    chunks = []

    def append(kind: str, content: str, **metadata):
        _require(len(chunks) < 10000, "Chunk count limit exceeded")
        _require(content.strip() != "" and len(content.encode("utf-8")) <= maximum,
                 "Fact exceeds profile limit; do not truncate reviewed values")
        ordinal = len(chunks)
        digest = content_hash(content)
        chunks.append({"id": chunk_id(identities["indexId"], ordinal, kind, digest),
                       "ordinal": ordinal, "kind": kind, "content": content,
                       "contentSha256": digest, "pageNumber": None, "byteStart": None,
                       "byteEnd": None, "fieldKey": None, "occurrenceIndex": None,
                       "reviewActionId": None, "evidenceIds": [], **metadata})

    for number, text in sorted(texts.items()):
        for start, end, content in _page_ranges(text, maximum, overlap):
            if content.strip():
                append("PageText", content, pageNumber=number, byteStart=start, byteEnd=end)

    seen_fields, seen_actions, seen_evidence = set(), set(), set()
    validated_facts = []
    for fact in facts:
        _require(isinstance(fact, dict), "Invalid reviewed fact")
        key, occurrence = fact.get("fieldKey"), fact.get("occurrenceIndex")
        _require(isinstance(key, str) and 0 < len(key) <= 100 and key.strip() == key,
                 "Invalid field key")
        _require(_integer(occurrence, 0, 2147483647), "Invalid field occurrence")
        identity = (key, occurrence)
        action_id = _id(fact.get("reviewActionId"))
        _require(identity not in seen_fields and action_id not in seen_actions, "Duplicate effective fact")
        seen_fields.add(identity)
        seen_actions.add(action_id)
        action, value = fact.get("action"), fact.get("value")
        _require(action in {"Confirmed", "Corrected", "Added", "Cleared"}, "Invalid review action")
        _require(value is None if action == "Cleared" else isinstance(value, str) and bool(value.strip()),
                 "Invalid effective value")
        evidence = fact.get("evidence")
        _require(isinstance(evidence, list) and len(evidence) <= 1000, "Invalid fact evidence")
        _require(action != "Added" or not evidence, "Added fact cannot invent machine evidence")
        evidence_ids = []
        for item in evidence:
            _require(isinstance(item, dict), "Invalid evidence reference")
            evidence_id = _id(item.get("id"))
            _require(evidence_id not in seen_evidence, "Duplicate evidence reference")
            seen_evidence.add(evidence_id)
            number = item.get("pageNumber")
            _require(type(number) is int and number in texts, "Evidence page is outside source")
            start, end, quote = item.get("byteStart"), item.get("byteEnd"), item.get("quote")
            if start is not None or end is not None or quote is not None:
                page_bytes = texts[number].encode("utf-8")
                _require(_integer(start, 0, len(page_bytes)) and
                         _integer(end, start + 1, len(page_bytes)) and isinstance(quote, str),
                         "Invalid evidence byte span")
                _require(page_bytes[start:end] == quote.encode("utf-8"), "Evidence quote mismatch")
            # Region-only evidence retains its ID; no guessed quote or page span is emitted.
            evidence_ids.append(evidence_id)
        if action != "Cleared":
            validated_facts.append((key, occurrence, value, action_id, sorted(evidence_ids)))
    # Match ASP.NET's StringComparer.Ordinal for supplementary Unicode field keys too.
    for key, occurrence, value, action_id, evidence_ids in sorted(
            validated_facts, key=lambda item: (item[0].encode("utf-16-be"), item[1])):
        append("ReviewedFact", f"{key}[{occurrence}]: {value}", fieldKey=key,
               occurrenceIndex=occurrence, reviewActionId=action_id, evidenceIds=evidence_ids)
    _require(bool(chunks), "No indexable evidence")
    return {"schemaVersion": "1.0", **identities, "documentSha256": source_hash,
            "profile": expected_profile, "chunks": chunks}
