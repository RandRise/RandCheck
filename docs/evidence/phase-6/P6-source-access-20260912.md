# Original-file access verification — September 12, 2026

Rand authorized continuing Phase 6. This increment implements
[authorized original-file access](../../design/phase-6-source-access.md): five-minute
grants, current membership checks, source integrity/scanning, fail-closed audit
and an enterprise document-workspace download button.

## Verification

| Check | Result |
| --- | --- |
| API regression | 165 passed, zero skips, including grant protection/expiry and source permission cases |
| Infrastructure regression | 161 passed, zero skips, including immutable source snapshots and organization/document path substitution |
| PostgreSQL integration | 21 passed, zero skips: 18 source-access cases plus three production transport/storage cases |
| Browser regression | 52 passed, zero skips: 49 Vitest cases and three existing review-request checks |
| Preserved data | All 34 retained table count/content fingerprints match this increment's before/after snapshots and the previous checkpoint |
| Spending / schema | 464 spend events, 153 reservations and 15 original migrations unchanged; no new migration or paid call |

399 unique checks pass. Repeated runs are not added. The full PostgreSQL,
Application, Domain and Python suites were not rerun; their prior evidence remains
separate. Existing worker and storage regressions pass after sharing the source-path
guard. Production Next.js and VitePress build logs are retained with verification.

## Security scenarios

Real signed-token HTTP requests and disposable PostgreSQL establish:

- Viewer, Reviewer and Operator downloads return the exact verified bytes as an
  attachment, with no-store/nosniff/no-referrer headers and a generated safe filename.
  Range and conditional requests cannot bypass full admission or audit.
- Grants cannot be moved between documents, organizations or users. A peer with
  legitimate document access must obtain their own grant. Missing, duplicate and
  query-string credentials fail without scanning.
- Expiry, role removal, revocation and revocation/reactivation reject outstanding
  grants. Expiry or revocation during scanning also prevents release.
- Missing/tampered/incorrect-size files, another organization's storage path,
  detection and scanner outage release no original bytes. Failure preserves any
  existing source file. Safe denied events are recorded.
- Forced audit INSERT failures prevent both credential issuance and file release.
  Successful authorization audit binds the current actor, document, hash and size;
  events exclude original contents, filenames, paths and temporary credentials.
- A production host refuses HTTP or Locked storage. A test HTTPS request to the
  production profile succeeds with Enforced storage. This is host-level testing,
  not verification of a deployed TLS certificate or reverse proxy.

Separate tests verify cryptographic integrity, purpose/key isolation, issue/expiry
boundaries and bounded token input. The reader scans and returns one memory snapshot,
so later filesystem changes cannot replace the response bytes. Browser tests verify
credential placement, rejection of foreign grant URLs, safe failures, cancellation,
attachment initiation and temporary blob URL cleanup.

## Evidence and snapshots

Artifacts are in `outputs/phase-6/source-access-20260912`: API/Infrastructure and
two PostgreSQL TRX reports, browser logs, build logs, retained fingerprints,
`verification.json`, `SNAPSHOTS.md` and three PNGs. The first sandboxed Node run
failed with the known path-permission error; the approved external run passed.
A final build found a missing `this` annotation in the new browser test; its log
is retained separately and the annotation was corrected before the passing build.

The screenshots use an isolated, clearly labeled fictional fixture on local port
3018, importing the real UI components. They show download initiation, scanner
outage and safe source-access audit details. No live identity or malware engine is
connected. The in-app browser showed the download-started state, but its automation
did not expose a completion event for the blob download. These snapshots do not
claim native file-save completion; exact HTTP file bytes and client attachment
initiation are verified separately by the tests above.

PostgreSQL recreates only `document_intelligence_test`, with one runner; the fixture
database is removed afterward. All source fixtures use new temporary directories.
The retained Phase 5 database is read only. .NET runs use `-p:NuGetAudit=false` for
offline metadata lookup, not as a dependency advisory assessment.

## Still open

Live Entra/TLS, real ClamAV and definition freshness, protected shared Data Protection
keys, filesystem permissions/encryption, native/deployed browser download validation,
quarantine cleanup, retention/deletion/holds, rate limits, complete API runtime grants
and the remaining tenant mutation matrix remain Phase 6 work. Download audit records
authorization before release, not guaranteed delivery. Phase 6 is not complete.
