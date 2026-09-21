# Phase 4 quality plan

**Status:** Approved foundation on 2026-08-19
**Execution status:** Technical release gate passed; completion approved by Rand on 2026-09-07

## Test matrix

| Priority | Layer | Required checks |
|---|---|---|
| P0 | Domain | Organization scope, one document per role, immutable runs/findings, valid transitions, append-only resolutions, non-negative tolerances |
| P0 | Snapshot | Completed review required, exact review/run pinning, effective-value projection, occurrence alignment, source/evidence preservation |
| P0 | Rules | Exact matches, missing roles, duplicates, supplier/currency mismatches, amount boundaries, line matching, deterministic ordering |
| P0 | Tolerance | Below/equal/above boundary, per-run frozen policy, invalid decimal rejection, no currency conversion |
| P0 | Persistence | PostgreSQL constraints, tenant-safe composite keys, migration upgrade/downgrade, atomic evaluation, no pending model changes |
| P0 | API | Organization isolation, stable validation/conflict errors, stale-version protection, idempotent reads, bounded inputs |
| P0 | Evidence | Both available sides identify document/review/run/field/page; missing evidence becomes an explicit data-quality finding |
| P0 | Claims | Dataset/rule versions and synthetic exclusion appear in every report |
| P1 | UI | Case linking, rule explanation, compared values, tolerance, page evidence, and resolution history remain visible |
| P1 | Evaluation | Finding precision/recall, false positives/negatives, per-rule breakdowns, deterministic report |

## Release gate

- [x] Reconciliation and tolerance contracts are versioned.
- [x] Synthetic development results are excluded from real-world claims.
- [x] Domain invariants and PostgreSQL constraints pass.
- [x] Completed review revisions produce immutable evidence-backed snapshots.
- [x] Exact matches produce no false discrepancy.
- [x] Amount and quantity mismatches identify both values and sources.
- [x] Tolerance boundary cases pass.
- [x] Every finding explains its rule and applied tolerance.
- [x] Reviewer resolution is append-only and attributable.
- [x] The quotation/PO/invoice scenario passes end to end.
- [x] Full Python, .NET, web, and documentation regressions pass.
- [x] Rand approves Phase 4 completion (2026-09-07).
