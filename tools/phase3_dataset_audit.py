#!/usr/bin/env python3
import argparse
import json
from pathlib import Path
import sys


REPO_ROOT = Path(__file__).resolve().parents[1]
API_ROOT = REPO_ROOT / "apps" / "api"
if str(API_ROOT) not in sys.path:
    sys.path.insert(0, str(API_ROOT))

from app.dataset_readiness import audit_dataset_readiness  # noqa: E402
from app.evaluation import EvaluationValidationError  # noqa: E402


def _read_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise EvaluationValidationError(f"could not read {path}: {error}") from error


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Audit Phase 3 dataset rights, privacy, and annotation readiness."
    )
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--source-register", type=Path, required=True)
    parser.add_argument("--annotation-ledger", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    return parser


def main() -> int:
    arguments = build_parser().parse_args()
    try:
        audit = audit_dataset_readiness(
            _read_json(arguments.dataset),
            _read_json(arguments.source_register),
            _read_json(arguments.annotation_ledger),
        )
    except EvaluationValidationError as error:
        print(f"Dataset audit input rejected: {error}", file=sys.stderr)
        return 2

    rendered = json.dumps(audit, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if arguments.output is None:
        sys.stdout.write(rendered)
    else:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(rendered, encoding="utf-8", newline="\n")
        print(f"Wrote dataset readiness audit to {arguments.output}")
    return 4 if not audit["claim_ready"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
