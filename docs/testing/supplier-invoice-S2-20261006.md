# Supplier invoice S2 independent verification

October 6, 2026. The explicitly selected `invoice-po-1.0` backend policy has passed the focused automated gate: **168 distinct tests**, including actual disposable PostgreSQL. Rand's instruction to continue authorized this S2 batch; no S1 or S2 manual result has been reported. Manual acceptance remains pending. This is backend policy, projection, HTTP contract and persistence proof with independent synthetic reviewed inputs. It does not establish complete extractor-to-review integration, OCR, browser, customer-demand, live identity or R5/R6 acceptance.

## Baseline

Use project runtimes from `tools/use-project-runtimes.ps1`, existing restored packages, one runner at a time and narrow projects only. No full solution build, package installation, demo or retained-database migration.

```powershell
. ./tools/use-project-runtimes.ps1
dotnet test apps/backend/tests/UaeDocumentIntelligence.Domain.Tests/UaeDocumentIntelligence.Domain.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~ReconciliationRuleEngineTests|FullyQualifiedName~ReconciliationDomainTests' --logger 'trx;LogFileName=baseline-domain.trx' --results-directory outputs/supplier-invoice-S2-20261006
dotnet test apps/backend/tests/UaeDocumentIntelligence.Application.Tests/UaeDocumentIntelligence.Application.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~ReconciliationHandlerTests|FullyQualifiedName~ReconciliationRepeatTests' --logger 'trx;LogFileName=baseline-application.trx' --results-directory outputs/supplier-invoice-S2-20261006
dotnet test apps/backend/tests/UaeDocumentIntelligence.Api.Tests/UaeDocumentIntelligence.Api.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~ReconciliationApiContractTests|FullyQualifiedName~ReconciliationRepeatContractTests|FullyQualifiedName~ReconciliationRepeatSecurityTests|FullyQualifiedName~ReconciliationMetadataContractTests' --logger 'trx;LogFileName=baseline-api.trx' --results-directory outputs/supplier-invoice-S2-20261006
```

Baseline complete: domain **13/13**, zero failures/skips, 304 ms test duration; application **9/9**, zero failures/skips, 817 ms; API **27/27**, zero failures/skips, 17 seconds. These disjoint selectors total **49 unique tests**. TRX and matching `.txt` logs are preserved under `outputs/supplier-invoice-S2-20261006`. Test duration excludes project compilation/startup. Counts from overlapping later reruns must not be added.

## Independent expected behavior

Freeze reviewed input values, source references and expected results before scoring the new policy. Self-authored synthetic source evidence measures policy behavior; it does not establish extraction or OCR accuracy. Values must be explicitly sourced or corrected with preserved original evidence. Missing units, bases or scope cannot be supplied by a fixture default that hides the actual coverage gap.

