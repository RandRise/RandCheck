# Quotations outside new reviews — October 12, 2026

Rand requested removal of quotation steps from new reviews while preserving all existing work. Other design proposals remain unapproved. This report records a UI/documentation change, not a database migration or API retirement.

## Dependency inspection before editing

Safe changes: fresh case creation in the web UI uses the existing InvoicePurchaseOrder workflow; known quotations leave the new pair choices; upload and guide wording name invoice and PO; quotation-number additions remain available only for quotation documents or existing quotation fields. Saved documents remain in the document list.

Keep: Python quotation classification and label extraction; .NET Quotation enums, role validation, legacy rules, query handling and repository contracts; historical snapshot fields and quotation-number labels; document/review/case APIs; request receipts; permission checks; saved questions and audit history. Removing these would risk existing reviews. No .NET or Python production code, storage, migrations, or retained data is changed.

The word quotation in Questions can also mean a cited text excerpt. Those evidence explanations are unrelated and stay intact.

## Compatibility boundary

Fresh web creation supplies workflowKind=InvoicePurchaseOrder. A pending creation saved by the earlier UI keeps its original key and payload for recovery. This may recover (or finish) the originally requested legacy creation. Existing quotation cases retain their follow-up controls. Older API clients may still create legacy cases; API retirement is outside this change.

Upload remains a general file endpoint. It checks supported file formats, not business meaning before extraction. Unknown documents remain selectable; known quotations are hidden from new choices. Already saved quotations stay visible without silent clearing. Existing server pin validation rejects a known wrong role; UI filtering is not a security boundary.

## Review and verification

Self-review found and addressed these points:

- The saved-case list always displayed a 3-source target, including invoice/PO cases. It now states the actual saved source count without an incorrect target.
- Removing quotation options could make an already saved draft look empty. A disabled, labelled option keeps that selection visible; no write clears it automatically.
- Changing a pending creation payload would break its retry identity. Earlier stored requests keep their original payload; fresh and remounted new requests retain InvoicePurchaseOrder.
- Older UI tests created quotation cases as setup. The relevant history tests now reopen existing cases and retain their pinning, ETag, Arabic, evidence and decision assertions. A variable-name mistake in this adaptation caused 2 first-run failures and was fixed.

Focused UI verification on October 12: the initial run passed 165/170. The remaining 3 failures were reported by the harness as STACK_TRACE_ERROR; they do not establish a product defect or a verified root cause. A clean rerun of all 4 affected files passed 53/53, including all 5 initial failures. The other 3 cases passed without product or assertion changes. The latest result for each of the **170 distinct cases is passing**, across these two receipts; this is not one clean 170-case run. Do not add reruns to the count.

Receipts: ui-first.json/log and ui-rerun.json/log under outputs/quotation-workflow-20261012. TypeScript, affected-file ESLint and all 17 preview-adapter contract checks pass. The preview uses synthetic data. The documentation build has its own receipt in gates.json; it does not count as application integration. Both changed SVG diagrams were rendered to PNG and visually inspected without clipping. Browser integration and manual acceptance are not claimed.

Preservation audit: all 1,128 pre-existing Git-visible files were checked. Only the 22 deliberately edited files differ from their captured baseline; no existing file is missing and no unrelated file changed. Exact originals are retained. No .NET/Python regression rerun is claimed for this UI-only change.

Existing S3 and October 11 results remain historical. This was a self-review; no independent agent review or user acceptance is claimed.

The first documentation build passed. Its rendered check found all 180 local links and 6 described illustrations, but detected that the report page had been read before the final test receipts were saved. A final documentation-only build refreshes that report; its build and content-check receipts are stored separately. No application rebuild or new product service is needed.

Exact original files and baseline hashes: outputs/quotation-workflow-20261012/before and baseline.json. The diff compares this turn with the pre-existing dirty tree, not Git HEAD.

## Short manual checks

1. Create a fresh check from Compare documents. Follow Return to invoice check. Expect invoice and PO choices, without a quotation slot or known quotation file option.
2. Open an earlier quotation from Documents and its earlier saved case. Check that the review, old comparison, evidence and decision history remain reachable. No edit is needed.
3. Read the guide's new/earlier-work diagram. Dashboard, role and three-stage ideas must still read as proposals.

Manual results remain unreported. Integration, live identity, general Arabic/OCR quality, report/export and deployment gaps remain open.
