import hashlib
import json
from datetime import datetime, timezone
from typing import Any

from .evaluation import EvaluationValidationError, validate_dataset


SOURCE_REGISTER_SCHEMA_VERSION = "1.0"
ANNOTATION_LEDGER_SCHEMA_VERSION = "1.0"
AUDIT_SCHEMA_VERSION = "1.0"
AUDITOR_VERSION = "0.1.0"
PROVENANCE_TYPES = {
    "organization_owned",
    "public_licensed",
    "synthetic",
    "written_permission",
}
REDACTION_STATUSES = {"completed", "not_required", "pending", "prohibited"}
STORAGE_CLASSIFICATIONS = {"internal", "public", "restricted"}
VERIFICATION_DISPOSITIONS = {"accepted", "changes_required"}


def canonical_sha256(payload: Any) -> str:
    rendered = json.dumps(
        payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return hashlib.sha256(rendered).hexdigest()


def _mapping(value: Any, context: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise EvaluationValidationError(f"{context} must be an object")
    return value


def _list(value: Any, context: str) -> list[Any]:
    if not isinstance(value, list):
        raise EvaluationValidationError(f"{context} must be an array")
    return value


def _string(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise EvaluationValidationError(f"{context} must be a non-empty string")
    return value


def _boolean(value: Any, context: str) -> bool:
    if not isinstance(value, bool):
        raise EvaluationValidationError(f"{context} must be a boolean")
    return value


def _sha256(value: Any, context: str) -> str:
    digest = _string(value, context)
    if len(digest) != 64 or any(
        character not in "0123456789abcdef" for character in digest
    ):
        raise EvaluationValidationError(f"{context} must be lowercase SHA-256")
    return digest


def _timestamp(value: Any, context: str) -> str:
    timestamp = _string(value, context)
    if not timestamp.endswith("Z"):
        raise EvaluationValidationError(f"{context} must be an explicit UTC timestamp")
    try:
        parsed = datetime.fromisoformat(timestamp.removesuffix("Z") + "+00:00")
    except ValueError as error:
        raise EvaluationValidationError(f"{context} must be an ISO-8601 timestamp") from error
    if parsed.tzinfo != timezone.utc:
        raise EvaluationValidationError(f"{context} must use UTC")
    return timestamp


def _utc_datetime(value: str) -> datetime:
    return datetime.fromisoformat(value.removesuffix("Z") + "+00:00")


def _enum(value: Any, allowed: set[str], context: str) -> str:
    selected = _string(value, context)
    if selected not in allowed:
        raise EvaluationValidationError(
            f"{context} must be one of {', '.join(sorted(allowed))}"
        )
    return selected


def _dataset_match(
    artifact: dict[str, Any], dataset: dict[str, Any], context: str
) -> None:
    if artifact.get("dataset_id") != dataset["dataset_id"]:
        raise EvaluationValidationError(f"{context}.dataset_id does not match the dataset")
    if artifact.get("dataset_version") != dataset["dataset_version"]:
        raise EvaluationValidationError(
            f"{context}.dataset_version does not match the dataset"
        )


def validate_source_register(
    payload: Any, dataset: dict[str, Any]
) -> dict[str, Any]:
    register = _mapping(payload, "source register")
    if register.get("source_register_schema_version") != SOURCE_REGISTER_SCHEMA_VERSION:
        raise EvaluationValidationError(
            "source register uses an unsupported schema version"
        )
    _string(register.get("register_id"), "source_register.register_id")
    _dataset_match(register, dataset, "source_register")
    sources = _list(register.get("sources"), "source_register.sources")
    sample_ids: set[str] = set()
    for index, raw_source in enumerate(sources):
        context = f"source_register.sources[{index}]"
        source = _mapping(raw_source, context)
        sample_id = _string(source.get("sample_id"), f"{context}.sample_id")
        if sample_id in sample_ids:
            raise EvaluationValidationError(
                f"source register contains duplicate sample_id {sample_id!r}"
            )
        sample_ids.add(sample_id)
        _string(source.get("asset_id"), f"{context}.asset_id")
        _sha256(source.get("sha256"), f"{context}.sha256")
        size = source.get("bytes")
        if not isinstance(size, int) or isinstance(size, bool) or size <= 0:
            raise EvaluationValidationError(f"{context}.bytes must be a positive integer")
        _string(source.get("media_type"), f"{context}.media_type")
        _timestamp(source.get("acquired_at_utc"), f"{context}.acquired_at_utc")
        _enum(source.get("provenance_type"), PROVENANCE_TYPES, f"{context}.provenance_type")

        rights = _mapping(source.get("rights"), f"{context}.rights")
        _string(rights.get("reference"), f"{context}.rights.reference")
        for permission in ("store", "label", "process", "publish_aggregate_results"):
            _boolean(rights.get(permission), f"{context}.rights.{permission}")

        privacy = _mapping(source.get("privacy"), f"{context}.privacy")
        _boolean(
            privacy.get("contains_personal_data"),
            f"{context}.privacy.contains_personal_data",
        )
        _boolean(
            privacy.get("contains_confidential_data"),
            f"{context}.privacy.contains_confidential_data",
        )
        _enum(
            privacy.get("redaction_status"),
            REDACTION_STATUSES,
            f"{context}.privacy.redaction_status",
        )
        _string(
            privacy.get("review_reference"),
            f"{context}.privacy.review_reference",
        )

        storage = _mapping(source.get("storage"), f"{context}.storage")
        _enum(
            storage.get("classification"),
            STORAGE_CLASSIFICATIONS,
            f"{context}.storage.classification",
        )
        _boolean(storage.get("committed_to_git"), f"{context}.storage.committed_to_git")
        _string(
            storage.get("location_reference"),
            f"{context}.storage.location_reference",
        )

    dataset_ids = {sample["sample_id"] for sample in dataset["samples"]}
    if sample_ids != dataset_ids:
        missing = sorted(dataset_ids - sample_ids)
        extra = sorted(sample_ids - dataset_ids)
        raise EvaluationValidationError(
            f"source register sample closure mismatch; missing={missing}, extra={extra}"
        )
    return register


def _validate_review(value: Any, context: str) -> dict[str, Any]:
    review = _mapping(value, context)
    _string(review.get("reviewer_id"), f"{context}.reviewer_id")
    _timestamp(review.get("completed_at_utc"), f"{context}.completed_at_utc")
    return review


def validate_annotation_ledger(
    payload: Any, dataset: dict[str, Any]
) -> dict[str, Any]:
    ledger = _mapping(payload, "annotation ledger")
    if ledger.get("annotation_ledger_schema_version") != ANNOTATION_LEDGER_SCHEMA_VERSION:
        raise EvaluationValidationError(
            "annotation ledger uses an unsupported schema version"
        )
    _string(ledger.get("ledger_id"), "annotation_ledger.ledger_id")
    _dataset_match(ledger, dataset, "annotation_ledger")
    _string(
        ledger.get("annotation_guide_version"),
        "annotation_ledger.annotation_guide_version",
    )
    _string(
        ledger.get("field_schema_version"),
        "annotation_ledger.field_schema_version",
    )
    records = _list(ledger.get("records"), "annotation_ledger.records")
    sample_ids: set[str] = set()
    dataset_samples = {sample["sample_id"]: sample for sample in dataset["samples"]}
    for index, raw_record in enumerate(records):
        context = f"annotation_ledger.records[{index}]"
        record = _mapping(raw_record, context)
        sample_id = _string(record.get("sample_id"), f"{context}.sample_id")
        if sample_id in sample_ids:
            raise EvaluationValidationError(
                f"annotation ledger contains duplicate sample_id {sample_id!r}"
            )
        if sample_id not in dataset_samples:
            raise EvaluationValidationError(
                f"annotation ledger contains unknown sample_id {sample_id!r}"
            )
        sample_ids.add(sample_id)
        initial = _validate_review(
            record.get("initial_annotation"), f"{context}.initial_annotation"
        )
        _sha256(
            initial.get("fields_canonical_sha256"),
            f"{context}.initial_annotation.fields_canonical_sha256",
        )
        verification = _validate_review(
            record.get("independent_verification"),
            f"{context}.independent_verification",
        )
        disposition = _enum(
            verification.get("disposition"),
            VERIFICATION_DISPOSITIONS,
            f"{context}.independent_verification.disposition",
        )
        _string(
            verification.get("reason"),
            f"{context}.independent_verification.reason",
        )
        if initial["reviewer_id"] == verification["reviewer_id"]:
            raise EvaluationValidationError(
                f"{context} initial annotator and verifier must be different"
            )
        if _utc_datetime(verification["completed_at_utc"]) < _utc_datetime(
            initial["completed_at_utc"]
        ):
            raise EvaluationValidationError(
                f"{context} verification cannot finish before initial annotation"
            )

        adjudication = record.get("adjudication")
        if disposition == "changes_required" and adjudication is None:
            raise EvaluationValidationError(
                f"{context}.adjudication is required when changes are required"
            )
        if adjudication is not None:
            adjudication = _validate_review(adjudication, f"{context}.adjudication")
            _string(adjudication.get("reason"), f"{context}.adjudication.reason")
            if adjudication["reviewer_id"] in {
                initial["reviewer_id"],
                verification["reviewer_id"],
            }:
                raise EvaluationValidationError(
                    f"{context} adjudicator must be independent of both reviewers"
                )
            if _utc_datetime(adjudication["completed_at_utc"]) < _utc_datetime(
                verification["completed_at_utc"]
            ):
                raise EvaluationValidationError(
                    f"{context} adjudication cannot finish before verification"
                )
        revision = record.get("final_label_revision")
        if not isinstance(revision, int) or isinstance(revision, bool) or revision < 1:
            raise EvaluationValidationError(
                f"{context}.final_label_revision must be a positive integer"
            )
        if disposition == "changes_required" and revision < 2:
            raise EvaluationValidationError(
                f"{context}.final_label_revision must be at least 2 after adjudication"
            )
        final_hash = _sha256(
            record.get("final_fields_canonical_sha256"),
            f"{context}.final_fields_canonical_sha256",
        )
        expected_hash = canonical_sha256(
            dataset_samples[sample_id]["ground_truth"]["fields"]
        )
        if final_hash != expected_hash:
            raise EvaluationValidationError(
                f"{context}.final_fields_canonical_sha256 does not match ground truth"
            )

    if sample_ids != set(dataset_samples):
        missing = sorted(set(dataset_samples) - sample_ids)
        raise EvaluationValidationError(
            f"annotation ledger sample closure mismatch; missing={missing}"
        )
    return ledger


def audit_dataset_readiness(
    dataset_payload: Any,
    source_register_payload: Any,
    annotation_ledger_payload: Any,
) -> dict[str, Any]:
    dataset = validate_dataset(dataset_payload)
    register = validate_source_register(source_register_payload, dataset)
    ledger = validate_annotation_ledger(annotation_ledger_payload, dataset)
    sources = {source["sample_id"]: source for source in register["sources"]}
    blockers: list[dict[str, Any]] = []

    def block(code: str, summary: str, sample_id: str | None = None) -> None:
        blocker = {"code": code, "summary": summary}
        if sample_id is not None:
            blocker["sample_id"] = sample_id
        blockers.append(blocker)

    if not dataset["claim_eligible"]:
        block(
            "dataset_not_claim_eligible",
            "The dataset manifest explicitly excludes performance claims.",
        )
    for sample in dataset["samples"]:
        sample_id = sample["sample_id"]
        source = sources[sample_id]
        if source["sha256"] != sample["source"]["sha256"]:
            block(
                "source_hash_mismatch",
                "The source register and dataset manifest SHA-256 values differ.",
                sample_id,
            )
        if source["provenance_type"] == "synthetic" or sample["source"]["synthetic"]:
            block(
                "synthetic_source",
                "Synthetic sources cannot support real-world performance claims.",
                sample_id,
            )
        for permission in ("store", "label", "process", "publish_aggregate_results"):
            if not source["rights"][permission]:
                block(
                    f"rights_{permission}_missing",
                    f"The recorded rights do not permit {permission.replace('_', ' ')}.",
                    sample_id,
                )
        privacy = source["privacy"]
        if privacy["redaction_status"] in {"pending", "prohibited"}:
            block(
                "privacy_review_incomplete",
                "Privacy review or required redaction is not complete.",
                sample_id,
            )
        if (
            privacy["contains_personal_data"]
            or privacy["contains_confidential_data"]
        ) and privacy["redaction_status"] != "completed":
            block(
                "sensitive_data_not_redacted",
                "Sensitive data requires completed redaction before benchmark use.",
                sample_id,
            )
        storage = source["storage"]
        if storage["classification"] == "restricted" and storage["committed_to_git"]:
            block(
                "restricted_source_in_git",
                "A restricted source must not be committed to Git.",
                sample_id,
            )

    blockers.sort(key=lambda item: (item.get("sample_id", ""), item["code"]))
    return {
        "audit_schema_version": AUDIT_SCHEMA_VERSION,
        "auditor_version": AUDITOR_VERSION,
        "dataset": {
            "id": dataset["dataset_id"],
            "version": dataset["dataset_version"],
            "sample_count": len(dataset["samples"]),
            "claim_eligible": dataset["claim_eligible"],
        },
        "input_fingerprints": {
            "dataset_canonical_sha256": canonical_sha256(dataset),
            "source_register_canonical_sha256": canonical_sha256(register),
            "annotation_ledger_canonical_sha256": canonical_sha256(ledger),
        },
        "checks": {
            "source_sample_closure": True,
            "annotation_sample_closure": True,
            "independent_verification": True,
            "final_label_hashes": True,
        },
        "claim_ready": not blockers,
        "blocker_count": len(blockers),
        "blockers": blockers,
    }
