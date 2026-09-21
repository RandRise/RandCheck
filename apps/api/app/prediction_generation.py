from __future__ import annotations

from dataclasses import asdict
from pathlib import Path
from typing import Any

from .evaluation import validate_dataset
from .extraction import ExtractionError, ExtractionProvider, ProviderResult


PREDICTION_SCHEMA_VERSION = "1.0"


class PredictionGenerationError(ValueError):
    """Raised when a dataset cannot be converted into frozen predictions."""


def _content_type(path: Path) -> str:
    suffix = path.suffix.lower()
    if suffix == ".pdf":
        return "application/pdf"
    if suffix == ".png":
        return "image/png"
    if suffix in {".jpg", ".jpeg"}:
        return "image/jpeg"
    raise PredictionGenerationError(
        f"dataset source {path.name!r} has an unsupported evaluation media type"
    )


def _field_payload(field: Any) -> dict[str, Any]:
    return {
        "key": field.key,
        "occurrence": field.occurrence,
        "value_type": field.value_type,
        "value": field.value,
        "normalized_value": field.normalized_value,
    }


def _provider_payload(provider: ProviderResult) -> dict[str, str]:
    return asdict(provider)


def _failure_provider_payload(provider_alias: str, adapter_version: str) -> dict[str, str]:
    # A provider can fail before it selects a native/OCR model. Recording that
    # explicitly is more truthful than inventing a model identity.
    return {
        "alias": provider_alias,
        "adapter_version": adapter_version,
        "model": "not-selected",
        "model_version": "not-selected",
    }


def generate_prediction_set(
    dataset_payload: Any,
    manifest_directory: Path,
    provider: ExtractionProvider,
    *,
    prediction_set_id: str,
    provider_alias: str,
    adapter_version: str,
) -> dict[str, Any]:
    """Run a Phase 2 provider over a Phase 3 dataset and freeze its outputs."""

    dataset = validate_dataset(dataset_payload)
    if not prediction_set_id.strip():
        raise PredictionGenerationError("prediction_set_id must be non-empty")

    runs: list[dict[str, Any]] = []
    for sample in dataset["samples"]:
        source_path = (manifest_directory / sample["source"]["path"]).resolve()
        try:
            result = provider.extract(source_path, _content_type(source_path))
        except ExtractionError as error:
            runs.append(
                {
                    "sample_id": sample["sample_id"],
                    "outcome": f"Failed:{error.code}",
                    "provider": _failure_provider_payload(
                        provider_alias, adapter_version
                    ),
                    "metrics": {
                        "provider_duration_ms": None,
                        "estimated_cost": None,
                        "cost_currency": None,
                    },
                    "fields": [],
                }
            )
            continue

        runs.append(
            {
                "sample_id": sample["sample_id"],
                "outcome": result.outcome,
                "provider": _provider_payload(result.provider),
                "metrics": {
                    "provider_duration_ms": result.metrics.provider_duration_ms,
                    "estimated_cost": result.metrics.estimated_cost,
                    "cost_currency": result.metrics.cost_currency,
                },
                "fields": [_field_payload(field) for field in result.fields],
            }
        )

    return {
        "prediction_schema_version": PREDICTION_SCHEMA_VERSION,
        "prediction_set_id": prediction_set_id,
        "dataset_id": dataset["dataset_id"],
        "dataset_version": dataset["dataset_version"],
        "runs": runs,
    }
