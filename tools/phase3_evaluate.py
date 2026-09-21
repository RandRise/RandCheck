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
    evaluate,
    validate_dataset,
    verify_dataset_sources,
)


def _read_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise EvaluationValidationError(f"could not read {path}: {error}") from error


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Evaluate a versioned field-extraction prediction set offline."
    )
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--predictions", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    return parser


def main() -> int:
    arguments = build_parser().parse_args()
    try:
        dataset_payload = _read_json(arguments.dataset)
        dataset = validate_dataset(dataset_payload)
        verify_dataset_sources(dataset, arguments.dataset.parent, REPO_ROOT)
        report = evaluate(
            dataset_payload,
            _read_json(arguments.predictions),
        )
    except EvaluationValidationError as error:
        print(f"Evaluation input rejected: {error}", file=sys.stderr)
        return 2

    rendered = json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if arguments.output is None:
        sys.stdout.write(rendered)
    else:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(rendered, encoding="utf-8", newline="\n")
        print(f"Wrote evaluation report to {arguments.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
