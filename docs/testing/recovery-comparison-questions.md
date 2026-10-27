# R4-T03 — Comparison and question context

September 30, 2026. Shared real UI, navigation/guides and synthetic preview adapter.
No production API, database schema or permission-policy changes.

## Delivered behavior

- Viewers can read saved cases and comparisons, matching backend Read permission.
  Creating cases, pinning reviews, comparing and recording decisions require Review.
- Saved comparisons have a selector and `?case=…&run=…` links. Refresh and Back
  restore the requested run; unavailable runs are explicit. Historical evidence and
  decisions come from that run, while current case sources are labeled separately.
- Questions opened from a case show its name and a return link. Preparing its
  pinned sources is explicit and restricted to reviewers/operators. Failed switches
  preserve the prior corpus without claiming it belongs to the requested case.
- Saved question results reopen with `?result=…` using GET only. Late responses
  cannot replace a newer location. Result question/index versions are independent
  of the current composer. Use saved sources explicitly reuses every saved index,
  including indexes that had no hits; source names are not invented.
- Viewers can reuse their own available saved sources for questions, matching Ask
  permission. Saved results/evidence belong to their originating account, including
  when the caller has administration access. English/Arabic guides explain this.
- Source-list failures offer retry without losing the question. Workspace state
  resets on organization, account or role changes.

## Role verification matrix

| Document role | Read cases/results | Prepare sources / change comparisons | Ask from available saved sources |
| --- | --- | --- | --- |
| Operator | Yes | Yes | Yes |
| Reviewer | Yes | Yes | Yes |
| Viewer | Yes | No | Yes |
| None / administrator-only | No | No | No |

Administration was tested both enabled and disabled for every document role. It
does not expand document permissions. Revoked and signed-out sessions do not mount
the workspace. Real identity configuration remains a separate R5 acceptance task.

## Automated checks

- Full UI suite: 200/200 across 17 files, serial workers and 15-second test timeout.
  Includes 17 new question-context tests and 15 new comparison-context tests.
- API/security and reconciliation contract tests: 118/118, no skipped tests.
- Request adapter and preview fixture tests: 11/11. Preview result/evidence reads
  and request-key replay are actor-scoped; changing the same actor's role preserves
  permitted reads, while other sample accounts receive 404. Root reviewed the
  fixture change after independent agent implementation.
- Focused ESLint and final production Next build/TypeScript pass. An initial build
  caught a synchronous test identity factory; corrected to the declared async API.
- Independent agent implemented comparison changes/tests; root reviewed them.
  A second agent authored question tests and audited backend contracts; comparison
  agent independently reviewed the final question implementation.

## Browser checks and preview state

- Checked Operator, Reviewer, Viewer, administrator-only, administrator+Operator,
  revoked and signed-out accounts. Viewer can read shared comparisons/history but
  has no mutation buttons; administrator-only cannot enter Questions. Revoked and
  signed-out states hide workspace content.
- Case-to-question handoff shows the actual case name and pinned review list. A
  saved question reload restores the answer and opens original evidence. Another
  account receives unavailable-result feedback without seeing its content.
- Created two sample comparisons with different tolerances, selected the older
  one and reloaded its bookmark. Its original values, tolerance and Accepted
  decision remained. Mobile 390px layouts have no horizontal overflow; questions
  were also inspected in light mode. Restored dark mode and the default viewport.
- Preview restarted once to load the updated actor-isolation fixture. This reset
  earlier temporary sample changes. Fresh case `Role checks — مراجعة الأدوار`
  (`c6abfcba-21a8-4413-8556-71a6d188eb61`) contains three pinned documents and two
  comparisons. Port 3410 remains running. Original user tab untouched; temporary
  verification tab closed. No Docker, real membership changes or real data used.
- Screenshot: `outputs/ui-preview/comparison-role-history.png`.

## Boundaries

The real API does not expose historical run document-version metadata through the
case DTO. The UI shows each run's actual stored field evidence and separately labels
current case pins; it does not infer historical review IDs from current links.
Unsaved question/source drafts are not persisted across reloads. Saved result URLs
are account-scoped bookmarks, not cross-account sharing links. Synthetic UI checks
do not prove Entra authentication or database persistence. History-download manual
testing remains deferred to Rand, and unrelated Arabic/Phase 7 changes are retained.
