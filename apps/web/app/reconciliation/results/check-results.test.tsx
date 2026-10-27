import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { CheckCards, ResultSummary } from "./check-results";
import { checkNextAction, decisionReasons, resultSummary } from "./result-presentation";
import { CaseRun, ResultCheck } from "./results.types";

const check = (status: string, extra: Partial<ResultCheck> = {}): ResultCheck => ({
  id: status, fieldKey: "total", status, explanation: "Backend explanation", ...extra,
});
const run = (checks: ResultCheck[]): CaseRun => ({
  id: "run", runNumber: 1, status: "Completed", ruleSetVersion: "invoice-po-2.0", fields: [], findings: [], checks,
  coverage: { status: "Complete", conclusion: "", couldNotCheck: 0 },
});
afterEach(cleanup);

it("counts passed, differing and unavailable checks once and gives differences priority", () => {
  const saved = run([check("NoDifference"), check("DifferenceFound"), check("CouldNotCheck")]);
  // A stale/older backend summary must not override actual saved check records.
  saved.coverage = { status: "NotRecorded", conclusion: "", couldNotCheck: 0 };
  expect(resultSummary(saved)).toMatchObject({ passed: 1, differed: 1, incomplete: 1, total: 3, outcome: "Differences found" });
  render(<ResultSummary run={saved}/>);
  expect(screen.getByLabelText("Check counts").children).toHaveLength(4);
});

it.each([
  run([]), { ...run([check("NoDifference")]), status: "Running" },
  { ...run([check("NoDifference")]), coverage: undefined },
  run([check("Unknown")]), { ...run([]), ruleSetVersion: "synthetic-total-only" },
])("does not infer a complete match from empty, unknown, running or simulated coverage", saved => {
  expect(resultSummary(saved).outcome).toBe("Checks incomplete");
});

it("only claims all required checks passed for complete recorded checks", () => {
  expect(resultSummary(run([check("NoDifference")])).outcome).toBe("All required checks passed");
});

it("shows exact saved values and Arabic evidence under the correct document roles", () => {
  const saved = run([check("DifferenceFound", { leftValue: "100", rightValue: "110", leftFieldSnapshotId: "po-field", rightFieldSnapshotId: "invoice-field", signedDifference: 10, tolerance: .01, currency: "AED" })]);
  saved.fields = [
    { id: "po-field", role: "PurchaseOrder", key: "total", occurrence: 0, value: "100", page: 2, quote: "طلب 100" },
    { id: "invoice-field", role: "Invoice", key: "total", occurrence: 0, value: "110", page: 3, quote: "فاتورة 110" },
  ];
  render(<CheckCards run={saved} busy={false} renderDecision={() => null}/>);
  expect(screen.getByText("Invoice").parentElement?.textContent).toContain("110Page 3فاتورة 110");
  expect(screen.getByText("Purchase order").parentElement?.textContent).toContain("100Page 2طلب 100");
  expect(screen.queryByText("Source")).toBeNull();
});

it("uses a saved source when a missing value has no field snapshot, with one next action", () => {
  const missing = check("CouldNotCheck", { reasonCode: "checked_numeric_value_unavailable" });
  const saved = run([missing]);
  saved.sources = [{ id: "source", role: "Invoice", documentId: "old-document", extractionRunId: "old-extraction" }];
  const fix = vi.fn();
  render(<CheckCards run={saved} busy={false} onFix={fix} renderDecision={() => null}/>);
  expect(screen.getAllByRole("button")).toHaveLength(1);
  fireEvent.click(screen.getByRole("button", { name: "Review invoice details" }));
  expect(fix).toHaveBeenCalledWith("Invoice", "total", 0, "old-document", "old-extraction");
  expect(checkNextAction(saved, missing).source?.documentId).toBe("old-document");
});

it("keeps a decided incomplete check incomplete and offers missing-information reasons", () => {
  const saved = run([check("CouldNotCheck", { findingId: "finding", reasonCode: "amount_basis_unavailable" })]);
  const finding = { id: "finding", ruleCode: "PAIR-NUMBER", fieldKey: "total", severity: "Warning", explanation: "", resolutions: [{ id: "decision", sequenceNumber: 1, action: "Resolved", reason: "Requested clarification", createdAt: "2026-10-23" }] };
  saved.findings = [finding];
  const choices = decisionReasons(finding, saved.checks![0]);
  expect(choices).toContain("Requested the missing information");
  expect(choices).not.toContain("Agreed price confirmed with the supplier");
  expect(resultSummary(saved)).toMatchObject({ outcome: "Checks incomplete", passed: 0, incomplete: 1 });
  const decision = vi.fn(() => <p>Decision remains separate</p>);
  render(<CheckCards run={saved} busy={false} renderDecision={decision}/>);
  expect(decision).toHaveBeenCalledTimes(1);
  expect(within(screen.getByRole("article")).getByText("Could not be checked")).toBeTruthy();
});

it("labels simulations and offers different quantity and supplier reasons", () => {
  render(<ResultSummary run={{ ...run([]), ruleSetVersion: "synthetic-total-only" }}/>);
  expect(screen.getByText("Sample preview only.")).toBeTruthy();
  const base = { id: "f", ruleCode: "PAIR", severity: "Warning", explanation: "", resolutions: [] };
  expect(decisionReasons({ ...base, fieldKey: "line_quantity" })).toContain("Supplier confirmed a partial invoice");
  expect(decisionReasons({ ...base, fieldKey: "supplier_trn" })).toContain("Requested the correct document");
});

it("keeps unknown backend explanations visible and does not relabel historical quotation runs", () => {
  const saved = run([check("CouldNotCheck", { reasonCode: "future_reason", explanation: "A new type of evidence is missing." })]);
  render(<CheckCards run={saved} busy={false} renderDecision={() => null}/>);
  expect(screen.getByText("A new type of evidence is missing.")).toBeTruthy();
  cleanup();
  render(<ResultSummary run={{ ...run([]), ruleSetVersion: "1.0", sources: [{ id: "q", role: "Quotation", documentId: "old" }] }}/>);
  expect(screen.getByText(/These results cover the saved documents/)).toBeTruthy();
  expect(screen.queryByText(/saved invoice and purchase order only/)).toBeNull();
});

it("targets header currency at row 0 and opens broad details for aggregate quantity gaps", () => {
  const saved = run([]);
  saved.sources = [{ id: "i", role: "Invoice", documentId: "invoice", extractionRunId: "extraction" }];
  const currency = checkNextAction(saved, check("CouldNotCheck", { fieldKey: "line_tax", occurrence: 2, reasonCode: "tax_currency_unavailable" }));
  expect(currency).toMatchObject({ key: "tax_currency", occurrence: 0, source: { documentId: "invoice", extractionRunId: "extraction" } });
  const quantity = checkNextAction(saved, check("CouldNotCheck", { fieldKey: "total", occurrence: 0, reasonCode: "different_or_unverified_quantities" }));
  expect(quantity.key).toBeUndefined();
  expect(quantity.source?.extractionRunId).toBe("extraction");
});

it("does not describe an unknown future status as a known difference", () => {
  render(<CheckCards run={run([check("FutureStatus")])} busy={false} renderDecision={() => null}/>);
  expect(screen.getByText(/no recognized outcome/)).toBeTruthy();
  expect(screen.queryByText(/checked values differ/)).toBeNull();
  expect(screen.queryByText(/Confirm the difference/)).toBeNull();
});
