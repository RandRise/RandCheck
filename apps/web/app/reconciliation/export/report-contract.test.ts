import { expect, it } from "vitest";
import captured from "./fixtures/real-pair-report.json";
import { reportCsv } from "./report-csv";
import { reportHtml } from "./report-html";
import { reportFixture } from "./report-fixture";
import type { ReviewReport } from "./report.types";

const documentFor = (report: ReviewReport) => new DOMParser().parseFromString(reportHtml(report), "text/html");
const cells = (row: Element) => [...row.querySelectorAll("td")].map(cell => cell.textContent);

it("keeps the real invoice-po-2.0 serialized side contract: invoice1261, PO1260", () => {
  const report = captured as unknown as ReviewReport;
  const check = report.run.checks.find(check => check.fieldKey === "total")!;
  expect(check.leftValue).toBe("1260.00"); expect(check.rightValue).toBe("1261.00");
  const row = [...documentFor(report).querySelectorAll("table:first-of-type tbody tr")].find(row => cells(row)[0] === "Document total")!;
  expect(cells(row).slice(1, 4)).toEqual(["Differed", "1261.00", "1260.00"]);
  const csv = reportCsv(report).split("\r\n").find(row => row.startsWith('"Check","Document total'))!;
  expect(csv).toContain(`,"1261.00","1260.00","${check.signedDifference}"`);
  expect(csv).toContain('"Invoice, page 1: 1261.00","Purchase order, page 1: 1260.00"');
});

it("keeps unchecked, cleared and unknown captured text historical, never effective", () => {
  const report = reportFixture();
  report.run.fields = ["Confirmed", "Corrected", "Added", "Unchecked", "Cleared", null].map((sourceKind, i) => ({
    id: `field-${i}`, key: "total", occurrence: 0, role: "Invoice", value: `10${i}`, sourceKind, quote: `captured ${i}`,
  }));
  const table = [...documentFor(report).querySelectorAll("table")].find(table => table.textContent?.includes("Effective reviewed value"))!;
  const rows = [...table.querySelectorAll("tbody tr")].map(cells);
  expect(rows.map(row => row[3])).toEqual(["Confirmed", "Corrected", "Added by reviewer", "Not checked", "Cleared from review", "Review state not recorded"]);
  expect(rows.map(row => row[4])).toEqual(["100", "101", "102", "No confirmed value", "No confirmed value", "No confirmed value"]);
  expect(rows.map(row => row[5])).toEqual(["100", "101", "102", "103", "104", "105"]);
  const csvRows = reportCsv(report).split("\r\n").filter(row => row.startsWith('"Saved field"'));
  expect(csvRows[4]).toContain('"Cleared from review",""');
  expect(csvRows[4]).toContain('"Invoice","104","Invoice: captured 4"');
  expect(csvRows.every(row => !row.includes("item row"))).toBe(true);
});

it("labels legacy quotation/PO finding values and field evidence by their actual roles", () => {
  const report = reportFixture(); report.run.ruleSetVersion = "1.0"; report.run.checks = [];
  report.run.findings = [{ ...report.run.findings[0], leftRole: "Quotation", rightRole: "PurchaseOrder", leftValue: "90", rightValue: "100" }];
  report.run.fields = [{ ...report.run.fields[0], role: "PurchaseOrder", quote: "Order text" }];
  const csv = reportCsv(report).split("\r\n");
  const finding = csv.find(row => row.startsWith('"Finding",'))!;
  expect(finding).toContain('"Amounts differ","","100"');
  const quoted = csv.find(row => row.startsWith('"Finding source",'))!;
  expect(quoted).toContain('"Captured finding value","90"'); expect(quoted).toContain('"Quotation"');
  const field = csv.find(row => row.startsWith('"Saved field",'))!;
  expect(field).toContain('"Purchase order","999999999999999999.123456","Purchase order, page 1: Order text"');
  const document = documentFor(report);
  expect([...document.querySelectorAll("th")].map(value => value.textContent)).toContain("Quotation");
});

it("preserves one-based item rows and connects repeated line findings to their decisions", () => {
  const report = reportFixture();
  report.run.checks = [1, 2].map(row => ({ ...report.run.checks[0], id: `check-${row}`, fieldKey: "line_total", occurrence: row, lineIdentity: `C:ITEM-${row}`, findingId: `finding-${row}` }));
  report.run.findings = [1, 2].map(row => ({ ...report.run.findings[0], id: `finding-${row}`, fieldKey: "line_total", occurrence: row }));
  const document = documentFor(report), titles = [...document.querySelectorAll("h3")].map(value => value.textContent);
  expect(titles.filter(title => title === "Item total, item row 1: ITEM-1")).toHaveLength(2);
  expect(titles.filter(title => title === "Item total, item row 2: ITEM-2")).toHaveLength(2);
  expect(document.body.textContent).toContain("Check ID: check-1 · Finding ID: finding-1");
  expect(document.body.textContent).not.toContain("item row 3");
  expect(reportCsv(report)).toContain('"Decision","Item total, item row 1: ITEM-1 (finding-1)"');
});
