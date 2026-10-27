# Less typing when checking an invoice

> **October 20 update:** Rand approved the numbered navigation and simple Home.
> G02/G03 source slices are implemented; manual and live integration checks remain
> open. [Current plan](one-review-navigation-20261020.md) and
> [verification](../testing/navigation-flow-20261020.md) supersede earlier
> unimplemented navigation wording below. Visual redesign and export stay separate.


October 12, 2026. Rand approved the direction explained in the conversation: one
invoice/PO review flow, suggested values rather than a long form, simple actions,
and backend checks that do not require choosing scope or review versions. This
does not approve the earlier dashboard or Owner/Staff proposals. Manual acceptance
of earlier batches is still pending.

## The intended experience

Add an invoice and its purchase order. The system reads them. A person checks the
suggested values against their sources, changing only what needs correction. The
backend compares usable facts and explains differences and missing checks. It
never guesses that an invoice is final, or that unchecked values are correct.

## Manageable batches

| Batch | Outcome | Acceptance boundary |
| --- | --- | --- |
| G01: easier value checking | One value and its source at a time; Keep this value, Change it, I can't tell. | Exact explicit decisions, warnings first, no advance before saved response, preserved drafts/history/access, clear missing-value status. First implementation checkpoint. |
| G02: backend check contract and policy | Show factual quantity and price results without asking the person to declare full/partial scope. | Versioned rules and compatible persistence; retain old runs/pending requests. Test partial/equal/excess quantities, complete coverage, duplicates, incompatible units, bases, taxes and missing evidence. Not implemented in G01. |
| G03: one saved pair workspace | Add/review both documents together; Show results binds the exact reviews inspected. | No separate version-selection ceremony; no adoption of newer reviews, auto-confirmation or uncertain-write replay. Test partial saves, refresh, stale versions, permissions and real API/database behavior. Not implemented in G01. |

Each meaningful batch includes captured before/after diffs in VS Code, focused
tests, independent Progress and Quality Reviewer assessment, fixes/recheck and a
short manual checkpoint. These are steps toward the approved outcome, not a claim
that the entire flow is already simplified.

## G01 behavior

Opening a document from a saved invoice pair selects guided mode. Start review
remains deliberate. The general Documents page also offers **Check one value at
a time**; its existing full-field view and older quotation reviews remain.

Warnings appear first. A high confidence value still needs a decision. **Keep this
value** saves an explicit confirmation. **Change it** opens the value and reason
boxes, with optional short reason buttons to reduce typing. Arabic text is kept.

**I can't tell** only opens an explanation. **Leave this value out** explicitly
records the existing Cleared action with the displayed reason. It excludes the
effective value without deleting the machine value or history. Dependent checks
may be unavailable. Cancel saves nothing. Missing fields and excluded values stay
visible; finishing the queue does not claim a complete match.

Saved decisions remain reachable under All fields. Existing additions and revision
controls stay there. Completing a review remains a separate explicit action; no
new bulk approval or evidence is invented. Source excerpts are real returned
quotes, not a new PDF image/highlight feature. Unsaved text remains a draft and
must be saved or discarded; it is not promised to survive a page refresh.

## Backend rules to carry forward

For a supported pair with compatible units, currency and before-tax price basis:
an invoice for 40 chairs at AED 110 against an order for 100 at AED 100 can show
40 of 100 listed and AED 10 more per invoiced chair, or AED 400 for those 40.
It must not call the lower invoice total an error solely because quantities differ,
or claim 60 remain to invoice without complete invoice history. Tax, discounts and
charges need their own supported facts. Missing evidence means Could not check.
G02 must specify these rules precisely before replacing the existing scoped policy.
