import hashlib
import json
import math
from typing import Any

from .evaluation import EvaluationValidationError, REPORT_SCHEMA_VERSION


THRESHOLD_SCHEMA_VERSION = "1.0"
COMPARISON_SCHEMA_VERSION = "1.0"
COMPARATOR_VERSION = "0.1.0"

HIGHER_IS_BETTER = {
    "exact_match.document_rate",
    "exact_match.field_value_rate",
    "precision",
    "recall",
}
LOWER_IS_BETTER = {
    "correction_rate",
    "failure_rate",
    "latency_ms.mean",
    "latency_ms.p50",
    "latency_ms.p95",
}
SUPPORTED_METRICS = HIGHER_IS_BETTER | LOWER_IS_BETTER
SUPPORTED_BREAKDOWNS = ("field", "layout", "scan_type")


def _mapping(value: Any, context: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise EvaluationValidationError(f"{context} must be an object")
    return value


def _string(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise EvaluationValidationError(f"{context} must be a non-empty string")
    return value


def _finite_number(value: Any, context: str) -> float:
    if (
        not isinstance(value, (int, float))
        or isinstance(value, bool)
        or not math.isfinite(value)
    ):
        raise EvaluationValidationError(f"{context} must be a finite number")
    return float(value)


def _canonical_sha256(payload: dict[str, Any]) -> str:
    rendered = json.dumps(
        payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return hashlib.sha256(rendered).hexdigest()


def validate_report(report_payload: Any, context: str) -> dict[str, Any]:
    report = _mapping(report_payload, context)
    if report.get("report_schema_version") != REPORT_SCHEMA_VERSION:
        raise EvaluationValidationError(
            f"{context} uses an unsupported report schema version"
        )
    dataset = _mapping(report.get("dataset"), f"{context}.dataset")
    for name in ("id", "version"):
        _string(dataset.get(name), f"{context}.dataset.{name}")
    sample_count = dataset.get("sample_count")
    if not isinstance(sample_count, int) or isinstance(sample_count, bool) or sample_count < 1:
        raise EvaluationValidationError(
            f"{context}.dataset.sample_count must be a positive integer"
        )
    fingerprints = _mapping(
        report.get("input_fingerprints"), f"{context}.input_fingerprints"
    )
    for name in ("dataset_canonical_sha256", "prediction_set_canonical_sha256"):
        fingerprint = _string(
            fingerprints.get(name), f"{context}.input_fingerprints.{name}"
        )
        if len(fingerprint) != 64 or any(
            character not in "0123456789abcdef" for character in fingerprint
        ):
            raise EvaluationValidationError(
                f"{context} {name} must be lowercase SHA-256"
            )
    _string(report.get("prediction_set_id"), f"{context}.prediction_set_id")
    _mapping(report.get("normalization"), f"{context}.normalization")
    _mapping(report.get("metrics"), f"{context}.metrics")
    breakdowns = _mapping(report.get("breakdowns"), f"{context}.breakdowns")
    for breakdown in (*SUPPORTED_BREAKDOWNS, "provider_version"):
        _mapping(breakdowns.get(breakdown), f"{context}.breakdowns.{breakdown}")
    return report


def validate_thresholds(payload: Any) -> dict[str, Any]:
    thresholds = _mapping(payload, "threshold set")
    if thresholds.get("threshold_schema_version") != THRESHOLD_SCHEMA_VERSION:
        raise EvaluationValidationError(
            "threshold set uses an unsupported schema version"
        )
    _string(thresholds.get("threshold_set_id"), "threshold_set.threshold_set_id")
    rules = thresholds.get("rules")
    if not isinstance(rules, list) or not rules:
        raise EvaluationValidationError(
            "threshold_set.rules must contain at least one rule"
        )
    rule_ids: set[str] = set()
    for index, raw_rule in enumerate(rules):
        context = f"threshold_set.rules[{index}]"
        rule = _mapping(raw_rule, context)
        rule_id = _string(rule.get("rule_id"), f"{context}.rule_id")
        if rule_id in rule_ids:
            raise EvaluationValidationError(
                f"threshold set contains duplicate rule_id {rule_id!r}"
            )
        rule_ids.add(rule_id)
        scope = _string(rule.get("scope"), f"{context}.scope")
        _validate_scope(scope, context)
        metric = _string(rule.get("metric"), f"{context}.metric")
        if metric not in SUPPORTED_METRICS:
            raise EvaluationValidationError(
                f"{context}.metric is not supported: {metric!r}"
            )
        constraints = (
            "minimum_candidate",
            "maximum_candidate",
            "maximum_regression",
        )
        present = [name for name in constraints if rule.get(name) is not None]
        if not present:
            raise EvaluationValidationError(
                f"{context} must define at least one threshold constraint"
            )
        for name in present:
            number = _finite_number(rule[name], f"{context}.{name}")
            if number < 0:
                raise EvaluationValidationError(
                    f"{context}.{name} must be non-negative"
                )
        minimum = rule.get("minimum_candidate")
        maximum = rule.get("maximum_candidate")
        if minimum is not None and maximum is not None and minimum > maximum:
            raise EvaluationValidationError(
                f"{context}.minimum_candidate cannot exceed maximum_candidate"
            )
    return thresholds


def _validate_scope(scope: str, context: str) -> None:
    if scope == "overall":
        return
    prefix, separator, name = scope.partition(".")
    if not separator or prefix not in SUPPORTED_BREAKDOWNS or not name:
        raise EvaluationValidationError(
            f"{context}.scope must be overall or one field/layout/scan_type group"
        )


def _scope_metrics(report: dict[str, Any], scope: str) -> dict[str, Any]:
    if scope == "overall":
        return report["metrics"]
    breakdown, _, name = scope.partition(".")
    metrics = report["breakdowns"][breakdown].get(name)
    if not isinstance(metrics, dict):
        raise EvaluationValidationError(
            f"comparison scope {scope!r} is missing from one report"
        )
    return metrics


def _metric_value(metrics: dict[str, Any], metric: str) -> float | None:
    value: Any = metrics
    for component in metric.split("."):
        if not isinstance(value, dict) or component not in value:
            raise EvaluationValidationError(
                f"metric {metric!r} is missing from an evaluation report"
            )
        value = value[component]
    if value is None:
        return None
    return _finite_number(value, f"metric {metric}")


def _metric_comparison(
    baseline_metrics: dict[str, Any],
    candidate_metrics: dict[str, Any],
    metric: str,
) -> dict[str, Any]:
    baseline = _metric_value(baseline_metrics, metric)
    candidate = _metric_value(candidate_metrics, metric)
    delta = (
        round(candidate - baseline, 6)
        if baseline is not None and candidate is not None
        else None
    )
    favorable_delta = (
        delta
        if metric in HIGHER_IS_BETTER
        else round(-delta, 6) if delta is not None else None
    )
    if favorable_delta == 0:
        favorable_delta = 0.0
    return {
        "baseline": baseline,
        "candidate": candidate,
        "delta": delta,
        "favorable_delta": favorable_delta,
        "direction": "higher_is_better"
        if metric in HIGHER_IS_BETTER
        else "lower_is_better",
    }


def _all_metric_deltas(
    baseline_metrics: dict[str, Any], candidate_metrics: dict[str, Any]
) -> dict[str, Any]:
    return {
        metric: _metric_comparison(baseline_metrics, candidate_metrics, metric)
        for metric in sorted(SUPPORTED_METRICS)
    }


def _compare_breakdowns(
    baseline: dict[str, Any], candidate: dict[str, Any]
) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for breakdown in SUPPORTED_BREAKDOWNS:
        baseline_groups = baseline["breakdowns"][breakdown]
        candidate_groups = candidate["breakdowns"][breakdown]
        shared = sorted(set(baseline_groups) & set(candidate_groups))
        result[breakdown] = {
            "shared": {
                group: _all_metric_deltas(
                    baseline_groups[group], candidate_groups[group]
                )
                for group in shared
            },
            "baseline_only": sorted(set(baseline_groups) - set(candidate_groups)),
            "candidate_only": sorted(set(candidate_groups) - set(baseline_groups)),
        }
    return result


def _evaluate_rule(
    rule: dict[str, Any], baseline: dict[str, Any], candidate: dict[str, Any]
) -> dict[str, Any]:
    comparison = _metric_comparison(
        _scope_metrics(baseline, rule["scope"]),
        _scope_metrics(candidate, rule["scope"]),
        rule["metric"],
    )
    violations: list[str] = []
    candidate_value = comparison["candidate"]
    favorable_delta = comparison["favorable_delta"]
    if candidate_value is None:
        violations.append("candidate metric is null")
    else:
        minimum = rule.get("minimum_candidate")
        maximum = rule.get("maximum_candidate")
        if minimum is not None and candidate_value < minimum:
            violations.append(
                f"candidate {candidate_value} is below minimum {minimum}"
            )
        if maximum is not None and candidate_value > maximum:
            violations.append(
                f"candidate {candidate_value} is above maximum {maximum}"
            )
    maximum_regression = rule.get("maximum_regression")
    if maximum_regression is not None:
        if favorable_delta is None:
            violations.append("regression cannot be calculated from a null metric")
        elif favorable_delta < -maximum_regression:
            violations.append(
                f"unfavorable change {-favorable_delta} exceeds {maximum_regression}"
            )
    return {
        "rule_id": rule["rule_id"],
        "scope": rule["scope"],
        "metric": rule["metric"],
        "passed": not violations,
        "comparison": comparison,
        "constraints": {
            name: rule[name]
            for name in (
                "minimum_candidate",
                "maximum_candidate",
                "maximum_regression",
            )
            if rule.get(name) is not None
        },
        "violations": violations,
    }


def compare_reports(
    baseline_payload: Any,
    candidate_payload: Any,
    threshold_payload: Any | None = None,
) -> dict[str, Any]:
    baseline = validate_report(baseline_payload, "baseline report")
    candidate = validate_report(candidate_payload, "candidate report")
    baseline_dataset = baseline["dataset"]
    candidate_dataset = candidate["dataset"]
    if (
        baseline_dataset["id"],
        baseline_dataset["version"],
        baseline_dataset["sample_count"],
    ) != (
        candidate_dataset["id"],
        candidate_dataset["version"],
        candidate_dataset["sample_count"],
    ):
        raise EvaluationValidationError(
            "reports must identify the same dataset version and sample count"
        )
    baseline_hash = baseline["input_fingerprints"]["dataset_canonical_sha256"]
    candidate_hash = candidate["input_fingerprints"]["dataset_canonical_sha256"]
    if baseline_hash != candidate_hash:
        raise EvaluationValidationError(
            "reports must use the same canonical dataset fingerprint"
        )
    if baseline.get("normalization") != candidate.get("normalization"):
        raise EvaluationValidationError(
            "reports must use the same normalization policy"
        )

    threshold_results: list[dict[str, Any]] = []
    threshold_set_id: str | None = None
    if threshold_payload is not None:
        thresholds = validate_thresholds(threshold_payload)
        threshold_set_id = thresholds["threshold_set_id"]
        threshold_results = [
            _evaluate_rule(rule, baseline, candidate) for rule in thresholds["rules"]
        ]

    return {
        "comparison_schema_version": COMPARISON_SCHEMA_VERSION,
        "comparator_version": COMPARATOR_VERSION,
        "dataset": {
            "id": baseline_dataset["id"],
            "version": baseline_dataset["version"],
            "sample_count": baseline_dataset["sample_count"],
            "canonical_sha256": baseline_hash,
        },
        "baseline": {
            "prediction_set_id": baseline["prediction_set_id"],
            "report_canonical_sha256": _canonical_sha256(baseline),
            "provider_versions": sorted(baseline["breakdowns"]["provider_version"]),
        },
        "candidate": {
            "prediction_set_id": candidate["prediction_set_id"],
            "report_canonical_sha256": _canonical_sha256(candidate),
            "provider_versions": sorted(candidate["breakdowns"]["provider_version"]),
        },
        "metrics": _all_metric_deltas(baseline["metrics"], candidate["metrics"]),
        "breakdowns": _compare_breakdowns(baseline, candidate),
        "thresholds": {
            "threshold_set_id": threshold_set_id,
            "evaluated_rule_count": len(threshold_results),
            "passed": all(result["passed"] for result in threshold_results),
            "results": threshold_results,
        }
        if threshold_payload is not None
        else None,
    }
