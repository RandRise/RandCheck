import { checkLabel, checkStatus, decisionName, effectiveFieldValue, evidence, fieldState, findingLabel, findingValue, isPairReport, label, reportMetadata, reportSummary, roleName } from "./report-content";
import type { ReviewReport } from "./report.types";

/** Quote every cell and neutralize formula starts even after whitespace/control characters.
 * The protective apostrophe is intentional; exact original values remain in the HTML report. */
export function csvCell(value: unknown) {
  let text = value == null ? "" : String(value);
  if (/^[\s\u0000-\u001f\u007f-\u009f]*[=+@-]/u.test(text) || /^[\t\r\n]/.test(text)) text = `'${text}`;
  return `"${text.replaceAll('"', '""')}"`;
}

export function reportCsv(report: ReviewReport) {
  const run = report.run, summary = reportSummary(run);
  const rows: unknown[][] = [["Record type", "Label / ID", "Status / action", "Effective value / reason", "Invoice value", "Purchase order value", "Difference", "Allowed difference", "Unit", "Currency", "Basis", "Invoice evidence", "Purchase order evidence", "Decision sequence", "Actor ID", "Time (UTC)", "Document role", "Captured text (history)", "Supporting text"]];
  const add = (row: unknown[]) => rows.push([...row, ...Array(Math.max(0, 19 - row.length)).fill("")]);
  reportMetadata(report).forEach(([key, value]) => add(["Report", key, "", value]));
  add(["Outcome", summary.outcome, "", summary.recorded ? `${summary.passed} passed; ${summary.differed} differed; ${summary.incomplete} could not be checked; ${summary.total} total` : "Full check counts were not recorded. No complete match can be claimed."]);
  Object.entries(run.tolerances).forEach(([key, value]) => add(["Setting", label(key), "", value]));
  run.sources.forEach(source => {
    for (const [key, value] of Object.entries(source)) add(["Saved document", `${roleName(source.role)}: ${label(key)}`, "", value]);
  });
  run.checks.forEach(check => {
    add(["Check", `${checkLabel(check)} (${check.id}; finding ${check.findingId ?? "none"})`, checkStatus(check.status), check.explanation,
      isPairReport(run) ? check.rightValue : null, isPairReport(run) ? check.leftValue : null, check.signedDifference, check.tolerance, check.unit, check.currency, check.basis,
      isPairReport(run) ? evidence(run, check.rightFieldSnapshotId) : null, isPairReport(run) ? evidence(run, check.leftFieldSnapshotId) : null]);
    if (!isPairReport(run)) {
      add(["Check source", `${check.id}: first saved value`, "Role not recorded", check.leftValue]);
      add(["Check source", `${check.id}: second saved value`, "Role not recorded", check.rightValue]);
    }
  });
  run.fields.forEach(field => add(["Saved field", `${label(field.key)}${field.occurrence > 0 ? `, item row ${field.occurrence}` : ""} (${field.id})`, fieldState(field), effectiveFieldValue(field), "", "", "", "", "", "", "", "", "", "", "", "", roleName(field.role), field.value, evidence(run, field.id)]));
  run.findings.forEach(finding => {
    const name = `${findingLabel(run, finding)} (${finding.id})`;
    add(["Finding", name, finding.severity, finding.explanation, findingValue(finding, "Invoice"), findingValue(finding, "PurchaseOrder"), finding.difference, finding.tolerance]);
    for (const [role, captured] of [[finding.leftRole, finding.leftValue], [finding.rightRole, finding.rightValue]]) {
      if (captured != null && role !== "Invoice" && role !== "PurchaseOrder") add(["Finding source", finding.id, "Captured finding value", captured, "", "", "", "", "", "", "", "", "", "", "", "", roleName(role)]);
    }
    if (!finding.resolutions.length) add(["Decision", finding.id, "No decision recorded"]);
    [...finding.resolutions].sort((a, b) => a.sequenceNumber - b.sequenceNumber).forEach(decision => add(["Decision", name, decisionName(decision.action), decision.reason, "", "", "", "", "", "", "", "", "", decision.sequenceNumber, decision.actorId, decision.createdAt]));
  });
  return `\uFEFF${rows.map(row => row.map(csvCell).join(",")).join("\r\n")}\r\n`;
}
