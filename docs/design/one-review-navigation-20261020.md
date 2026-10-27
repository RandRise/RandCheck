# One place to check an invoice

Approved direction, October 20, 2026. This implements the numbered navigation
and simpler Home requested by Rand, together with the earlier approved G02/G03
direction. It does not approve a new visual theme, Owner roles or reviewed export.

## The path

**Home → Check an invoice → 1 Add documents → 2 Check details → 3 See results**

Home also lists saved checks with Continue. Each check has a stable reference.
The initial name is “Invoice check”; supplier-based names and richer list details
remain later improvements. Creating the saved check is one deliberate click.
There is no routine name/category form.

1. Upload or choose an invoice and PO. Saving a selection does not confirm values.
2. Check each document beside its source in the same workspace. Complete its
   review explicitly. Continue to purchase order saves the exact invoice review
   displayed. Show results saves the exact PO review displayed and requests a
   comparison. No request to load a newer review is substituted.
3. Read differences, missing checks, evidence and decision history in place.
   Check this value returns to the matching source only when document and
   extraction identities support it. Earlier results retain their original facts.

Back to Home keeps saved work. Unsaved edits require a discard decision.
Navigation is blocked during writes. A lost reply requires a read-only recovery
before another write; partial pins remain saved. Closed checks are read only.
Old document and comparison links, quotations and audit history stay accessible.

## Backend policy

New requests use `invoice-po-2.0`, without an order-scope question. The persisted
Unknown context is a compatibility value, not a user declaration. Version 1
remains supported with its original scope requirement.

Unique checked items with compatible units can show invoice quantity, PO
quantity and signed difference. A smaller quantity is not an error by itself.
Checked compatible price bases permit unit-price comparison. Aggregate totals
require complete trusted row coverage and equal checked quantities across the
same unambiguous item set, plus each amount's currency/basis requirements.
Missing facts still produce Could not check. AED evidence remains required.

The policy never infers final invoice, remaining balance, delivery or payment
approval. It does not yet calculate the earlier illustrative quantity-times-price
impact example. Reviewed report/CSV export is still separate work.

## Acceptance and verification

Check exact review pinning, stale tokens, lost replies, partial saves, dirty Back,
inline decision saves, closed checks, reordered extraction rows, quotation history,
Arabic guidance, old policies and database evidence protection. Review source
against the captured dirty-tree baseline; never treat earlier uncommitted work
as part of this batch.

[Verification and remaining checks](../testing/navigation-flow-20261020.md).
Source implementation and automated tests do not establish manual acceptance or
live extraction-to-database integration.
