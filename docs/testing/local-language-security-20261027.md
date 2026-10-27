# Local language and Admin verification — October 27, 2026

Bounded L1 is independently cleared under the [completion ledger](../tasks/local-completion-20261027.md).
These are agent-operated checks with fictional data, not user acceptance or live
Microsoft Entra verification. Baseline: 1,379 files. Evidence:
`outputs/local-completion-20261027/l1/`.

## Real Admin integration

The explicit test-executable mode `--admin-browser-acceptance ROOT OUTPUT` starts
real loopback Kestrel with the existing offline signed-JWT fixture. The browser
mounts the actual Admin components, EnterpriseProvider and authenticated transport
through Vite on 3338. Session, CORS, authorization, version checks, repositories and
PostgreSQL are real. Only sign-in metadata and the navigation adapter are test
substitutions. No production sign-in mode or permissions changed.

The database is exactly `document_intelligence_test` on local port 55623. Runs are
sequential and must never overlap other persistence tests. Each attempt recreates
only that reserved test database and disposes it afterward. A per-run nonce binds
the frontend readiness check to its owned process. The retained database and
3310/8310 workspace are not test targets. The source scanner is deterministic and
the original file is fictional; this is not malware-scanner certification.

`admin-full-2/acceptance.json` records ten passing assertions:

- Real browser Admin session and membership reads.
- Last active Admin cannot demote themselves; no membership changes.
- Explicit Admin grant saves a new membership version.
- A concurrent edit causes a conflict; reload preserves the newer saved state.
- Direct HTTP obtains and downloads the exact original with an actor-bound grant,
  attachment disposition and no-store caching.
- Browser revocation requires confirmation. Direct HTTP then rejects the existing
  Staff token and original-file grant; the Admin session still works.
- Browser storage settings persist with a new version.
- Protection requires explicit release and remains in history.
- A deliberately lost reply after a real successful save locks further writes.
  Refresh recovers the committed value; exactly one accepted write occurs.
- Activity history displays the actual access, protection and storage changes
  using reads only. Direct HTTP also verifies the original-download audit event.

Screenshots were inspected for hierarchy, readable controls and spacing. Runtime
browser exceptions: zero. Existing fictional dark/light layout checks remain
separate evidence; this run does not claim every layout was rechecked.

The first connection attempt failed because direct execution lacked the test
content-root manifest. The explicit private manifest fixed it. The first full
attempt reached the lost-reply test but the fault was installed after transport
captured `fetch`; the application saved normally. Installing an inert wrapper
before initialization and arming only that one write fixed the harness. Earlier
failed receipts are preserved; they are not passing acceptance evidence.

Fresh focused PostgreSQL regressions: **28 passed, 0 failed, 0 skipped** in
`tests/l1-security.trx` (4m 18s). The filter covers source access, database session
revocation, bootstrap permissions, concurrent last-Admin removal and
cross-organization isolation. The test project built with zero warnings/errors.
`cleanup.json` independently confirms no reserved test database and no listener on
3338 after completion. Independent final recheck cleared the bounded Admin and language scope; no material findings remain.
Offline identity does not verify live Entra, and the Vite adapter does not verify
Next server routing; the separate real demo journey covers that local routing.

## Language scope

Three inputs use the actual owned demo worker: a 300-dpi raster of the frozen
English invoice, plus the older two-page mixed-language native and scanned PDFs.
The latter two are representations of one fictional document, not independent
accuracy samples. Hashes, operation keys, provider receipts, comparisons with
frozen truth and exact UTF-8 evidence are retained in `language/`.

No field is automatically confirmed. Missing facts and imperfect OCR remain
reviewable and must not become a complete comparison. Earlier Phase 7 best-model
receipts are historical; this demo uses its packaged fast model and local-1.6.0.

| Actual worker input | Expected header values | Evidence | Outcome |
| --- | --- | --- | --- |
| English invoice raster | 9/9 agree | 15 exact quoted UTF-8 slices and normalized polygons | Partial; unknown document type |
| Mixed native PDF, including OCR on Arabic page | 8/9 agree | 15 exact quoted slices and normalized polygons | Partial; unknown type; Arabic review warning |
| Mixed scanned PDF | 8/9 agree | 15 exact quoted slices and normalized polygons | Partial; unknown type; Arabic review warning |

All three lacked an unambiguous recognized document title and retained the
document-role review warning. Mixed-language payment wording lost a diacritic:
`خلال 30 يوماً` became `خلال 30 يوما`. The mismatch remains recorded. All source
values initially stayed Unreviewed; no comparisons were created by uploading or
reading them. These small fictional samples do not establish production accuracy.

Six actual browser assertions then passed on the retained full-field review
surface (`/?document=...`): explicitly correct that one Arabic value using the
source and a reason; leave the other 14 values unchecked and completion disabled;
preserve original OCR evidence; render RTL Arabic and isolated English digits in
both themes at 1440px/390px without page overflow; reopen the saved correction;
preserve case-list summaries. This does not compare every frozen result or decision
again; those were checked separately in S6. This is a compatibility-surface check, not another
complete three-step invoice journey. Screenshots were visually inspected.

Independent review narrowed one evidence label: the original browser receipt says
earlier comparisons were unchanged, but that assertion compares case-list summaries.
The script and this report now state that exact scope; the original receipt is
retained. The browser ownership guard also now checks its API URL and Development
mode before any future write. No uploads or correction were repeated for these
changes. Four script syntax checks, 229 documentation links and the VitePress
build pass (34.30s; existing bundle-size advisory).

One initial browser navigation timed out during the restored demo's first Next
compilation. After HTTP readiness returned 200, inspection and acceptance passed.
The timeout is retained as runtime startup evidence, not hidden as a passed run.
The owned demo web is running again; its volumes and the retained workspace remain.
