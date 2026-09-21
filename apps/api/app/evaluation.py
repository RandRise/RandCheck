import hashlib
import json
import math
import unicodedata
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any


DATASET_SCHEMA_VERSION = "1.0"
PREDICTION_SCHEMA_VERSION = "1.0"
REPORT_SCHEMA_VERSION = "1.0"
EVALUATOR_VERSION = "0.1.0"
READY_OUTCOME = "Ready"


class EvaluationValidationError(ValueError):
    """Raised when an evaluation input cannot support a trustworthy report."""


@dataclass(frozen=True)
class SampleScore:
    sample_id: str
    layout: str
    scan_type: str
    provider_version: str
    outcome: str
    truth: dict[tuple[str, int], dict[str, Any]]
    predictions: dict[tuple[str, int], dict[str, Any]]
    duration_ms: int | None
    estimated_cost: float | None
    cost_currency: str | None


def _require_mapping(value: Any, context: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise EvaluationValidationError(f"{context} must be an object")
    return value


def _require_list(value: Any, context: str) -> list[Any]:
    if not isinstance(value, list):
        raise EvaluationValidationError(f"{context} must be an array")
    return value


def _require_string(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise EvaluationValidationError(f"{context} must be a non-empty string")
    return value


def _validate_fields(value: Any, context: str) -> dict[tuple[str, int], dict[str, Any]]:
    fields = _require_list(value, context)
    validated: dict[tuple[str, int], dict[str, Any]] = {}
    for index, raw_field in enumerate(fields):
        field_context = f"{context}[{index}]"
        field = _require_mapping(raw_field, field_context)
        key = _require_string(field.get("key"), f"{field_context}.key")
        occurrence = field.get("occurrence")
        if not isinstance(occurrence, int) or isinstance(occurrence, bool) or occurrence < 0:
            raise EvaluationValidationError(
                f"{field_context}.occurrence must be a non-negative integer"
            )
        _require_string(field.get("value_type"), f"{field_context}.value_type")
        value_text = field.get("value")
        if not isinstance(value_text, str):
            raise EvaluationValidationError(f"{field_context}.value must be a string")
        normalized = field.get("normalized_value")
        if normalized is not None and not isinstance(normalized, str):
            raise EvaluationValidationError(
                f"{field_context}.normalized_value must be a string or null"
            )
        identity = (key, occurrence)
        if identity in validated:
            raise EvaluationValidationError(
                f"{context} contains duplicate field identity {key}[{occurrence}]"
            )
        validated[identity] = field
    return validated


def validate_dataset(payload: Any) -> dict[str, Any]:
    dataset = _require_mapping(payload, "dataset")
    if dataset.get("dataset_schema_version") != DATASET_SCHEMA_VERSION:
        raise EvaluationValidationError(
            f"dataset uses unsupported schema version {dataset.get('dataset_schema_version')!r}"
        )
    _require_string(dataset.get("dataset_id"), "dataset.dataset_id")
    _require_string(dataset.get("dataset_version"), "dataset.dataset_version")
    _require_string(dataset.get("purpose"), "dataset.purpose")
    claim_eligible = dataset.get("claim_eligible")
    if not isinstance(claim_eligible, bool):
        raise EvaluationValidationError("dataset.claim_eligible must be a boolean")
    exclusion = dataset.get("claim_exclusion_reason")
    if not claim_eligible:
        _require_string(exclusion, "dataset.claim_exclusion_reason")

    normalization = _require_mapping(dataset.get("normalization"), "dataset.normalization")
    if normalization != {"name": "strict-normalized-value", "version": "1.0"}:
        raise EvaluationValidationError(
            "dataset.normalization must select strict-normalized-value version 1.0"
        )

    samples = _require_list(dataset.get("samples"), "dataset.samples")
    if not samples:
        raise EvaluationValidationError("dataset.samples must contain at least one sample")
    sample_ids: set[str] = set()
    for index, raw_sample in enumerate(samples):
        context = f"dataset.samples[{index}]"
        sample = _require_mapping(raw_sample, context)
        sample_id = _require_string(sample.get("sample_id"), f"{context}.sample_id")
        if sample_id in sample_ids:
            raise EvaluationValidationError(f"dataset contains duplicate sample_id {sample_id!r}")
        sample_ids.add(sample_id)
        segments = _require_mapping(sample.get("segments"), f"{context}.segments")
        for segment in ("layout", "scan_type", "language"):
            _require_string(segments.get(segment), f"{context}.segments.{segment}")
        source = _require_mapping(sample.get("source"), f"{context}.source")
        _require_string(source.get("path"), f"{context}.source.path")
        sha256 = _require_string(source.get("sha256"), f"{context}.source.sha256")
        if len(sha256) != 64 or any(character not in "0123456789abcdef" for character in sha256):
            raise EvaluationValidationError(f"{context}.source.sha256 must be lowercase SHA-256")
        _require_string(source.get("license"), f"{context}.source.license")
        if not isinstance(source.get("synthetic"), bool):
            raise EvaluationValidationError(f"{context}.source.synthetic must be a boolean")
        if claim_eligible and source["synthetic"]:
            raise EvaluationValidationError(
                f"{context}.source.synthetic cannot be true in a claim-eligible dataset"
            )
        ground_truth = _require_mapping(sample.get("ground_truth"), f"{context}.ground_truth")
        _validate_fields(ground_truth.get("fields"), f"{context}.ground_truth.fields")
    return dataset


def verify_dataset_sources(
    dataset: dict[str, Any], manifest_directory: Path, allowed_root: Path
) -> None:
    resolved_root = allowed_root.resolve()
    for sample in dataset["samples"]:
        raw_path = Path(sample["source"]["path"])
        if raw_path.is_absolute():
            raise EvaluationValidationError(
                f"source path for {sample['sample_id']} must be relative"
            )
        resolved_path = (manifest_directory / raw_path).resolve()
        try:
            resolved_path.relative_to(resolved_root)
        except ValueError as error:
            raise EvaluationValidationError(
                f"source path for {sample['sample_id']} escapes the allowed root"
            ) from error
        if not resolved_path.is_file():
            raise EvaluationValidationError(
                f"source file for {sample['sample_id']} does not exist"
            )
        digest = hashlib.sha256(resolved_path.read_bytes()).hexdigest()
        if digest != sample["source"]["sha256"]:
            raise EvaluationValidationError(
                f"source SHA-256 for {sample['sample_id']} does not match"
            )


def validate_prediction_set(payload: Any, dataset: dict[str, Any]) -> dict[str, Any]:
    predictions = _require_mapping(payload, "prediction set")
    if predictions.get("prediction_schema_version") != PREDICTION_SCHEMA_VERSION:
        raise EvaluationValidationError(
            "prediction set uses an unsupported schema version"
        )
    _require_string(predictions.get("prediction_set_id"), "prediction_set.prediction_set_id")
    if predictions.get("dataset_id") != dataset["dataset_id"]:
        raise EvaluationValidationError("prediction set dataset_id does not match the dataset")
    if predictions.get("dataset_version") != dataset["dataset_version"]:
        raise EvaluationValidationError("prediction set dataset_version does not match the dataset")

    dataset_ids = {sample["sample_id"] for sample in dataset["samples"]}
    run_ids: set[str] = set()
    for index, raw_run in enumerate(_require_list(predictions.get("runs"), "prediction_set.runs")):
        context = f"prediction_set.runs[{index}]"
        run = _require_mapping(raw_run, context)
        sample_id = _require_string(run.get("sample_id"), f"{context}.sample_id")
        if sample_id not in dataset_ids:
            raise EvaluationValidationError(
                f"prediction set contains unknown sample_id {sample_id!r}"
            )
        if sample_id in run_ids:
            raise EvaluationValidationError(
                f"prediction set contains duplicate sample_id {sample_id!r}"
            )
        run_ids.add(sample_id)
        _require_string(run.get("outcome"), f"{context}.outcome")
        provider = _require_mapping(run.get("provider"), f"{context}.provider")
        for name in ("alias", "adapter_version", "model", "model_version"):
            _require_string(provider.get(name), f"{context}.provider.{name}")
        _validate_fields(run.get("fields"), f"{context}.fields")
        metrics = _require_mapping(run.get("metrics"), f"{context}.metrics")
        duration = metrics.get("provider_duration_ms")
        if duration is not None and (
            not isinstance(duration, int) or isinstance(duration, bool) or duration < 0
        ):
            raise EvaluationValidationError(
                f"{context}.metrics.provider_duration_ms must be a non-negative integer or null"
            )
        cost = metrics.get("estimated_cost")
        currency = metrics.get("cost_currency")
        if cost is not None:
            if (
                not isinstance(cost, (int, float))
                or isinstance(cost, bool)
                or not math.isfinite(cost)
                or cost < 0
            ):
                raise EvaluationValidationError(
                    f"{context}.metrics.estimated_cost must be a finite non-negative number"
                )
            _require_string(currency, f"{context}.metrics.cost_currency")
        elif currency is not None:
            raise EvaluationValidationError(
                f"{context}.metrics.cost_currency requires estimated_cost"
            )
    return predictions


def _provider_version(run: dict[str, Any] | None) -> str:
    if run is None:
        return "missing_prediction"
    provider = run["provider"]
    return "/".join(
        provider[name] for name in ("alias", "adapter_version", "model", "model_version")
    )


def _comparable(field: dict[str, Any]) -> tuple[str, str]:
    selected = field.get("normalized_value")
    if selected is None:
        selected = field["value"]
    return field["value_type"], unicodedata.normalize("NFC", selected)


def _canonical_sha256(payload: dict[str, Any]) -> str:
    rendered = json.dumps(
        payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return hashlib.sha256(rendered).hexdigest()


def _score_samples(
    dataset: dict[str, Any], predictions: dict[str, Any]
) -> list[SampleScore]:
    runs = {run["sample_id"]: run for run in predictions["runs"]}
    scores: list[SampleScore] = []
    for sample in dataset["samples"]:
        run = runs.get(sample["sample_id"])
        truth = _validate_fields(
            sample["ground_truth"]["fields"],
            f"ground truth for {sample['sample_id']}",
        )
        prediction_fields = (
            _validate_fields(run["fields"], f"predictions for {sample['sample_id']}")
            if run is not None
            else {}
        )
        metrics = run["metrics"] if run is not None else {}
        scores.append(
            SampleScore(
                sample_id=sample["sample_id"],
                layout=sample["segments"]["layout"],
                scan_type=sample["segments"]["scan_type"],
                provider_version=_provider_version(run),
                outcome=run["outcome"] if run is not None else "MissingPrediction",
                truth=truth,
                predictions=prediction_fields,
                duration_ms=metrics.get("provider_duration_ms"),
                estimated_cost=metrics.get("estimated_cost"),
                cost_currency=metrics.get("cost_currency"),
            )
        )
    return scores


def _ratio(numerator: int, denominator: int) -> float | None:
    return round(numerator / denominator, 6) if denominator else None


def _percentile(values: list[int], percentile: float) -> int | None:
    if not values:
        return None
    ordered = sorted(values)
    index = max(0, math.ceil(percentile * len(ordered)) - 1)
    return ordered[index]


def _metrics(scores: list[SampleScore], field_key: str | None = None) -> dict[str, Any]:
    expected = predicted = correct = incorrect = missing = unexpected = 0
    exact_documents = failures = 0
    durations: list[int] = []
    costs: dict[str, float] = defaultdict(float)
    cost_count = 0
    relevant_scores: list[SampleScore] = []

    for score in scores:
        truth = {
            identity: field
            for identity, field in score.truth.items()
            if field_key is None or identity[0] == field_key
        }
        predictions = {
            identity: field
            for identity, field in score.predictions.items()
            if field_key is None or identity[0] == field_key
        }
        if field_key is not None and not truth and not predictions:
            continue
        relevant_scores.append(score)
        truth_ids = set(truth)
        prediction_ids = set(predictions)
        shared = truth_ids & prediction_ids
        sample_correct = sum(
            _comparable(truth[identity]) == _comparable(predictions[identity])
            for identity in shared
        )
        sample_incorrect = len(shared) - sample_correct
        sample_missing = len(truth_ids - prediction_ids)
        sample_unexpected = len(prediction_ids - truth_ids)

        expected += len(truth_ids)
        predicted += len(prediction_ids)
        correct += sample_correct
        incorrect += sample_incorrect
        missing += sample_missing
        unexpected += sample_unexpected
        failed = score.outcome != READY_OUTCOME
        failures += failed
        if not failed and not (sample_incorrect or sample_missing or sample_unexpected):
            exact_documents += 1
        if score.duration_ms is not None:
            durations.append(score.duration_ms)
        if score.estimated_cost is not None and score.cost_currency is not None:
            costs[score.cost_currency] += score.estimated_cost
            cost_count += 1

    correction_actions = incorrect + missing + unexpected
    review_decisions = correct + correction_actions
    comparable_predictions = correct + incorrect
    return {
        "sample_count": len(relevant_scores),
        "expected_field_count": expected,
        "predicted_field_count": predicted,
        "correct_field_count": correct,
        "incorrect_field_count": incorrect,
        "missing_field_count": missing,
        "unexpected_field_count": unexpected,
        "exact_match": {
            "field_value_rate": _ratio(correct, comparable_predictions),
            "document_rate": _ratio(exact_documents, len(relevant_scores)),
        },
        "precision": _ratio(correct, predicted),
        "recall": _ratio(correct, expected),
        "correction_rate": _ratio(correction_actions, review_decisions),
        "failure_rate": _ratio(failures, len(relevant_scores)),
        "latency_ms": {
            "measured_sample_count": len(durations),
            "mean": round(sum(durations) / len(durations), 3) if durations else None,
            "p50": _percentile(durations, 0.50),
            "p95": _percentile(durations, 0.95),
        },
        "cost": {
            "measured_sample_count": cost_count,
            "coverage_rate": _ratio(cost_count, len(relevant_scores)),
            "totals_by_currency": {
                currency: round(total, 6) for currency, total in sorted(costs.items())
            },
        },
    }


def evaluate(dataset_payload: Any, prediction_payload: Any) -> dict[str, Any]:
    dataset = validate_dataset(dataset_payload)
    predictions = validate_prediction_set(prediction_payload, dataset)
    scores = _score_samples(dataset, predictions)

    field_keys = sorted(
        {identity[0] for score in scores for identity in score.truth | score.predictions}
    )

    def grouped(attribute: str) -> dict[str, Any]:
        groups: dict[str, list[SampleScore]] = defaultdict(list)
        for score in scores:
            groups[getattr(score, attribute)].append(score)
        return {name: _metrics(group) for name, group in sorted(groups.items())}

    return {
        "report_schema_version": REPORT_SCHEMA_VERSION,
        "evaluator_version": EVALUATOR_VERSION,
        "dataset": {
            "id": dataset["dataset_id"],
            "version": dataset["dataset_version"],
            "sample_count": len(dataset["samples"]),
            "purpose": dataset["purpose"],
            "claim_eligible": dataset["claim_eligible"],
            "claim_exclusion_reason": dataset.get("claim_exclusion_reason"),
        },
        "prediction_set_id": predictions["prediction_set_id"],
        "input_fingerprints": {
            "dataset_canonical_sha256": _canonical_sha256(dataset),
            "prediction_set_canonical_sha256": _canonical_sha256(predictions),
        },
        "normalization": dataset["normalization"],
        "metrics": _metrics(scores),
        "breakdowns": {
            "field": {field_key: _metrics(scores, field_key) for field_key in field_keys},
            "layout": grouped("layout"),
            "scan_type": grouped("scan_type"),
            "provider_version": grouped("provider_version"),
        },
        "methodology": {
            "field_identity": "key plus occurrence",
            "exact_value": (
                "value_type and Unicode-NFC normalized_value; value is used only when "
                "normalized_value is null"
            ),
            "precision": "exactly correct predictions divided by all predicted fields",
            "recall": "exactly correct predictions divided by all ground-truth fields",
            "correction_rate": (
                "incorrect, missing, and unexpected fields divided by all review decisions"
            ),
            "failure_rate": "non-Ready or missing prediction runs divided by dataset samples",
            "percentiles": "nearest-rank percentile over measured provider durations",
        },
    }
