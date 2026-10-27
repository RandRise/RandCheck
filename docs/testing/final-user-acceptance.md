# Final testing with fresh invoices and purchase orders

Status: **test plan, not test results**. No scenario below is marked passed solely
by appearing in this checklist. On October 27, Rand delegated hands-on verification
to the implementation agent. Fresh local results are recorded separately in the
[journey report](local-journey-20261027.md),
[language/Admin report](local-language-security-20261027.md) and
[clean-source report](clean-source-delivery-20261027.md). Those bounded checks do
not imply every variation below ran, or that the user accepted them manually.

Use this for the final release check after the remaining features are ready.
Your earlier individual tests still count as their own evidence; this final pass
checks that the whole process works together on new files.

## Prepare a small test pack

Use fictional or permitted redacted documents. Give files readable names such as
`Acme-PO-1001.pdf` and `Acme-Invoice-2001.pdf`. Keep Arabic text and English digits.
Keep the original expected values in a separate note, so the app cannot be its
own answer key. Do not reset the existing library or overwrite old results.

| Pair | Put these facts in the documents |
| --- | --- |
| A: agreement | Same clear supplier, AED, order reference, quantities, units, prices, tax and totals. All required facts present. |
| B: differences | Copy A with a different invoice quantity, price and total. Write down each expected difference. |
| C: missing facts | Keep checked AED currency with source text on both files, but leave out a necessary price, quantity or basis on different copies. Include one unreadable value. |
| D: different quantities | Invoice fewer units than the PO; also make a copy with an unclear quantity. These test factual invoice-versus-PO quantities, not how much remains to be billed. |
| E: difficult lines | Reorder items; add duplicate descriptions; add an extra item; remove one item; use incompatible or unclear units. |
| F: language and format | One clear Arabic/English native PDF and one scanned copy. Include a rotated or low-quality page for a failure check. |
| G: unsupported/conflicting facts | Non-AED currency; separate copies with missing, unchecked or ambiguous currency; wrong document type, conflicting supplier details and unclear order reference. |

Run A from upload to export first. Then use B–G for the targeted checks below.
Reuse a fresh pair across related checks where practical; record the saved review
and comparison number each time. Never manufacture a missing fact just to get a pass.

## How to record a result

For each ID, record **Pass / Fail / Blocked / Not run**, date, app version,
account role, file names, case reference, review versions and comparison number.
Write expected versus actual behavior. Add a screenshot or downloaded artifact
when useful, without exposing private data. Blocked means a prerequisite is
missing; it does not mean the scenario passed.

## 1. Upload and start the check

| ID | What to try | Expected result |
| --- | --- | --- |
| U01 | Upload only A's invoice. | File is retained; no invoice check is created until both choices are saved. |
| U02 | Add A's PO and continue. | One check opens with the correct two files. No values are confirmed automatically. |
| U03 | Use one new file and one saved file. | Correct pair is saved; neither file is duplicated just by selecting it. |
| U04 | Choose an invoice for the PO position; choose the same file twice. | Wrong type or invalid pair is blocked with a clear next action. |
| U05 | Upload an unsupported, empty or damaged file. | Clear error or unsupported status; no false successful review. Use a disposable fixture. |
| U06 | Watch queued/processing, then Ready or Partial. | Actual server state is shown. Extraction success does not mean values were checked. Partial results remain explicit. |
| U07 | Refresh or double-click during upload/creation; retry a lost response. | Same request is recovered safely; no duplicate check or duplicate operation from a retry. Network-failure setup may need an assisted check. |
| U08 | Reprocess a previously reviewed document. | Earlier review/results remain accessible; new extraction is separate and unconfirmed. |
| U09 | Cancel a queued job if cancellation is offered. | Cancellation is truthful; cancelling cannot erase existing saved history. |

## 2. Check details

| ID | What to try | Expected result |
| --- | --- | --- |
| R01 | Review A invoice, then PO. | Values and evidence belong to the correct document. Complete each review explicitly. |
| R02 | Correct one extracted value and choose a suitable reason. | Saved value and audit reason survive refresh; original extraction remains traceable. |
| R03 | Leave a required value unchecked. | No silent confirmation; completion or comparison reports the applicable missing check. |
| R04 | Mark an unavailable value missing or exclude an unusable value. | The app preserves uncertainty; it does not invent a value or claim agreement. |
| R05 | Leave with an unsaved correction; use browser Back and Details. | Warning protects edits; returning to optional details does not lose the active document. |
| R06 | Refresh after saving invoice but before saving PO. | The correct pair and saved review remain. No new comparison is created by refresh. |
| R07 | Correct line values and inspect their page evidence. | Correct item/row is changed; other rows stay unchanged. |
| R08 | Review F's Arabic/English text. | Text is legible and ordered correctly; numbers use English digits. Wrong OCR remains visible for correction. |
| R09 | Review G's non-AED invoice. | It is not silently converted or relabelled AED. New comparison is blocked and earlier saved results remain. |
| R10 | Watch the numbered phases and revisit a saved phase. | Current = purple plus text, saved setup = green plus check/text, next = grey. Only one current phase. Colour never proves a match. |

## 3. Compare and read the result

