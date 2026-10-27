import { expect, it } from "vitest";
import { csvCell, reportCsv } from "./report-csv";
import { reportHtml } from "./report-html";
import { reportFilename, reportSummary } from "./report-content";
import { reportFixture } from "./report-fixture";

it("preserves exact decimals, Arabic, evidence and all decisions in both formats", () => {
  const report = reportFixture();
  for (const output of [reportHtml(report), reportCsv(report)]) {
    for (const text of ["999999999999999999.123456", "الإجمالي", "review-1", "extraction-1", report.snapshotId, report.caseVersion, "Agreed change", "Needs another check", "actor-2", "Price basis missing", "Could not be checked"]) expect(output).toContain(text);
  }
  expect(reportCsv(report).startsWith("\uFEFF")).toBe(true);
  expect(reportCsv(report)).toContain('"Invoice, page 1: الإجمالي 999999999999999999.123456\nAED"');
  expect(reportFilename(report, "html")).toBe("invoice-check_REC-2026-000001_comparison-2_aaaaaaaa.html");
});

it.each(["=SUM(A1)", "+cmd", "-1+2", "@function", "  =formula", "\u0000\t=cmd", "\r=cmd", "\ntext", "\ttext"])("neutralizes formula/control starts: %j", input => {
  expect(csvCell(input)).toBe(`"'${input}"`);
});

it("quotes commas, quotes and multiline cells without deleting source content", () => {
  expect(csvCell('شركة, "One"\nTwo')).toBe('"شركة, ""One""\nTwo"');
});

it("renders hostile saved text only as text with no active scripts or remote resources", () => {
  const report = reportFixture(); report.name = '<img src=x onerror="alert(1)">';
  report.run.sources[0].role = "</h3><script>alert(1)</script>";
  report.run.findings[0].resolutions[0].reason = "<svg onload=alert(1)>";
  const document = new DOMParser().parseFromString(reportHtml(report), "text/html");
  expect(document.querySelectorAll("script,img,svg,iframe,link,form")).toHaveLength(0);
  expect(document.body.textContent).toContain(report.name);
  expect(document.querySelector('meta[http-equiv="Content-Security-Policy"]')).toBeTruthy();
});

it("keeps incomplete and legacy coverage honest regardless of recorded decisions", () => {
  const report = reportFixture();
  expect(reportSummary(report.run)).toMatchObject({ outcome: "Differences found", passed: 0, differed: 1, incomplete: 1, total: 2 });
  report.run.checks = [report.run.checks[1]];
  expect(reportSummary(report.run).outcome).toBe("Differences found");
  report.run.findings = [report.run.findings[1]];
  expect(reportSummary(report.run).outcome).toBe("Checks incomplete");
  report.run.checks = []; report.run.ruleSetVersion = "1.0";
  expect(reportHtml(report)).toContain("Full check counts were not recorded. No complete match can be claimed.");
});

it("recognizes older saved differences without rounding large decimal values", () => {
  const report = reportFixture(); report.run.checks = []; report.run.ruleSetVersion = "1.0";
  report.run.findings[0].difference = "9007199254740992.000001";
  report.run.findings[0].tolerance = "9007199254740992.000000";
  expect(reportSummary(report.run).outcome).toBe("Differences found");
  report.run.findings[0].difference = "-9007199254740992.000000";
  expect(reportSummary(report.run).outcome).toBe("Checks incomplete");
});
