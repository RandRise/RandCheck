import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import DocumentLibrary from "./page";

const access = vi.hoisted(() => ({ operate: true }));
vi.mock("../security/enterprise-provider", () => ({
  useEnterprise: () => ({ session: { tenantId: "tenant", organizationId: "org", actorId: "actor", role: "Staff" } }),
  can: (_session: unknown, permission: string) => permission !== "Operate" || access.operate,
}));

const a = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const b = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const files = [
  { id: a, originalFilename: "invoice.txt", documentType: "invoice", status: "Ready", sizeBytes: 512, createdAt: "2026-10-20T09:00:00Z" },
  { id: b, originalFilename: "quotation.txt", documentType: "quotation", status: "Ready", sizeBytes: 512, createdAt: "2026-10-20T09:00:00Z" },
];
function review(id = a) {
  return { documentId: id, extractionRunId: "run", selectedExtractionRunId: "run",
    session: { id: `review-${id}`, status: id === b ? "Completed" : "InProgress", revisionNumber: 1, concurrencyToken: "version" },
    fields: [{ fieldKey: "supplier_name", occurrenceIndex: 0, decisionStatus: id === b ? "Confirmed" : "Unreviewed", cues: [],
      effective: { isPresent: true, value: id === a ? "المورد 123" : "Earlier supplier", source: id === a ? "MachinePending" : "Reviewer" },
      machine: { value: "المورد 123", valueType: "String", confidence: .9, evidence: [{ page: 1, quotedText: "المورد 123 AED" }] } }] };
}
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json", ETag: '"version"' } });
function requests(override?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  const fetcher = vi.fn(async (raw: string | URL | Request, init?: RequestInit) => {
    const url = new URL(String(raw));
    const custom = override?.(url, init);
    if (url.pathname === "/api/documents/library") {
      const response = custom ? await custom : json(files);
      if (!response.ok) return response;
      const data = await response.json();
      if (!Array.isArray(data)) return json(data);
      const search = (url.searchParams.get("q") ?? "").toLowerCase();
      const filtered = data.filter(file => (url.searchParams.get("includeQuotations") === "true" || file.documentType !== "quotation") && file.originalFilename.toLowerCase().includes(search));
      return json({ documents: filtered, total: filtered.length, offset: 0, pageSize: 10 });
    }
    if (custom) return custom;
    for (const file of files) {
      if (url.pathname === `/api/documents/${file.id}/review`) return json(review(file.id));
      if (url.pathname === `/api/documents/${file.id}`) return json(file);
      if (url.pathname === `/api/documents/${file.id}/review-history`) return json({ historical: true });
    }
    throw new Error(`Unexpected request ${url.pathname}`);
  });
  vi.stubGlobal("fetch", fetcher);
  return fetcher;
}
function expectNoWrites(fetcher: ReturnType<typeof requests>) {
  expect(fetcher.mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
}
async function openFile(name = "invoice.txt") {
  if (name === "quotation.txt") {
    const toggle = await screen.findByRole("checkbox", { name: "Show earlier quotations" });
    if (!(toggle as HTMLInputElement).checked) fireEvent.click(toggle);
  }
  fireEvent.click(await screen.findByRole("button", { name: new RegExp(name.replace(".", "\\.")) }));
  await screen.findByRole("region", { name: "Saved file values" });
}

describe("Document library", () => {
  beforeEach(() => { access.operate = true; window.history.replaceState(null, "", "/documents"); });
  afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it("starts with one searchable list and no duplicate upload, review or page links", async () => {
    const fetcher = requests();
    const view = render(<DocumentLibrary />);
    await screen.findByRole("button", { name: /invoice.txt/ });
    expect(screen.getByRole("heading", { name: "Choose a document" })).toBeTruthy();
    expect(fetcher).toHaveBeenCalledTimes(1);
    expect(view.container.querySelector('input[type="file"]')).toBeNull();
    expect(screen.queryAllByRole("link")).toHaveLength(0);
    expect(screen.queryByRole("button", { name: /quotation.txt/ })).toBeNull();
    expect(screen.getByText("Showing 1–1 of 1")).toBeTruthy();
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "quotation" } });
    fireEvent.click(screen.getByRole("button", { name: "Search" }));
    await waitFor(() => expect(screen.queryByRole("button", { name: /invoice.txt/ })).toBeNull());
    expect(await screen.findByText(/No documents match this search/)).toBeTruthy();
    await openFile("quotation.txt");
    expect(screen.getByRole("heading", { name: "quotation.txt" })).toBeTruthy();
    expect(screen.queryByRole("link", { name: /Questions|invoice check/ })).toBeNull();
    expectNoWrites(fetcher);
  });

  it("reads unchecked values and evidence without offering edit or confirm actions", async () => {
    const fetcher = requests();
    render(<DocumentLibrary />);
    await openFile();
    expect(screen.getByText("Saved values and evidence").closest("details")?.open).toBe(true);
    expect(screen.getByText("Not checked")).toBeTruthy();
    expect(screen.getByText((_text, element) => element?.tagName === "P" && element.textContent === "المورد 123").getAttribute("dir")).toBe("auto");
    fireEvent.click(screen.getByText("Source evidence"));
    expect(within(screen.getByRole("region", { name: "Saved file values" })).getByText("Page 1")).toBeTruthy();
    for (const name of ["Start review", "Complete review", "Confirm AI value", "Save correction", "Next field needing review"])
      expect(screen.queryByRole("button", { name })).toBeNull();
    expect(screen.getByRole("button", { name: "Download original" })).toBeTruthy();
    expectNoWrites(fetcher);
  });

  it("retains quotation history as a read-only download without leaving the library", async () => {
    const fetcher = requests();
    vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:history");
    vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    render(<DocumentLibrary />);
    await openFile("quotation.txt");
    fireEvent.click(screen.getByText("Review history"));
    fireEvent.click(screen.getByRole("button", { name: "Download review history" }));
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => String(url).endsWith(`${b}/review-history`))).toBe(true));
    await screen.findByText(/History is ready/);
    expect(screen.getByRole("link", { name: "Save history JSON" }).getAttribute("download")).toBe(`review-history-${b}.json`);
    expect(location.pathname).toBe("/documents");
    expectNoWrites(fetcher);
  });

  it("restores saved file links and browser Back without selecting a substitute", async () => {
    window.history.replaceState(null, "", `/documents?document=${b}`);
    const fetcher = requests();
    render(<DocumentLibrary />);
    await screen.findByText("Review completed");
    expect(screen.queryByRole("button", { name: /quotation.txt/ })).toBeNull();
    expect(screen.getByText(/This earlier quotation is open for reading/)).toBeTruthy();
    await openFile();
    expect(new URLSearchParams(location.search).get("document")).toBe(a);
    act(() => window.history.back());
    await screen.findByRole("heading", { name: "quotation.txt" });
    expectNoWrites(fetcher);
  });

  it.each(["invalid", "missing"])("keeps an %s bookmark error visible", async kind => {
    window.history.replaceState(null, "", `/documents?document=${kind === "invalid" ? "bad-id" : a}`);
    const fetcher = requests(url => url.pathname === "/api/documents/library" ? json([]) : json({}, 404));
    render(<DocumentLibrary />);
    await screen.findByText(kind === "invalid" ? "This document link is invalid." : "Document not found or access unavailable.");
    expect(screen.queryByRole("region", { name: "Saved file values" })).toBeNull();
    expectNoWrites(fetcher);
  });

  it("ignores a late response for a file the user has left", async () => {
    let resolve!: (response: Response) => void;
    const delayed = new Promise<Response>(done => { resolve = done; });
    const fetcher = requests(url => url.pathname === `/api/documents/${a}/review` ? delayed : undefined);
    render(<DocumentLibrary />);
    fireEvent.click(await screen.findByRole("button", { name: /invoice.txt/ }));
    await openFile("quotation.txt");
    await act(async () => resolve(json(review(a))));
    expect(screen.getByText("Review completed")).toBeTruthy();
    expect(screen.queryByText("Review in progress")).toBeNull();
    expectNoWrites(fetcher);
  });

  it("explains an empty list and a search with no matches", async () => {
    requests();
    render(<DocumentLibrary />);
    await screen.findByRole("button", { name: /invoice.txt/ });
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "not-a-file" } });
    fireEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(await screen.findByText(/No documents match this search/)).toBeTruthy();
    cleanup();
    requests(() => json([]));
    render(<DocumentLibrary />);
    await screen.findByText(/Files added through Home will appear here/);
  });

  it("hides a selected quotation from the list without closing its saved values or history", async () => {
    const fetcher = requests();
    render(<DocumentLibrary />);
    await openFile("quotation.txt");
    fireEvent.click(screen.getByRole("checkbox", { name: "Show earlier quotations" }));
    await screen.findByText("Showing 1–1 of 1");
    expect(screen.queryByRole("button", { name: /quotation.txt/ })).toBeNull();
    expect(screen.getByRole("heading", { name: "quotation.txt" })).toBeTruthy();
    expect(screen.getByText("Review completed")).toBeTruthy();
    expect(screen.getByText("Review history")).toBeTruthy();
    expect(new URLSearchParams(location.search).get("document")).toBe(b);
    expectNoWrites(fetcher);
  });

  it("keeps purchase orders and unknown types visible and explains a quotation-only library", async () => {
    requests(url => url.pathname.endsWith("library") ? json([
      { ...files[0], documentType: "purchase_order" }, { ...files[1], documentType: null },
    ]) : undefined);
    render(<DocumentLibrary />);
    await screen.findByRole("button", { name: /invoice.txt/ });
    expect(screen.getByRole("button", { name: /quotation.txt/ })).toBeTruthy();
    expect(screen.getByText("Showing 1–2 of 2")).toBeTruthy();
    cleanup();
    requests(url => url.pathname.endsWith("library") ? json([files[1]]) : undefined);
    render(<DocumentLibrary />);
    await screen.findByText(/No documents match this search/);
    expect(screen.getByText("No matching files")).toBeTruthy();
    expect(screen.queryByText(/No documents yet/)).toBeNull();
    fireEvent.click(screen.getByRole("checkbox", { name: "Show earlier quotations" }));
    expect(await screen.findByRole("button", { name: /quotation.txt/ })).toBeTruthy();
  });

  it("offers a failed review load retry without creating a review", async () => {
    let attempt = 0;
    const fetcher = requests(url => url.pathname === `/api/documents/${a}/review` && attempt++ === 0 ? json({}, 503) : undefined);
    render(<DocumentLibrary />);
    fireEvent.click(await screen.findByRole("button", { name: /invoice.txt/ }));
    fireEvent.click(await screen.findByRole("button", { name: "Retry review load" }));
    await screen.findByRole("region", { name: "Saved file values" });
    expectNoWrites(fetcher);
  });

  it("keeps processing recovery behind its disclosure and respects permissions", async () => {
    const fetcher = requests();
    const view = render(<DocumentLibrary />);
    await openFile();
    expect(screen.getByText("Processing details").closest("details")?.open).toBe(false);
    fireEvent.click(screen.getByText("Processing details"));
    expect(screen.getByRole("button", { name: "Reprocess document" })).toBeTruthy();
    access.operate = false;
    view.rerender(<DocumentLibrary />);
    expect(screen.queryByRole("button", { name: "Reprocess document" })).toBeNull();
    expectNoWrites(fetcher);
  });

  it("distinguishes excluded, missing and saved values without inventing evidence", async () => {
    const saved = { ...review(), selectedExtractionRunId: "newer-run", fields: [
      { fieldKey: "supplier_name", occurrenceIndex: 0, decisionStatus: "Cleared", cues: [],
        effective: { isPresent: false, source: "Reviewer" } },
      { fieldKey: "invoice_number", occurrenceIndex: 0, decisionStatus: "Missing", cues: [],
        effective: { isPresent: false, source: "Missing" } },
      ...(["Added", "Corrected"] as const).map((status, index) => ({
        fieldKey: index === 0 ? "currency" : "total", occurrenceIndex: 0, decisionStatus: status, cues: [],
        effective: { isPresent: true, value: index === 0 ? "AED" : "100", source: "Reviewer" },
        latestAction: { id: status, action: status, reason: `${status} from the original file`, sequenceNumber: 1 },
      })),
    ] };
    const fetcher = requests(url => url.pathname.endsWith("/review") ? json(saved) : undefined);
    render(<DocumentLibrary />);
    await openFile();
    expect(screen.getByText(/These values belong to an earlier extraction/)).toBeTruthy();
    expect(screen.getByText("Saved values and evidence").closest("details")?.open).toBe(true);
    for (const value of ["Excluded", "Not extracted", "AED", "100", "Added", "Corrected"])
      expect(screen.getByText(value)).toBeTruthy();
    for (const reason of ["Added from the original file", "Corrected from the original file"])
      expect(screen.getByText(reason)).toBeTruthy();
    for (const summary of screen.getAllByText("Source evidence")) fireEvent.click(summary);
    expect(screen.getAllByText("No source evidence is saved for this value.")).toHaveLength(4);
    expect(screen.queryByText(/Extracted text:/)).toBeNull();
    expectNoWrites(fetcher);
  });

  it("refreshes the paged list and current document after processing recovery without loading every file", async () => {
    const fetcher = requests((url, init) => url.pathname.endsWith("/reprocess") && init?.method === "POST"
      ? json({ jobId: "old-job", extractionRunId: "old-run", status: "Ready", replayed: true })
      : url.pathname.endsWith("/processing") ? json({ jobId: "old-job", extractionRunId: "old-run", status: "Ready", attemptCount: 1 }) : undefined);
    render(<DocumentLibrary />);
    await openFile();
    fireEvent.click(screen.getByText("Processing details"));
    fireEvent.click(screen.getByRole("button", { name: "Reprocess document" }));
    await screen.findByText(/Existing processing request found/);
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => new URL(String(url)).pathname === `/api/documents/${a}`)).toBe(true));
    await screen.findByRole("button", { name: /invoice.txt/ });
    expect(fetcher.mock.calls.some(([url]) => String(url).includes("work-list"))).toBe(false);
    expect(fetcher.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(1);
    expect(screen.getByRole("region", { name: "Saved file values" })).toBeTruthy();
  });
});