| ID | What to try | Expected result |
| --- | --- | --- |
| C01 | Compare fully reviewed A. | All required checks passed only if the backend actually performed and passed every required check. If a prerequisite is unsupported, report that gap rather than forcing a match. |
| C02 | Compare B. | Expected differences and compared values are shown with correct Invoice/PO labels. |
| C03 | Compare C after both documents pass the checked-AED gate. | Missing price/quantity/basis checks name the missing fact and offer a useful next action; no false full pass. |
| C04 | Compare a pair with both differences and missing facts. | Both remain visible. Passed + differed + could not check equals total checks. |
| C05 | Compare D. | Compare only the quantities stated on this invoice and PO; unclear quantities remain incomplete. No required full/partial invoice question, remaining-to-bill calculation or whole-order settlement claim. |
| C06 | Compare E with reordered items. | Rows are matched by supported evidence, not simply by position. |
| C07 | Compare E with duplicate/ambiguous descriptions. | Ambiguous lines stay unresolved; the app does not pick an arbitrary match. |
| C08 | Compare E with extra/missing items or incompatible units. | Unsupported comparison bases and line gaps are visible; prices are not compared as if their units match. |
| C09 | Inspect quantity × price, subtotal, tax and rounding. | Supported checks follow documented policy/tolerance; unsupported arithmetic remains incomplete. Record the exact boundary cases for an assisted test. |
| C10 | Use conflicting supplier details or PO references. | No unsupported claim that they are the same supplier/order. Broader supplier identity rules still require a separate scope decision. |
| C11 | Create a newer review, then inspect an older comparison. | Old comparison keeps its exact original reviewed versions, values and evidence. New edits do not rewrite it. |
| C12 | Change one selected document after results exist. | Old results remain clearly saved history; they are not presented as a fresh comparison of the new pair. |
| C13 | Repeat Show results after a timeout or refresh. | Safe retry behavior; no accidental extra comparison from replaying the same request. |
| C14 | Try G with missing, unchecked, ambiguous or non-AED currency on either file; also try missing currency evidence. | The AED gate blocks a new evaluation with a clear message. No new result is saved and earlier results remain unchanged. Never guess or relabel a foreign amount to get through the gate. |

## 4. Decisions, navigation and questions

| ID | What to try | Expected result |
| --- | --- | --- |
| D01 | Record a decision for a price difference and for a missing check. | Reason choices fit the scenario; recording a decision never changes a check into Passed. |
| D02 | Refresh and open the same result from History. | Correct comparison, evidence, decision and actor record remain. |
| D03 | Change a decision in another tab, then save a stale form. | Conflict is explained; a stale save does not silently overwrite newer work. Use two permitted sessions. |
| D04 | Search Library by filename, supplier and printed invoice/PO number. | Relevant accessible files appear; pages contain at most 10. Historical quotations remain opt-in and readable. |
| D05 | Open Questions from a case and use its documents. | Exact currently pinned case reviews load together. Opening the page alone does not prepare or confirm them. |
| D06 | Search reviewed filenames and change pages. | At most 10 review options; selected sources remain. Search does not submit a question. |
| D07 | Ask a supported question, then a question whose fact is missing. | Supported answer cites the right saved evidence; missing/unsupported facts stay unanswered. This still needs a full live answer check. |
| D08 | Use Dark/Light, keyboard and a narrow window. | Text, icons, focus and phase states are legible; no clipped actions. Sidebar icons keep their text labels. |

## 5. Access, export and final release checks

| ID | What to try | Expected result |
| --- | --- | --- |
| A01 | Sign in as Staff and then a separately authorized Admin. | Staff has its permitted work tools; only Admin sees administration links and can use those endpoints. Never promote an account just to make a link appear. |
| A02 | Open an admin URL as Staff; try another organization's document URL. | Access is denied without leaking content. This requires prepared accounts and an assisted API check. |
| A03 | Revoke/change access during a session; sign out and back in. | Stale private content is cleared and API access follows current permissions. Legacy account migration needs its own approved check. |
| E01 | Download today's review-history JSON. | Valid technical review history with saved actions. This is not the final comparison report or CSV. Current filename uses the document ID. |
| E02 | Download the readable HTML report and CSV. | **Implemented and locally checked; repeat with your fresh pair.** Useful filenames, exact compared values, evidence, decisions, differences and missing checks; report and CSV represent one consistent saved version. |
| E03 | Open exports with Arabic, commas, quotes and multiline text. | **S5 locally checked; repeat with fresh files.** Correct encoding/columns; spreadsheet formulas from document text cannot execute. |
| E04 | Export an old comparison after new reviews or decision changes. | **S5 locally checked; repeat with fresh files.** Original comparison is preserved and the exported decision version/cutoff is clear. No mixing of separately fetched versions. |
| E05 | Download original files and test denied export access. | Correct file, useful filename, no content leakage. Deployment storage/download checks remain open. |
| F01 | Run A end to end in the release environment. | Real upload/worker → review → backend comparison → decision → downloaded report/CSV. No preview simulation substitutes for a step. |
| F02 | Exercise one recoverable worker/API outage with fresh test data. | Work survives; recovery action is clear; no silent completion or duplicate writes. Assisted environment check. |
| F03 | Check startup/configuration, actual identity, storage and deployment. | Required services and permissions work in the target environment. Local Development identity is insufficient evidence. |

## What must wait

The local synthetic comparison and library checks are earlier evidence, not fresh
acceptance of these new documents. Before U06/F01, confirm the real upload worker
is running; the current isolated manual workspace does not enable it by default.
Before A01–A03, prepare real permitted Staff/Admin accounts. Before E02–E04/F01,
complete the reviewed report/CSV work. Scanned Arabic/OCR support must be measured
against the declared supported layouts; unsupported cases must remain explicit.

This is the release checklist for the current scope, not a promise to test every
possible document. New supported formats or business rules need additional cases.
