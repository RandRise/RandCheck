from __future__ import annotations

import hashlib
import json
from collections import Counter
from pathlib import Path
from typing import Any


QUESTION_SET_SCHEMA_VERSION = "1.0"
AUDIT_SCHEMA_VERSION = "1.0"
AUDITOR_VERSION = "0.1.0"
ANSWER_STATUSES = {"Answered", "PartiallyAnswered", "InsufficientEvidence"}
REASON_CODES = {
    "no_relevant_evidence",
    "incomplete_evidence",
    "conflicting_evidence",
    "unsupported_scope",
    "unsafe_source",
}
CATEGORIES = {
    "single_document",
    "cross_document",
    "paraphrase",
    "unanswerable",
    "adversarial_isolation",
}


class CitedQuestionValidationError(ValueError):
    """Raised when a cited-question evaluation set is not trustworthy."""


def _mapping(value: Any, context: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise CitedQuestionValidationError(f"{context} must be an object")
    return value


def _list(value: Any, context: str) -> list[Any]:
    if not isinstance(value, list):
        raise CitedQuestionValidationError(f"{context} must be an array")
    return value


def _string(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise CitedQuestionValidationError(f"{context} must be a non-empty string")
    return value


def _sha256(value: Any, context: str) -> str:
    digest = _string(value, context)
    if len(digest) != 64 or any(character not in "0123456789abcdef" for character in digest):
        raise CitedQuestionValidationError(f"{context} must be a lowercase SHA-256")
    return digest


def _unique_strings(value: Any, context: str, *, allow_empty: bool = False) -> list[str]:
    items = _list(value, context)
    if not allow_empty and not items:
        raise CitedQuestionValidationError(f"{context} must not be empty")
    validated = [_string(item, f"{context}[{index}]") for index, item in enumerate(items)]
    if len(validated) != len(set(validated)):
        raise CitedQuestionValidationError(f"{context} must not contain duplicates")
    return validated


def _canonical_sha256(payload: dict[str, Any]) -> str:
    encoded = json.dumps(
        payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def validate_question_set(payload: Any) -> dict[str, Any]:
    dataset = _mapping(payload, "question set")
    if dataset.get("question_set_schema_version") != QUESTION_SET_SCHEMA_VERSION:
        raise CitedQuestionValidationError("question set uses an unsupported schema version")
    _string(dataset.get("dataset_id"), "question_set.dataset_id")
    _string(dataset.get("dataset_version"), "question_set.dataset_version")
    _string(dataset.get("purpose"), "question_set.purpose")
    if dataset.get("synthetic") is not True:
        raise CitedQuestionValidationError("development question set must declare synthetic=true")
    if dataset.get("claim_eligible") is not False:
        raise CitedQuestionValidationError(
            "synthetic cited-question evaluation cannot be claim eligible"
        )
    _string(dataset.get("claim_exclusion_reason"), "question_set.claim_exclusion_reason")

    pack = _mapping(dataset.get("source_pack"), "question_set.source_pack")
    for key in ("dataset_id", "dataset_version", "truth_set_path"):
        _string(pack.get(key), f"question_set.source_pack.{key}")
    _sha256(pack.get("truth_set_sha256"), "question_set.source_pack.truth_set_sha256")

    source_records = _list(dataset.get("sources"), "question_set.sources")
    if not source_records:
        raise CitedQuestionValidationError("question_set.sources must not be empty")
    sources: dict[str, dict[str, Any]] = {}
    source_pages: dict[str, set[int]] = {}
    for index, raw_source in enumerate(source_records):
        context = f"question_set.sources[{index}]"
        source = _mapping(raw_source, context)
        source_id = _string(source.get("source_id"), f"{context}.source_id")
        if source_id in sources:
            raise CitedQuestionValidationError(f"duplicate source_id {source_id!r}")
        sources[source_id] = source
        for key in (
            "organization_fixture",
            "review_fixture_id",
            "role",
            "path",
            "license",
        ):
            _string(source.get(key), f"{context}.{key}")
        _sha256(source.get("sha256"), f"{context}.sha256")
        if source.get("synthetic") is not True:
            raise CitedQuestionValidationError(f"{context}.synthetic must be true")
        canonicalization = _mapping(
            source.get("canonicalization"), f"{context}.canonicalization"
        )
        _string(canonicalization.get("adapter"), f"{context}.canonicalization.adapter")
        _string(canonicalization.get("version"), f"{context}.canonicalization.version")
        pages = _list(source.get("pages"), f"{context}.pages")
        if not pages:
            raise CitedQuestionValidationError(f"{context}.pages must not be empty")
        numbers: set[int] = set()
        for page_index, raw_page in enumerate(pages):
            page_context = f"{context}.pages[{page_index}]"
            page = _mapping(raw_page, page_context)
            number = page.get("page_number")
            if not isinstance(number, int) or isinstance(number, bool) or number < 1:
                raise CitedQuestionValidationError(
                    f"{page_context}.page_number must be a positive integer"
                )
            if number in numbers:
                raise CitedQuestionValidationError(
                    f"{context}.pages contains duplicate page {number}"
                )
            numbers.add(number)
            _sha256(page.get("canonical_text_sha256"), f"{page_context}.canonical_text_sha256")
        source_pages[source_id] = numbers

    requirements = _mapping(
        dataset.get("coverage_requirements"), "question_set.coverage_requirements"
    )
    required_counts: dict[str, int] = {}
    for category in CATEGORIES:
        count = requirements.get(category)
        if not isinstance(count, int) or isinstance(count, bool) or count < 1:
            raise CitedQuestionValidationError(
                f"question_set.coverage_requirements.{category} must be a positive integer"
            )
        required_counts[category] = count

    questions = _list(dataset.get("questions"), "question_set.questions")
    if not questions:
        raise CitedQuestionValidationError("question_set.questions must not be empty")
    question_ids: set[str] = set()
    category_counts: Counter[str] = Counter()
    for index, raw_question in enumerate(questions):
        context = f"question_set.questions[{index}]"
        question = _mapping(raw_question, context)
        question_id = _string(question.get("question_id"), f"{context}.question_id")
        if question_id in question_ids:
            raise CitedQuestionValidationError(f"duplicate question_id {question_id!r}")
        question_ids.add(question_id)
        category = _string(question.get("category"), f"{context}.category")
        if category not in CATEGORIES:
            raise CitedQuestionValidationError(f"{context}.category is unsupported")
        category_counts[category] += 1
        _string(question.get("question"), f"{context}.question")
        _unique_strings(question.get("security_tags"), f"{context}.security_tags", allow_empty=True)

        scope = _mapping(question.get("scope"), f"{context}.scope")
        organization = _string(
            scope.get("organization_fixture"), f"{context}.scope.organization_fixture"
        )
        allowed = _unique_strings(
            scope.get("allowed_source_ids"), f"{context}.scope.allowed_source_ids"
        )
        forbidden = _unique_strings(
            scope.get("forbidden_source_ids"),
            f"{context}.scope.forbidden_source_ids",
            allow_empty=True,
        )
        unknown = (set(allowed) | set(forbidden)) - set(sources)
        if unknown:
            raise CitedQuestionValidationError(
                f"{context}.scope contains unknown source IDs {sorted(unknown)}"
            )
        if set(allowed) & set(forbidden):
            raise CitedQuestionValidationError(
                f"{context}.scope allowed and forbidden sources must be disjoint"
            )
        if any(sources[source_id]["organization_fixture"] != organization for source_id in allowed):
            raise CitedQuestionValidationError(
                f"{context}.scope allowed source belongs to another organization"
            )
        if (
            category == "adversarial_isolation"
            and "cross_organization_retrieval" in question["security_tags"]
        ):
            if not forbidden:
                raise CitedQuestionValidationError(
                    f"{context} cross-organization case requires a forbidden source"
                )
            if any(
                sources[source_id]["organization_fixture"] == organization
                for source_id in forbidden
            ):
                raise CitedQuestionValidationError(
                    f"{context} forbidden isolation source must belong to another organization"
                )

        expected = _mapping(question.get("expected"), f"{context}.expected")
        status = _string(expected.get("status"), f"{context}.expected.status")
        if status not in ANSWER_STATUSES:
            raise CitedQuestionValidationError(f"{context}.expected.status is unsupported")
        reason = expected.get("reason_code")
        if reason is not None and reason not in REASON_CODES:
            raise CitedQuestionValidationError(f"{context}.expected.reason_code is unsupported")
        claims = _list(expected.get("claims"), f"{context}.expected.claims")
        if status == "InsufficientEvidence":
            if claims:
                raise CitedQuestionValidationError(
                    f"{context} insufficient-evidence result cannot contain claims"
                )
            if reason is None:
                raise CitedQuestionValidationError(
                    f"{context} insufficient-evidence result requires a reason code"
                )
        elif not claims:
            raise CitedQuestionValidationError(f"{context} answered result requires claims")

        claim_ids: set[str] = set()
        evidence_sources: set[str] = set()
        for claim_index, raw_claim in enumerate(claims):
            claim_context = f"{context}.expected.claims[{claim_index}]"
            claim = _mapping(raw_claim, claim_context)
            claim_id = _string(claim.get("claim_id"), f"{claim_context}.claim_id")
            if claim_id in claim_ids:
                raise CitedQuestionValidationError(
                    f"{context} contains duplicate claim_id {claim_id!r}"
                )
            claim_ids.add(claim_id)
            _string(claim.get("text"), f"{claim_context}.text")
            evidence = _list(claim.get("evidence"), f"{claim_context}.evidence")
            if not evidence:
                raise CitedQuestionValidationError(
                    f"{claim_context}.evidence must not be empty"
                )
            for evidence_index, raw_evidence in enumerate(evidence):
                evidence_context = f"{claim_context}.evidence[{evidence_index}]"
                item = _mapping(raw_evidence, evidence_context)
                source_id = _string(item.get("source_id"), f"{evidence_context}.source_id")
                if source_id not in allowed:
                    raise CitedQuestionValidationError(
                        f"{evidence_context}.source_id is outside the allowed corpus"
                    )
                evidence_sources.add(source_id)
                page_number = item.get("page_number")
                if page_number not in source_pages[source_id]:
                    raise CitedQuestionValidationError(
                        f"{evidence_context}.page_number is not present in the source"
                    )
                _string(item.get("quote"), f"{evidence_context}.quote")
                if item.get("provenance_type") != "PageText":
                    raise CitedQuestionValidationError(
                        f"{evidence_context}.provenance_type must be PageText"
                    )
                _string(item.get("field_key"), f"{evidence_context}.field_key")
                occurrence = item.get("occurrence")
                if (
                    not isinstance(occurrence, int)
                    or isinstance(occurrence, bool)
                    or occurrence < 0
                ):
                    raise CitedQuestionValidationError(
                        f"{evidence_context}.occurrence must be a non-negative integer"
                    )
        if category == "cross_document" and len(evidence_sources) < 2:
            raise CitedQuestionValidationError(
                f"{context} cross-document answer must cite at least two sources"
            )

    if dict(category_counts) != required_counts:
        raise CitedQuestionValidationError(
            "question category counts do not match coverage_requirements: "
            f"expected {required_counts}, actual {dict(category_counts)}"
        )
    return dataset


def _safe_repo_file(repo_root: Path, raw_path: str, context: str) -> Path:
    relative = Path(raw_path)
    if relative.is_absolute():
        raise CitedQuestionValidationError(f"{context} must be relative")
    root = repo_root.resolve()
    resolved = (root / relative).resolve()
    try:
        resolved.relative_to(root)
    except ValueError as error:
        raise CitedQuestionValidationError(f"{context} escapes the repository") from error
    if not resolved.is_file():
        raise CitedQuestionValidationError(f"{context} does not exist")
    return resolved


def _verify_digest(path: Path, expected: str, context: str) -> None:
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != expected:
        raise CitedQuestionValidationError(f"{context} SHA-256 does not match")


def audit_question_set(payload: Any, repo_root: Path) -> dict[str, Any]:
    dataset = validate_question_set(payload)
    pack = dataset["source_pack"]
    truth_set = _safe_repo_file(
        repo_root, pack["truth_set_path"], "question_set.source_pack.truth_set_path"
    )
    _verify_digest(
        truth_set, pack["truth_set_sha256"], "question_set.source_pack.truth_set_path"
    )

    try:
        import pdfplumber

        from app.local_extraction import LocalInvoiceExtractor
    except ImportError as error:
        raise CitedQuestionValidationError(
            "PDF dependencies are required to audit canonical page evidence"
        ) from error

    canonical_pages: dict[tuple[str, int], str] = {}
    unique_paths: dict[str, tuple[Path, list[dict[str, Any]]]] = {}
    for source in dataset["sources"]:
        source_path = _safe_repo_file(
            repo_root, source["path"], f"source {source['source_id']!r} path"
        )
        _verify_digest(source_path, source["sha256"], f"source {source['source_id']!r}")
        unique_paths.setdefault(source["path"], (source_path, source["pages"]))

    extracted_by_path: dict[str, dict[int, str]] = {}
    for raw_path, (source_path, expected_pages) in unique_paths.items():
        with pdfplumber.open(source_path) as document:
            extracted = {
                index: LocalInvoiceExtractor._native_page(page, index).canonical_text
                for index, page in enumerate(document.pages, start=1)
            }
        if set(extracted) != {page["page_number"] for page in expected_pages}:
            raise CitedQuestionValidationError(
                f"source {raw_path!r} page closure does not match"
            )
        for page in expected_pages:
            text = extracted[page["page_number"]]
            actual_hash = hashlib.sha256(text.encode("utf-8")).hexdigest()
            if actual_hash != page["canonical_text_sha256"]:
                raise CitedQuestionValidationError(
                    f"source {raw_path!r} page {page['page_number']} canonical hash does not match"
                )
        extracted_by_path[raw_path] = extracted

    for source in dataset["sources"]:
        for page_number, text in extracted_by_path[source["path"]].items():
            canonical_pages[(source["source_id"], page_number)] = text

    evidence_count = 0
    for question in dataset["questions"]:
        for claim in question["expected"]["claims"]:
            for evidence in claim["evidence"]:
                evidence_count += 1
                page_text = canonical_pages[(evidence["source_id"], evidence["page_number"])]
                occurrence_count = page_text.count(evidence["quote"])
                if occurrence_count != 1:
                    raise CitedQuestionValidationError(
                        f"question {question['question_id']!r} evidence quote must occur "
                        f"exactly once; found {occurrence_count}"
                    )

    category_counts = Counter(question["category"] for question in dataset["questions"])
    status_counts = Counter(question["expected"]["status"] for question in dataset["questions"])
    return {
        "audit_schema_version": AUDIT_SCHEMA_VERSION,
        "auditor_version": AUDITOR_VERSION,
        "dataset_id": dataset["dataset_id"],
        "dataset_version": dataset["dataset_version"],
        "dataset_canonical_sha256": _canonical_sha256(dataset),
        "synthetic": dataset["synthetic"],
        "claim_eligible": dataset["claim_eligible"],
        "source_count": len(dataset["sources"]),
        "unique_source_file_count": len(unique_paths),
        "question_count": len(dataset["questions"]),
        "evidence_label_count": evidence_count,
        "category_counts": dict(sorted(category_counts.items())),
        "status_counts": dict(sorted(status_counts.items())),
        "source_hashes_verified": True,
        "canonical_pages_verified": True,
        "evidence_quotes_verified": True,
        "passed": True,
    }
