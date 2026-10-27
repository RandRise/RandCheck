import type { ReviewReport } from "./report.types";

/** Synthetic test fixture; never used by the application transport. */
export function reportFixture(): ReviewReport {
  return { schemaVersion: 1, snapshotId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", caseId: "case-1", reference: "REC-2026-000001", name: "Office order",
    caseVersion: "version-1", capturedAtUtc: "2026-10-27T12:00:00Z", run: {
      id: "run-1", runNumber: 2, status: "Completed", ruleSetVersion: "invoice-po-2.0", evaluatedAt: "2026-10-26T12:00:00Z",
      coverage: { status: "Incomplete", conclusion: "Missing information", couldNotCheck: 1 },
      tolerances: { quantity: "0", unitPrice: "0.01", tax: "0.01", lineTotal: "0.01", documentTotal: "0.01" },
      sources: [{ id: "source-1", role: "Invoice", documentId: "invoice-1", reviewSessionId: "review-1", extractionRunId: "extraction-1", sha256: "a".repeat(64) },
        { id: "source-2", role: "PurchaseOrder", documentId: "po-1", reviewSessionId: "review-2", extractionRunId: "extraction-2", sha256: "b".repeat(64) }],
      fields: [{ id: "field-1", documentSnapshotId: "source-1", role: "Invoice", sourceKind: "Confirmed", key: "total", occurrence: 0, value: "999999999999999999.123456", page: 1, quote: "الإجمالي 999999999999999999.123456\nAED" }],
      checks: [{ id: "check-1", fieldKey: "total", status: "DifferenceFound", explanation: "Amounts differ", leftValue: "0", rightValue: "999999999999999999.123456", signedDifference: "999999999999999999.123456", tolerance: "0.01", rightFieldSnapshotId: "field-1", findingId: "finding-1" },
        { id: "check-2", fieldKey: "line_unit_price", status: "CouldNotCheck", explanation: "Price basis missing", findingId: "finding-2" }],
      findings: [{ id: "finding-1", ruleCode: "Total", severity: "Warning", leftRole: "PurchaseOrder", rightRole: "Invoice", leftValue: "0", rightValue: "999999999999999999.123456", explanation: "Amounts differ", difference: "999999999999999999.123456", tolerance: "0.01", resolutions: [
        { id: "decision-1", sequenceNumber: 1, action: "Accepted", reason: "Agreed change", actorId: "actor-1", createdAt: "2026-10-27T10:00:00Z" },
        { id: "decision-2", sequenceNumber: 2, action: "Resolved", reason: "Needs another check", actorId: "actor-2", createdAt: "2026-10-27T11:00:00Z" }] },
        { id: "finding-2", ruleCode: "Price", severity: "Warning", explanation: "Price basis missing", resolutions: [] }],
    } };
}
