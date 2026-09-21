# Phase 5 server-log policy proposal

**Status:** Proposed, 2026-09-08. No server settings, permissions, retention jobs,
or existing logs were changed. Approval of this document is not evidence that
the deployment controls work.

## Decision in plain language

Treat raw PostgreSQL logs as sensitive data, restrict who can read them, and
export only deliberately constructed content-free operational events. Keep the
current development workflow synthetic while deployment controls remain open.
This permits continued Phase 5 model work; it does not authorize real invoices.

The [database-log verification](../evidence/phase-5/P5-T08-database-log-verification.md)
found synthetic input in PostgreSQL primary error messages and duplicate-key
details. Application-side filtering passed, but cannot sanitize PostgreSQL's
own file. Disabling parameter logging or reducing detail is not a guarantee
that error messages contain no private values. Silencing all errors is not the
proposed remedy.

## Proposed controls

These are engineering defaults for review, not statutory invoice-retention
requirements or a statement of UAE compliance.

| Area | Proposed rule | Evidence required before real data |
| --- | --- | --- |
| Ownership | Rand approves prototype policy; a named deployment operator owns access, retention and incidents before deployment | Recorded owner and review date |
| Raw log access | Only the database service, required log collector and explicitly authorized operators; application users and the application identity cannot read raw logs | Effective permissions tested using each identity, including inherited permissions |
| Storage | Protected host/volume with encryption at rest; no web-accessible directory, repository tracking or uncontrolled shared-folder sync | Storage/key access and sharing configuration checked |
| Collection/export | Raw logs stay in the protected store; centralized operational output uses an allowlist of event type, severity, time and opaque operational ID | Structured fields and formatted messages checked; no SQL, parameters, error text, prompts, invoice text or private identifiers |
| Transport | Approved collectors use authenticated encrypted transport; no unaudited support uploads | Collector destination, credentials and access reviewed |
| Rotation | Rotate daily or at 100 MiB, whichever comes first; alert on disk pressure or collector failure | Disposable-environment rollover and failure tests |
| Retention | Raw operational logs: maximum 7 days; content-free operational events: maximum 30 days, unless an approved exception applies | Scheduled expiry and deletion evidence |
| Copies | Same classification and original expiry apply to rotated files and exports; exclude raw logs from routine backups unless expiry can be enforced | Inventory covering backups, snapshots, exports and downstream stores |
| Exceptions | Incident/legal holds require a named approver, reason, restricted scope, expiry/review date and release action | Exception register; no indefinite default hold |
| Emergency access | Time-limited operator access with a recorded reason and subsequent review | Access grant/revoke and audit trail tested |

The allowlisted export is a separate event representation, not an assertion
that regex replacements can make arbitrary raw logs safe. The collector's own
errors and dead-letter storage must meet the same policy. Audit metadata must
not copy the sensitive content it describes.

Exact platform commands depend on the deployment target. PostgreSQL settings
alone do not implement Windows permissions, storage encryption, backups or
export controls. The current local `pg_ctl -l` file is not evidence of managed
rotation. Keep the [PostgreSQL 16 logging reference](https://www.postgresql.org/docs/16/runtime-config-logging.html)
with the eventual platform-specific configuration review.

## Verification checklist — not yet performed

- [ ] Choose the deployment target and operator; inventory every original and copy.
- [ ] Demonstrate that an ordinary user and app identity cannot read raw logs,
      while the intended service and authorized operator retain required access.
- [ ] Generate unique synthetic conversion-error and duplicate-key markers.
      Confirm any marker-bearing original is protected and no marker reaches
      operational exports, collector diagnostics or ordinary user responses.
- [ ] Test rotation, disk-pressure alerting and collector failures without
      silently losing security-relevant events.
- [ ] Test expiration on disposable logs using a shortened test interval;
      verify rotated files, exports and backup treatment, not just the active file.
- [ ] Demonstrate exception expiry and emergency access revocation.
- [ ] Record configuration/version, test identities, results and owner approval.

Preserve `tmp/phase5-postgres-20260907/server.log` as existing synthetic review
evidence for now. Applying the future retention policy requires an explicit
decision about that evidence; drafting this proposal does not authorize deletion.

## Remaining boundary

The server-log gate stays open until the selected controls are implemented and
verified. Production patching, authentication, deployment hardening and broader
Phase 6/8 operations remain separate. Phase 5 prompt-injection, grounding and
acceptance work also remain open; this proposal does not close P5-T08.
