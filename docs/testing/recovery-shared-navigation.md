# R3-T02 — Shared navigation verification

September 30, 2026. Complete before beginning R3-T03 role guidance.

## Delivered

The real application uses a persistent shared frame with Documents, Compare
documents and Questions according to document role, plus Members & access, Audit
history and Retention & holds for administrators. The brand link sends an
administrator without document access to membership administration. Organization,
user and role stay visible, including on small screens. Each destination has one
active-page indicator. Keyboard users can skip to the content.

The appearance selector and sign-out share the account bar. Signed-out, loading
and denied screens retain appearance controls without workspace navigation. The
callback remains independent. Simple page names replace decorative headlines;
phase labels, numbered banners and duplicate administration tabs were removed.
Source-related labels, the Arabic limitation, and contextual case/audit links remain.

## Gate results

- Full UI suite: 111/111 across 11 files, 67.57 seconds, one worker.
- Two later composition tests added: focused navigation suite 22/22, 4.53 seconds.
  Appearance changes preserve the exact input node and draft; one selector appears
  in both ready and signed-out compositions.
- Seven request/preview-adapter checks pass. TypeScript, focused ESLint, production
  Next build (all 10 static pages), and diff whitespace check pass.
- Independent reviewer found no blocking lifecycle, authorization or accessibility
  regression. Removed an unused import identified during review.
- Browser: actual-component preview sidebar and case form inspected on desktop;
  case source controls/tolerances and all six combined-role links checked at 390px.
  Admin navigation reached Members & access with the correct active indicator;
  document width 375 at viewport width 390 (remaining width is the scrollbar).
  A sample case named Navigation layout check was created; no real data was changed.

## Integration impact

| Area | Evidence |
| --- | --- |
| Roles and direct routes | Tests cover Viewer, Reviewer, Operator, admin-only and combined roles, including denied deep links |
| Identity lifecycle | Loading, callback, sign-out, revoked access and membership downgrade tests pass; transport and abort behavior unchanged |
| State | Membership changes retain the original revision reset; theme changes do not remount children |
| Routing | Original paths and case-specific question query preserved; Next Link used; exact aria-current tested on six routes |
| Appearance | Production and preview disable the outer selector and use frame controls; signed-out controls remain available |
| API/data/audit | Endpoints, payloads, ETags, retry rules, database and audit writes unchanged |
| Arabic/evidence | Content/provenance/bidi behavior unchanged; previous regression suites still pass |

The fast preview uses full-page navigation through its Link adapter. Its browser
walkthrough does not prove Next client-side routing persistence or real Entra
authentication. API/security checks here use existing mocks and contract tests;
they do not close the real integration gates. Broader case recovery, viewer source
selection and full workflow simplification remain R4 work. History-download manual
verification remains deferred to Rand.
