import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { ReportDownload } from "./report-download";
import { reportFixture } from "./report-fixture";
import { loadReport } from "./report-api";

const identity = vi.hoisted(() => ({ actorId: "person-1" }));
vi.mock("../../security/enterprise-provider", () => ({ useEnterprise: () => ({ session: { organizationId: "org-1", actorId: identity.actorId, tenantId: "tenant-1", role: "Staff" } }) }));
let fetcher: ReturnType<typeof vi.fn>, files: Blob[];
const props = { caseId: "case-1", runId: "run-1", version: "version-1" };
beforeEach(() => {
  identity.actorId = "person-1"; files = [];
  fetcher = vi.fn(async () => Response.json(reportFixture())); vi.stubGlobal("fetch", fetcher);
  vi.stubGlobal("URL", Object.assign(URL, { createObjectURL: vi.fn((blob: Blob) => { files.push(blob); return "blob:sample"; }), revokeObjectURL: vi.fn() }));
  vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
});
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it("reauthorizes both downloads and uses one snapshot for HTML and CSV", async () => {
  render(<ReportDownload {...props}/>);
  fireEvent.click(screen.getByRole("button", { name: "Download results" }));
  await screen.findByRole("status");
  fetcher.mockResolvedValueOnce(Response.json({ ...reportFixture(), snapshotId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" }));
  fireEvent.change(screen.getByLabelText("File type"), { target: { value: "csv" } });
  fireEvent.click(screen.getByRole("button", { name: "Download results" }));
  await waitFor(() => expect(files).toHaveLength(2));
  const read = (blob: Blob) => new Promise<string>(resolve => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result)); reader.readAsText(blob); });
  for (const file of files) { const text = await read(file); expect(text).toContain(reportFixture().snapshotId); expect(text).not.toContain("bbbbbbbb-bbbb"); }
  expect(fetcher).toHaveBeenCalledTimes(2);
  expect(fetcher.mock.calls[0][1].headers["If-Match"]).toBe('"version-1"');
});

it.each([401, 403, 412, 500])("does not download or replay after HTTP %i", async status => {
  fetcher.mockResolvedValueOnce(new Response("private details", { status }));
  render(<ReportDownload {...props}/>); fireEvent.click(screen.getByRole("button", { name: "Download results" }));
  expect((await screen.findByRole("alert")).textContent).not.toContain("private details");
  expect(files).toHaveLength(0); expect(fetcher).toHaveBeenCalledTimes(1);
});

it("drops a late response and cached data when the account changes", async () => {
  let resolve!: (value: Response) => void;
  fetcher.mockImplementationOnce(() => new Promise<Response>(done => { resolve = done; }));
  const view = render(<ReportDownload {...props}/>); fireEvent.click(screen.getByRole("button", { name: "Download results" }));
  const signal = fetcher.mock.calls[0][1].signal as AbortSignal;
  identity.actorId = "person-2"; view.rerender(<ReportDownload {...props}/>);
  expect(signal.aborted).toBe(true);
  await act(async () => resolve(Response.json(reportFixture())));
  expect(files).toHaveLength(0); expect(screen.queryByRole("status")).toBeNull();
});

it("drops a late response when the selected run or case version changes", async () => {
  let resolve!: (value: Response) => void;
  fetcher.mockImplementationOnce(() => new Promise<Response>(done => { resolve = done; }));
  const view = render(<ReportDownload {...props}/>); fireEvent.click(screen.getByRole("button", { name: "Download results" }));
  view.rerender(<ReportDownload {...props} version="version-2"/>);
  await act(async () => resolve(Response.json(reportFixture())));
  expect(files).toHaveLength(0);
});

it("clears a prepared snapshot on account change and blocks reuse after access revocation", async () => {
  const view = render(<ReportDownload {...props}/>);
  fireEvent.click(screen.getByRole("button", { name: "Download results" })); await screen.findByRole("status");
  identity.actorId = "person-2"; view.rerender(<ReportDownload {...props}/>);
  expect(screen.queryByRole("status")).toBeNull();
  expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:sample");
  fetcher.mockResolvedValueOnce(Response.json({ ...reportFixture(), snapshotId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" }));
  fireEvent.click(screen.getByRole("button", { name: "Download results" })); await screen.findByRole("status");
  fetcher.mockResolvedValueOnce(new Response("private", { status: 403 }));
  fireEvent.change(screen.getByLabelText("File type"), { target: { value: "csv" } });
  fireEvent.click(screen.getByRole("button", { name: "Download results" })); await screen.findByRole("alert");
  expect(screen.queryByRole("status")).toBeNull(); expect(files).toHaveLength(2);
});

it("rejects precision-losing numeric decimals and wrong snapshot identities", async () => {
  const report = reportFixture();
  fetcher.mockResolvedValueOnce(Response.json({ ...report, run: { ...report.run, tolerances: { quantity: 0 } } }));
  await expect(loadReport(props.caseId, props.runId, props.version, new AbortController().signal)).rejects.toThrow("incomplete");
  fetcher.mockResolvedValueOnce(Response.json({ ...report, caseId: "foreign" }));
  await expect(loadReport(props.caseId, props.runId, props.version, new AbortController().signal)).rejects.toThrow("incomplete");
});