| Case | Expected result | Verification boundary |
| --- | --- | --- |
| Complete same-currency invoice and PO, exact headers/rows | Completed checks match with no missing quotation warning; truthful complete coverage only when every required value/basis/source is present | Domain and application |
| Known amount, quantity or price mismatch | Correct direction/absolute difference and exact captured tolerance; both source fields retained | Domain, projection and API |
| Legacy rule set 1.0 | Existing quotation/PO/invoice semantics and stored runs remain unchanged | Existing regressions and explicit old/new dispatch |
| Missing document, identity, currency, amount, tax/basis, rows or provenance | Explicit unavailable coverage; zero findings alone cannot mean complete agreement | Domain and API coverage |
| Malformed ASCII amount or Arabic/mixed digits in reviewed values | No accepted numeric amount through raw fallback or permissive decimal parsing; raw/source values preserved | Domain and reviewed projection |
| Legacy machine raw `1,23` with misleading normalized `123`, or Arabic raw with ASCII normalization, merely confirmed | New policy must not silently accept the legacy conversion; require supported explicit correction | Application and API |
| Explicit valid correction to English digits | Use the entered corrected value and retain its action identity and original page quote without claiming the quote contained the correction | Application and API |
| Invoice number plus body PO-number reference | Preserve role-specific invoice identity without duplicate `document_number` snapshots | Application |
| Incompatible/missing currencies, units or price/amount/tax bases | Suppress the affected monetary/quantity subtraction and state which checks are unavailable; unrelated eligible checks may remain meaningful. Quantity/price-per-unit basis such as `1 EA` does not establish the unit price's net/gross amount basis | Domain and API |
| Difference equal to tolerance; smallest supported amount above it | Equal boundary matches; above boundary differs without rounding | Domain |
| Three or more duplicate identities on either side | Identity stays permanently ambiguous; no later duplicate becomes uniquely matched | Domain |
| Reordered unique lines | Match exact supported identities rather than row position; preserve both occurrence/source references | Domain |
| Partial or unknown invoice/PO scope | Ineligible for whole-order complete agreement; no inferred cumulative invoicing or delivery result | Domain and application |
| Partial extraction/omitted row knowledge | Frozen coverage reflects the reviewed extraction context and cannot be reconstructed from today's case pins | Application and persistence |
| Completed review with an action-null machine field | Source kind remains Unchecked; completion status alone cannot turn an unchecked value into a trusted comparison | Application |
| Non-matching or unavailable check | Each DifferenceFound/CouldNotCheck references an owned finding so the existing findings-only UI cannot hide unavailable coverage | Domain and API |
| New review/re-pinning/new policy after an old completed run | Old document/review snapshots, tolerances, findings, coverage and rule version stay immutable | Actual disposable persistence if changed |
| 18 integer digits plus six fractional digits | No decimal overflow, persistence overflow or rounding; either exact supported persistence or explicit unavailable coverage before save | Domain and actual persistence if changed |
| Stale ETag, denied roles/organization, duplicate or uncertain request | Existing permissions, audit and concurrency guarantees remain; no mutation replay through a read/retry and no repeated evaluation from the same stale version | Application/API; persistence for changed guarantees |

## Final focused verification

Tester-owned new files are `SupplierInvoicePairPolicyVerificationTests.cs` in Domain.Tests, `SupplierInvoicePairHandlerVerificationTests.cs` in Application.Tests, `SupplierInvoicePairContractVerificationTests.cs` in Api.Tests and `SupplierInvoicePairPersistenceTests.cs` in Persistence.Tests. The parent saved the exact existing schema-test baseline before authorizing additive migration/table/constraint/index expectations in `PostgresPersistenceTests.cs`; all old assertions remain.

| Group | Final distinct passing checks | Exact receipt |
| --- | ---: | --- |
| Domain: 62 independent pair scenarios, 25 strict-number cases, 13 legacy cases | 100 | `domain-complete-final.trx` (343 ms reported suite duration) |
| Application: raw/corrected/unchecked/type/role/warnings/ETag plus legacy behavior | 22 | `application-final.trx` (1 second) |
| API: explicit policy/scope, ETags, frozen result/source shape, existing permissions/audit/contracts | 34 | `api-first.trx` (25 seconds) |
| Actual PostgreSQL: five new policy cases and seven existing schema/migration/history/repeat cases | 12 | Latest-per-test-name union in `postgres-distinct-final.json` |

These disjoint groups total **168**, including the original 49 baseline cases and seven existing PG cases. The 112 new cases are not added again to that total. Every final test passed; no skip/not-executed result substitutes for coverage. `final-status.json` records the counters and boundaries. No full solution build, broad suite, OCR, paid provider, identity service or app demo ran. The persistence project's necessary referenced backend/tool projects compiled during its narrow test command; that is not a separate acceptance run.

Final commands use the runtime bootstrap above, `--no-restore -m:1`, existing packages, one runner at a time:

