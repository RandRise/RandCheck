import { API, apiFetch } from "../../security/api-fetch";
import type { ReviewReport } from "./report.types";

/** The server enforces this version inside its coherent read; never retry a 412 with a newer version. */
export async function loadReport(caseId: string, runId: string, version: string, signal: AbortSignal): Promise<ReviewReport> {
  const response = await apiFetch(`${API}/api/reconciliation-cases/${encodeURIComponent(caseId)}/runs/${encodeURIComponent(runId)}/report`, {
    signal, cache: "no-store", headers: { "If-Match": `"${version}"` },
  });
  if (response.status === 412) throw new Error("Results or decisions changed. Refresh results before downloading.");
  if (!response.ok) throw new Error("The export is unavailable. Check access and try again.");
  const report = await response.json() as ReviewReport;
  if (report?.schemaVersion !== 1 || report.caseId !== caseId || report.run?.id !== runId || report.caseVersion !== version ||
    typeof report.snapshotId !== "string" || !/^[0-9a-f-]{36}$/i.test(report.snapshotId) || typeof report.reference !== "string" || typeof report.name !== "string" ||
    !Number.isFinite(Date.parse(report.capturedAtUtc)) || !Number.isInteger(report.run.runNumber) ||
    ![report.run.checks, report.run.fields, report.run.sources, report.run.findings].every(Array.isArray) ||
    !report.run.tolerances || !Object.values(report.run.tolerances).every(decimal) ||
    report.run.checks.some(check => !optionalDecimal(check.signedDifference) || !optionalDecimal(check.tolerance)) ||
    report.run.findings.some(finding => !optionalDecimal(finding.difference) || !optionalDecimal(finding.tolerance) || !Array.isArray(finding.resolutions))) {
    throw new Error("The export response is incomplete. No file was downloaded.");
  }
  return report;
}

const decimal = (value: unknown) => typeof value === "string" && /^-?\d+(?:\.\d+)?$/.test(value);
const optionalDecimal = (value: unknown) => value == null || decimal(value);
