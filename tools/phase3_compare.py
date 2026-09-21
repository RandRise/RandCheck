#!/usr/bin/env python3
import argparse
import json
from pathlib import Path
import sys


REPO_ROOT = Path(__file__).resolve().parents[1]
API_ROOT = REPO_ROOT / "apps" / "api"
if str(API_ROOT) not in sys.path:
    sys.path.insert(0, str(API_ROOT))

from app.evaluation import EvaluationValidationError  # noqa: E402
from app.evaluation_comparison import compare_reports  # noqa: E402


def _read_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise EvaluationValidationError(f"could not read {path}: {error}") from error


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Compare two reports for the same frozen evaluation dataset."
    )
    parser.add_argument("--baseline", type=Path, required=True)
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--thresholds", type=Path)
    parser.add_argument("--output", type=Path)
    return parser


def main() -> int:
    arguments = build_parser().parse_args()
    try:
        comparison = compare_reports(
            _read_json(arguments.baseline),
            _read_json(arguments.candidate),
            _read_json(arguments.thresholds) if arguments.thresholds else None,
        )
    except EvaluationValidationError as error:
        print(f"Comparison input rejected: {error}", file=sys.stderr)
        return 2

    rendered = json.dumps(comparison, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if arguments.output is None:
        sys.stdout.write(rendered)
    else:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(rendered, encoding="utf-8", newline="\n")
        print(f"Wrote evaluation comparison to {arguments.output}")
    thresholds = comparison["thresholds"]
    return 3 if thresholds is not None and not thresholds["passed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
