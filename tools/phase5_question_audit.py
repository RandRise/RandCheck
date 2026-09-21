#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys


REPO_ROOT = Path(__file__).resolve().parents[1]
API_ROOT = REPO_ROOT / "apps" / "api"
if str(API_ROOT) not in sys.path:
    sys.path.insert(0, str(API_ROOT))

from app.cited_question_evaluation import (  # noqa: E402
    CitedQuestionValidationError,
    audit_question_set,
)


DEFAULT_DATASET = (
    REPO_ROOT
    / "samples"
    / "phase-5"
    / "cited-questions-v0.1.0"
    / "question-set.json"
)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Audit Phase 5 question coverage, sources, pages, and evidence quotes."
    )
    parser.add_argument("--dataset", type=Path, default=DEFAULT_DATASET)
    parser.add_argument("--repo-root", type=Path, default=REPO_ROOT)
    parser.add_argument("--output", type=Path)
    return parser


def main() -> int:
    arguments = build_parser().parse_args()
    try:
        payload = json.loads(arguments.dataset.read_text(encoding="utf-8"))
        report = audit_question_set(payload, arguments.repo_root)
    except (OSError, json.JSONDecodeError, CitedQuestionValidationError) as error:
        print(f"Phase 5 question audit rejected: {error}", file=sys.stderr)
        return 2

    rendered = json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if arguments.output is None:
        sys.stdout.write(rendered)
    else:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(rendered, encoding="utf-8", newline="\n")
        print(f"Wrote Phase 5 question audit to {arguments.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

