#!/usr/bin/env python3
import argparse
import json
from pathlib import Path
import sys


REPO_ROOT = Path(__file__).resolve().parents[1]
API_ROOT = REPO_ROOT / "apps" / "api"
if str(API_ROOT) not in sys.path:
    sys.path.insert(0, str(API_ROOT))

from app.evaluation import (  # noqa: E402
    EvaluationValidationError,
    validate_dataset,
    verify_dataset_sources,
)
from app.extraction import get_extractor  # noqa: E402
from app.local_extraction import ADAPTER_VERSION  # noqa: E402
from app.prediction_generation import (  # noqa: E402
    PredictionGenerationError,
    generate_prediction_set,
)


def _read_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise EvaluationValidationError(f"could not read {path}: {error}") from error


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Run a Phase 2 extraction provider over a Phase 3 dataset and freeze "
            "a versioned prediction set."
        )
    )
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--prediction-set-id", required=True)
    parser.add_argument("--provider", choices=("local",), default="local")
    parser.add_argument("--output", type=Path, required=True)
    return parser


def main() -> int:
    arguments = build_parser().parse_args()
    try:
        dataset_payload = _read_json(arguments.dataset)
        dataset = validate_dataset(dataset_payload)
        verify_dataset_sources(dataset, arguments.dataset.parent, REPO_ROOT)
        prediction_set = generate_prediction_set(
            dataset_payload,
            arguments.dataset.parent,
            get_extractor(arguments.provider),
            prediction_set_id=arguments.prediction_set_id,
            provider_alias=arguments.provider,
            adapter_version=ADAPTER_VERSION,
        )
    except (EvaluationValidationError, PredictionGenerationError, ValueError) as error:
        print(f"Prediction generation rejected: {error}", file=sys.stderr)
        return 2

    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(
        json.dumps(prediction_set, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Wrote frozen predictions to {arguments.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
