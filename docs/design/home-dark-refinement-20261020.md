# Home: dark workspace refinement

## Accepted and implemented in Home

Rand accepted the Mosaic-inspired preview and then approved applying it to Home.
The Home implementation now retains actual saved data, account controls,
permissions, safe retries and light-mode support. Colour overrides apply only
while Home is mounted, including its enclosing frame. Other screens keep their
current appearance.

[Implementation findings, evidence and manual checks](../testing/home-design-20261020.md).
Acceptance of the preview is recorded; manual acceptance of the working page is
still pending. The following sections retain the earlier proposal history.

## Proposal history

October 20, 2026. Rand chose **Keep the dark workspace and refine it**.
The dark direction is approved. Rand liked the first layout and requested dark
grey with some colour. Rand approved the grey, then requested a better button
colour than teal, then rejected amber. Rand requested grey shades and the
frontend-design skill. Rand then found the grey version lifeless, reviewed
external references, and approved preparing a Mosaic-inspired Home preview.
The current proposal uses dark grey surfaces, purple accents and clearer cards.
Approval covers preparing this preview; visual acceptance and application
implementation remain pending.

## Current: Mosaic-inspired preview

The reference is [Cruip's Mosaic](https://cruip.com/mosaic/). This is an original
static adaptation of its visual direction, with no copied template source,
installed dependencies or external assets.

- Grey canvas `#1c1d21`, surfaces `#26272d`, raised cards `#313239`.
- Purple start button `#7060df`, white text and a lavender outline.
- A purple-tinted Start here panel with an illustrated invoice/PO pair.
- Three ordered step tiles; their colours identify steps, not completion.
- Saved reviews remain neutral, with one action per row and no match badges.
- Segoe UI first, clear headings, and organization context retained on mobile.

The workflow, sample rows and destinations stay the same. The preview contains
no scripts, API calls or working application controls. Light mode and the
working app are outside this batch. See the static preview linked below.

Fresh checks and before/after evidence are in `outputs/home-mosaic-20261020`.
Rendering passed at 1440, 768, 420 and 360px without horizontal overflow,
runtime errors or external resources. Self-review caught a low-contrast button
outline against its fill; the outline was lightened before final verification.
Static checks do not establish functional or keyboard accessibility, backend
integration, or user acceptance. Final text contrast is at least 4.74:1 and
tested control outlines at least 3.14:1. All 223 checked local document targets
exist. Independent review found no material issue blocking presentation and
confirmed only the five intended files changed against the captured baseline.
Interactive focus, light mode, full account controls, real application states
and long arbitrary names remain implementation acceptance work.

## First batch

Refine Home first. The first proposal kept the existing navy/blue palette; Rand
then requested the charcoal/teal revision. Keep the system
typeface, sidebar destinations and 3-phase invoice workflow. This is an Operate
screen: the user should quickly start a check or continue saved work.

- One heading and one short explanation replace the repeated introductory copy.
- Start here groups the existing primary action with a compact 3-step guide.
- Continue your work uses aligned rows: name/reference, saved status, one action.
- Give the primary action stronger contrast; keep Refresh checks secondary.
- Keep comfortable reading sizes, Arabic text, English digits and narrow layouts.

[Open the static proposal](/proposals/home-dark.html). It uses explicitly labelled
example data, makes no API requests and contains no functioning app controls.
It illustrates Home's hierarchy; it does not propose removing account controls,
sign-out, help, permission-dependent navigation or the existing theme choice.
Production loading, error, empty, retry and pagination states must remain.

No new stats, supplier names, match badges or inferred completion states are
proposed. Existing case data does not support richer progress labels without
additional design and implementation work.

## Colour revision

**Earlier graphite proposal, rejected as lifeless:** the canvas `#181818` and surface `#222222` stayed.
The primary button is light grey `#d7d7d7` with dark text `#191919`; secondary
controls use `#303030` with soft white text. Quiet dividers `#414141` separate
content; stronger `#808080` outlines identify secondary controls. The logo,
selected navigation and proposal notice also use neutral grey.

The frontend-design skill informed this revision: assign contrast by purpose
instead of colouring every action and navigation element equally. Keep the
accepted layout, existing system type, left alignment and compact step guide.
Only the main start action gets a light fill. No new behaviour or navigation
is proposed. The brief and captured before files are in
`outputs/home-graphite-20261020`; amber and earlier proposals remain preserved.

Fresh graphite checks: desktop at 1440px and phone at 420px render without
overflow or runtime errors. Content outside CSS is unchanged. Text contrast
is at least 6.58:1, primary-button text 12.22:1 and secondary-control outlines
3.34:1 against their fill. The changed documents pass 220 local-link checks.
Independent review found no material visual issue blocking presentation.
This verified the static proposal, not keyboard behaviour, permissions, real
states or full application accessibility. Rand subsequently rejected this palette.

**Earlier amber proposal, rejected:** Rand approved the charcoal grey and asked to replace teal buttons.
The proposal now uses muted amber `#e7b66e`, dark button text `#291d0b` and a
matching selection tint `#3c3224`. All grey surfaces and layout remain unchanged.
This accent is a proposal, not an accepted application change. The preceding
teal version is preserved under `outputs/home-amber-20261020/before`.

**Earlier teal proposal and verification:**
Charcoal background `#181818`, grey surfaces `#222222`, soft white text `#f2f2f2`,
secondary text `#b7b7b7`, and teal accents `#5ee0c2`. Teal marks actions and the
selected menu item; saved-work statuses stay neutral. Only colour tokens changed
in the static HTML. The amber proposal notice remains separate from product UI.
Before files and fresh colour checks are in `outputs/home-colour-20261020`.
Fresh desktop/phone renders have no overflow or runtime errors. Measured text
colour pairs exceed 4.5:1; control outlines exceed 3:1 against both dark surfaces.
Self-review confirmed that only the HTML colour tokens changed. This small
palette iteration does not repeat the earlier independent layout review or
establish full application accessibility.

## Earlier navy proposal checks

The static design renders at 1440px and 420px without horizontal overflow or
runtime errors. It contains no scripts or external resource requests. The four
affected Markdown files pass 215 local-link checks. Captures and scoped source
diffs are in `outputs/home-design-20261020`; the source diff was opened in the
existing VS Code window and the proposal was opened in the browser.

Independent assessment found no material issue blocking presentation. When
implementing, keep organization context visible on mobile and check long Arabic
and mixed-language names, loading, error, empty and pagination states. Static
illustrative controls do not verify keyboard use, permissions or functional
correctness. Layout review was pending at that checkpoint; Rand subsequently
liked the layout. The current Mosaic-inspired treatment still awaits approval.

## Review sequence

1. Review Home's layout and visual weight.
2. Implement the accepted Home refinement as one small batch, then run focused
   checks, independent review, show code diffs and request a short manual check.
3. Apply the accepted spacing and control style to the invoice workflow, then
   its results and the Document library in separate batches.

The real-backend result gap is still open. Better styling must not make simulated
or incomplete results look verified. No application files, records, permissions
or comparison rules change in this proposal batch.
