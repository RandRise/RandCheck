# Reviewed-fact answer candidate

**Current application extension:** the [public reviewed-field contract](phase-5-reviewed-public-contract.md)
adds atomic answer/citation persistence, API/UI history and access checks, using
answer/English policy version 1.2. The private trial described below is preserved
as historical design context. Its 29/30 result has not been rewritten.

September 11, 2026. Rand approved trying this provider-free backend approach after
the source-v4 paid evaluation exposed field-selection and citation failures.
At that checkpoint this was a private candidate, with no public route or answer persistence.

## Data and authority

`CitedFactSourceStore` loads the recorded run's selected indexes for the active
actor in the execution organization. Every answer and evidence navigation reloads
authorization, index eligibility, pinned extraction and completed review under
a consistent read transaction. Quarantined/superseded indexes are unavailable.

`CitedTypedFactProjection` reconstructs the latest completed decisions and their
reviewed chunks. Confirmed fields use their reviewed extracted value; corrected
or added fields use the review value; cleared and unreviewed fields provide no
answer operand. Chunk identity, content hash, review action and original page
byte evidence must match domain reconstruction. No database migration is needed.

TRN, TIN and document identifiers are separate string fields. Leading zeroes
remain intact. This is field selection, not an arithmetic or tax-registration
validity check. Amounts use invariant decimal values with explicit bounds and
reviewed currency; quantity has no currency. Ambiguous numeric formats refuse.
Dates require a reviewed ISO date; payment terms cannot manufacture a due date.
The [due-date repair](../evidence/phase-5/P5-due-date-verification.md) adds explicit
local-1.3.0 extraction and missing-field review support, with conflict warnings.

Document roles come from unambiguous reviewed primary identifiers. A filename
or a label found in page text does not establish a role. More than one document
of the same role requires a narrower corpus.

## Supported computation and language

The versioned English selector supports a bounded vocabulary for single-field
lookups, values across documents, two-document absolute monetary differences,
highest training/onboarding/workshop line amounts, and line breakdowns. It is
not a general natural-language parser. Unknown vocabulary, ambiguous fields,
compound instructions and unparsed numeric filters refuse. Line matching occurs
within each document using reviewed description/item-code words; matching lines
need not share an occurrence number across documents.

Arithmetic uses the selected typed operands. Differences and maxima require
the same reviewed currency and complete document coverage. Ties are explicit.
A breakdown requires exact quantity × unit price + stated line tax = gross.
This does not infer a VAT rate, round a discrepancy, convert currency, determine
legal treatment or verify a payment event. Missing evidence produces explicit
incomplete/partial results; incomplete comparisons emit no computed conclusion.

Claims are backend templates. Citations carry the exact reviewed fact, its hash,
document/index/review/extraction/action identities and original page evidence.
For a correction, the reviewed support can differ from the separately labelled
original page quote. Evidence navigation resolves the stored fact again.

## Boundary and policies

- Projection: `reviewed-fact-projection-v1`.
- Source selection: `reviewed-fields-from-selected-corpus-v1`
  (all reviewed facts in the selected corpus, not the old top-five ranked hits).
- Answer: `reviewed-fact-answer-v1.1`.
- English intents: `bounded-english-fact-intents-v1.1`.
- Private CLI: `--typed-source-case`; it selects a saved synthetic run read-only,
  uses an offline answer client, and resolves every emitted citation.
- `PublicationAllowed`, `ModelInvoked` and `GeneralSemanticSupportEstablished`
  remain false. No provider dispatch, spend reservation or answer write occurs.

The closed vocabulary and role/line rules deliberately constrain this experiment.
They are not comprehensive intent-security controls or a production acceptance
claim. Future public integration needs an explicit supported-question contract,
answer/citation persistence and replay semantics, request-boundary tests, and
fresh evaluation on varied documents and wording. A read transaction is not a
claim of immediate revocation throughout a future public response lifecycle.

See the [verification](../evidence/phase-5/P5-reviewed-fact-verification.md).