```powershell
dotnet test apps/backend/tests/UaeDocumentIntelligence.Domain.Tests/UaeDocumentIntelligence.Domain.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~SupplierInvoicePairPolicyVerificationTests|FullyQualifiedName~SupplierInvoiceNumberTests|FullyQualifiedName~ReconciliationRuleEngineTests|FullyQualifiedName~ReconciliationDomainTests' --logger 'trx;LogFileName=domain-complete-final.trx' --results-directory outputs/supplier-invoice-S2-20261006
dotnet test apps/backend/tests/UaeDocumentIntelligence.Application.Tests/UaeDocumentIntelligence.Application.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~ReconciliationHandlerTests|FullyQualifiedName~ReconciliationRepeatTests' --logger 'trx;LogFileName=application-final.trx' --results-directory outputs/supplier-invoice-S2-20261006
dotnet test apps/backend/tests/UaeDocumentIntelligence.Api.Tests/UaeDocumentIntelligence.Api.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~ReconciliationApiContractTests|FullyQualifiedName~ReconciliationRepeatContractTests|FullyQualifiedName~ReconciliationRepeatSecurityTests|FullyQualifiedName~ReconciliationMetadataContractTests' --logger 'trx;LogFileName=api-first.trx' --results-directory outputs/supplier-invoice-S2-20261006
```

Initial failed receipts remain: `domain-first.trx` 40 passed/57 failed used the legacy-only engine in a new-policy fixture; `domain-explicit-engine.trx` 39 passed/58 failed records a test patch applied to the wrong call site; `domain-corrected-helper.trx` 96 passed/1 failed used Link instead of the public Replace operation for repinning. These were fixture/setup mistakes; assertions and input expectations were preserved. `application-first.txt` records a test compile error requiring the fixture's deferred enumerable to materialize as `IReadOnlyCollection`; no application tests ran in that attempt. The reviewed duplicate fixture now declares the actual authored row count, requires verified source coverage, records every duplicate member, and withholds numeric matching, so unavailable row coverage cannot mask a duplicate-identity defect.

Fresh disposable PostgreSQL 16.4 was initialized at the owned `outputs/supplier-invoice-S2-20261006/pg-78d6e87e023e/data` path; `cluster.json` and `pg-verified.txt` record its absolute target, synthetic owner and loopback-only `127.0.0.1:25440` listener. Existing test-database guards restricted destructive resets to `document_intelligence_test`. Restricted-token initdb failed before server creation (`initdb-restricted.txt`); the approved identical native initialization succeeded (`initdb-approved.txt`). No retained database or old cluster was used. The new history INSERT gap was fixed before executing the frozen PostgreSQL gate: a server-assigned full transaction ID permits the original atomic save and refuses later child insertion. Actual testing proves savepoint success, four child-table append refusals, marker forgery refusal/override, update/delete immutability, preserved source/actor/time/check values after decisions/re-pinning/new scope/legacy policy, downgrade refusal with history, exact 24,6 signed differences, 18,6 tolerances, restricted-role writes/audit, concurrency, raw-value rejection, scoped foreign keys and no model drift.

