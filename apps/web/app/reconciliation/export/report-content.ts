import type { ReportCheck, ReportField, ReportFinding, ReportRun, ReviewReport } from "./report.types";
import { checkTitle } from "../results/result-presentation";

export const roleName = (role?: string) => role === "PurchaseOrder" ? "Purchase order" : role === "Invoice" ? "Invoice" : role ?? "Document";
export const checkStatus = (status: string) => status === "NoDifference" ? "Passed" : status === "DifferenceFound" ? "Differed" : "Could not be checked";
export const decisionName = (action: string) => ({ Accepted: "Accepted or acknowledged", Resolved: "Follow-up recorded", Dismissed: "No action needed" }[action] ?? action);
export const label = (key: string) => key.replaceAll("_", " ").replace(/([a-z])([A-Z])/g, "$1 $2");

export function reportSummary(run: ReportRun) {
  const passed = run.checks.filter(check => check.status === "NoDifference").length;
  const differed = run.checks.filter(check => check.status === "DifferenceFound").length;
  const incomplete = run.checks.length - passed - differed;
  const recorded = run.checks.length > 0 && /^invoice-po-[12]\.0$/.test(run.ruleSetVersion);
  const allPassed = recorded && run.status === "Completed" && incomplete === 0 && differed === 0 &&
    run.coverage?.status === "Complete" && !run.findings.some(finding => !run.checks.some(check => check.findingId === finding.id));
  const olderDifference = run.findings.some(finding => !run.checks.some(check => check.findingId === finding.id) &&
    differenceExceeds(finding.difference, finding.tolerance));
  return { passed, differed, incomplete, total: run.checks.length, recorded,
    outcome: differed > 0 || olderDifference ? "Differences found" : allPassed ? "All required checks passed" : "Checks incomplete" };
}

// Compare decimal text without converting money to JavaScript floating-point numbers.
function differenceExceeds(difference?: string | null, tolerance?: string | null) {
  if (!difference || !tolerance || !/^-?\d+(?:\.\d+)?$/.test(difference) || !/^\d+(?:\.\d+)?$/.test(tolerance)) return false;
  const [left, leftFraction = ""] = difference.replace(/^-/, "").split(".");
  const [right, rightFraction = ""] = tolerance.split(".");
  const scale = Math.max(leftFraction.length, rightFraction.length);
  return BigInt(left + leftFraction.padEnd(scale, "0")) > BigInt(right + rightFraction.padEnd(scale, "0"));
}

export function evidence(run: ReportRun, fieldId?: string | null) {
  const field = run.fields.find(value => value.id === fieldId);
  return field ? `${roleName(field.role)}${field.page == null ? "" : `, page ${field.page}`}: ${field.quote ?? "Supporting text not recorded"}` : "Supporting text not recorded";
}

export function checkLabel(check: ReportCheck) {
  return `${checkTitle(check)}${check.occurrence != null && check.occurrence > 0 ? `, item row ${check.occurrence}` : ""}${check.lineIdentity ? `: ${check.lineIdentity.replace(/^[CD]:/, "")}` : ""}`;
}

export const isPairReport = (run: ReportRun) => /^invoice-po-[12]\.0$/.test(run.ruleSetVersion);

/** The pair engine stores Purchase order on the left and Invoice on the right.
 * Its signed difference remains Invoice minus Purchase order. */
export function checkValues(run: ReportRun, check: ReportCheck) {
  return isPairReport(run) ? [check.rightValue, check.leftValue] : [check.leftValue, check.rightValue];
}

export const checkHeaders = (run: ReportRun) => isPairReport(run) ? ["Invoice", "Purchase order"] : ["First saved value", "Second saved value"];

export function findingLabel(run: ReportRun, finding: ReportFinding) {
  const check = run.checks.find(value => value.findingId === finding.id);
  return check ? checkLabel(check) : `${label(finding.fieldKey ?? finding.ruleCode)}${finding.occurrence != null && finding.occurrence > 0 ? `, item row ${finding.occurrence}` : ""}`;
}

export function findingValue(finding: ReportFinding, role: string) {
  return finding.leftRole === role ? finding.leftValue : finding.rightRole === role ? finding.rightValue : null;
}

export function fieldState(field: ReportField) {
  return ({ Confirmed: "Confirmed", Corrected: "Corrected", Added: "Added by reviewer", Unchecked: "Not checked", Cleared: "Cleared from review" }[field.sourceKind ?? ""] ?? "Review state not recorded");
}

export function effectiveFieldValue(field: ReportField) {
  return ["Confirmed", "Corrected", "Added"].includes(field.sourceKind ?? "") ? field.value : null;
}

export function reportMetadata(report: ReviewReport): [string, string][] {
  return [["Case reference", report.reference], ["Case name at export", report.name], ["Comparison", String(report.run.runNumber)],
    ["Snapshot ID", report.snapshotId], ["Case and decision version", report.caseVersion], ["Prepared at (UTC)", report.capturedAtUtc],
    ["Comparison ID", report.run.id], ["Rule version", report.run.ruleSetVersion],
    ["Saved coverage", report.run.coverage?.status ?? "Not recorded"], ["Saved conclusion", report.run.coverage?.conclusion ?? "Not recorded"],
    ["Compared at (UTC)", report.run.evaluatedAt ?? "Not recorded"], ["Compared by (actor ID)", report.run.evaluationActorId ?? "Not recorded"],
    ["Scope", "Only the saved documents. This does not approve payment or establish what remains on the order."],
    ["Decision cutoff", "Only the decision sequences in this snapshot. Later decisions and document edits are not included."]];
}

export function reportFilename(report: ReviewReport, extension: string) {
  const reference = report.reference.replace(/[^A-Za-z0-9_-]/g, "-").slice(0, 70) || "saved-check";
  return `invoice-check_${reference}_comparison-${report.run.runNumber}_${report.snapshotId.slice(0, 8)}.${extension}`;
}
