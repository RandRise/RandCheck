# R3-T03 — Role guidance verification

September 30, 2026. Completes the shared-interface phase after R3-T02.

## Bilingual extension — September 30, 2026

Rand requested more specific role explanations in English and Arabic, with a visual
inside each guide. The guide now offers English/العربية controls, responsibilities,
detailed page tasks, access limits and a numbered workflow diagram. All nine guide
variants are translated, including Viewer saved answers and the three administration
pages. Arabic retains English control names in parentheses to match the current UI.
This localizes the guide only; the wider application is not fully translated.

The diagram uses HTML/CSS and readable ordered steps, with RTL horizontal flow for
Arabic and downward flow at narrow widths. No image download, font dependency or
diagram runtime is needed. Language stays selected while the same guide is open,
across client route updates and close/reopen; identity/access resets still clear it.
There is no persistent language preference across a full page reload.

Verification: 20 focused guide tests (seven added by an independent test author),
133/133 full UI tests across 12 files in 49.97 seconds, focused ESLint and production
build all pass. Build includes TypeScript and all 10 static pages. Independent review
cleared both languages, task accuracy, diagram ordering and focus/state behavior.
Corrected the old hold-release instruction: Place hold needs a reason; Release hold
uses Confirm release without a new reason. Review guidance distinguishes confirming
extracted output from saving typed corrections; case decisions do not edit sources.

Browser checks: Arabic desktop and 390px layouts, mixed English control labels,
downward mobile diagram, English/light layout and Arabic/dark layout passed. The case
name draft remained intact while opening help and switching language/theme. At 390px,
document width was 375px with no horizontal overflow. The viewport was reset and the
original Dark preference restored. Screenshots: `outputs/ui-preview/role-guide-arabic-desktop.png`
and `outputs/ui-preview/role-guide-arabic-mobile.png`.

Integration impact: provider permissions, routing, APIs, ETags, audit writes, document
content and persistence remain unchanged. Full existing UI regressions pass; the
earlier seven request/adapter checks were not repeated for this presentation-only
extension. Browser evidence still uses sample data, not real authentication.

## Delivered

Help with this page opens an inline guide in the actual workspace. Instructions
follow the current page and document role, with separate administration guidance
for members, audit history and retention. Viewer instructions describe the current
saved-result path; they do not promise source preparation that the role cannot use.

Opening and closing help preserves the working form. Close and Escape return focus
to the help button. Navigation updates the instructions; identity, organization or
permission changes dismiss stale help. Denied, signed-out and callback screens do
not expose the workspace guide. Both the application and fast preview use it.

## Gate results

- Independent guide tests: 13/13. Full UI suite: 126/126 across 12 files, one worker,
  45.57 seconds. Includes the 22 shared-navigation tests.
- Seven request/preview-adapter checks pass, including ETag rotation and stale-write
  handling, role boundaries and sample comparison/question evidence.
- Focused ESLint passes. Production Next build passes TypeScript and all 10 static
  pages (5.2-second compilation, 10.3-second TypeScript check).
- Independent reviewer cleared the final code after field-name copy and DOM-order
  corrections. No remaining blocker.
- Browser: combined administrator guidance verified on desktop; Escape closes help
  and restores focus. At 390px the guide is readable and document width is 375px,
  with no horizontal overflow. The temporary viewport and original Light preference
  were restored after inspection. Dark screenshot saved under
  `outputs/ui-preview/shared-navigation-guide.png`.

## Integration impact

| Area | Result |
| --- | --- |
| Forms and state | Tests preserve the same input node and draft across open/close/Escape |
| Identity and permissions | Actual provider composition tests cover role, actor, organization and administration changes, denied routes, sign-out and callback |
| Routes | Open guidance updates with the page; current route and case/question links are unchanged |
| API, persistence and audit | No endpoint, payload, ETag, database, mutation or audit-writing change |
| Evidence and Arabic | No changes to document content, evidence, provenance or bidi handling; existing UI regressions pass |
| Preview | Uses the real shared components; sample adapters remain explicit simulations |

The browser checks use sample data. They do not verify real Microsoft sign-in,
backend authorization or durable audit delivery. Real security integration remains
an R5 gate. Case recovery and more continuous workflows remain R4 work. Manual
history-download verification remains deferred to Rand.