The original PG command ran 12 cases: **10 passed / 2 failed**, zero skips, 2m53s (`postgres-first.trx`). The first failed fixture supplied invalid JSON `[]changed`, which failed type parsing before the intended immutable-history guard; it now mutates valid JSON. The second tried the unchanged legacy policy on an invoice with both invoice-number and PO-reference aliases, which the legacy projection cannot combine. Its repair preserves the original complete pair truth, pins a separate explicitly legacy-compatible reviewed invoice, and still proves the original run is unchanged. The two-case rerun (`postgres-affected-final.trx`) passed the restricted-role/savepoint/append/marker/downgrade case and exposed unordered Check collection comparison in history (one pass / one fail). Sorting by stable IDs and comparing every DTO record property replaced order-sensitive JSON strings; no value/quote/action/basis property is removed. `postgres-history-final.txt` preserves a nullable compile-only error, with no tests executed. The final history-only test obtains a fresh persisted GET baseline and verifies exact actor, evaluation time, version, scope, tolerances, coverage and all source/field/check values: **1/1 passed**, actual TRX case duration 5.4986717s (`postgres-persisted-history-final.trx`; VSTest's one-case aggregate prints less than 1ms).

The final PG result is **12 distinct passes assembled from those receipts**, not a single all-green 12-case run. Ten passes come from the first receipt, one from the affected receipt and one from the persisted-history receipt. Exact test names, latest outcomes and their source receipts are frozen in `postgres-distinct-final.json`.

```powershell
$env:TEST_POSTGRES_CONNECTION = 'Host=127.0.0.1;Port=25440;Database=postgres;Username=supplier_invoice_test;Pooling=false'
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests/UaeDocumentIntelligence.Persistence.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~Supplier_pair_|FullyQualifiedName~Expected_schema_constraints_and_indexes_exist|FullyQualifiedName~Migration_apply_is_idempotent_and_rollback_reapply_succeeds|FullyQualifiedName~Reconciliation_graph_round_trips_with_frozen_sources_findings_and_resolutions|FullyQualifiedName~Case_repeat_' --logger 'trx;LogFileName=postgres-first.trx' --results-directory outputs/supplier-invoice-S2-20261006
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests/UaeDocumentIntelligence.Persistence.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~Supplier_pair_restricted_role_persists_exact_large_signed_difference_owned_findings_and_frozen_sources|FullyQualifiedName~Supplier_pair_source_checks_remain_frozen_across_resolution_scope_and_policy_evaluations' --logger 'trx;LogFileName=postgres-affected-final.trx' --results-directory outputs/supplier-invoice-S2-20261006
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests/UaeDocumentIntelligence.Persistence.Tests.csproj --no-restore -m:1 --filter 'FullyQualifiedName~Supplier_pair_source_checks_remain_frozen_across_resolution_scope_and_policy_evaluations' --logger 'trx;LogFileName=postgres-persisted-history-final.trx' --results-directory outputs/supplier-invoice-S2-20261006
```

Migration count 23 came from the actual migration inventory. Existing schema/migration assertions remain intact with additive new table/check/FK/index expectations. `verification-final-source-hashes.json` holds the final 22 production/doc/test/wrapper hashes; implementation's final manifest records its doc-only activation clarification separately from unchanged production hashes. Initial attempts and hashes remain preserved. Independent source review passed both policy code and new tests, including repair of the duplicate fixture's row count. Offline vulnerability-feed unavailability during EF generation is not a vulnerability-audit pass.

Cleanup is complete: `fixture-database-absent.txt` is zero, `pg-stop.txt` records successful shutdown of only the exact verified owned cluster, and `cleanup.txt` records port 25440 absent. Data/log/evidence files remain; there was no recursive deletion or global cleanup.

## Actual policy manual milestone

From the repository root, run:

```powershell
./outputs/supplier-invoice-S2-20261006/check-policy.ps1
```

The tiny wrapper uses existing project runtimes and one actual domain-policy test. It should print a matching pair of 1260 AED, an invoice of 1261 versus PO 1260 with signed difference +1 AED, and incompatible USD/AED as CouldNotCheck, then the unique JSON receipt path. The JSON includes original quotes, field IDs/source kinds, sources and checks. The tested invocation (`manual-checkpoint.txt`) passed one test and wrote `manual-20261006T1226481563046-ae426e.json`; the final Domain run also exercises this same fact with the clearer console summary. This source-authored reviewed-input exercise is actual backend policy proof, not extractor/PDF/OCR, HTTP admission, browser or customer proof. S1 manual acceptance remains pending; no S2 manual feedback has been reported. The current extractor profile alone does not activate this policy or provide every checked unit, tax currency, supplier-specific TRN and basis. The next product workflow must select `invoice-po-1.0` explicitly; legacy 1.0 and its known number-alias restriction remain unchanged. S3-S6 have not started.
