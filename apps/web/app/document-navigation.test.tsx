import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import Home from "./document-workspace";

const ids = { a: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", b: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb", c: "cccccccc-cccc-4ccc-8ccc-cccccccccccc" };
function documentItem(key: keyof typeof ids) { return { id: ids[key], originalFilename: `invoice-${key}.txt`, sizeBytes: 512,
  status: "Ready", documentType: "Invoice", language: "English", extractor: "mock", processingMs: 1,
  createdAt: "2026-09-30T08:00:00Z", fields: [] }; }
function workspace(key: keyof typeof ids) {
  return { documentId: ids[key], extractionRunId: `run-${key}`, selectedExtractionRunId: `run-${key}`,
    session: { id: `review-${key}`, revisionNumber: 1, status: "InProgress", concurrencyToken: `version-${key}` },
    fields: [{ fieldKey: "invoice_number", occurrenceIndex: 0, decisionStatus: "Confirmed", cues: [],
      machine: { id: `field-${key}`, value: `VALUE-${key}`, valueType: "String", confidence: 1, evidence: [{ page: 1 }] },
      effective: { isPresent: true, value: `VALUE-${key}`, valueType: "String", source: "Reviewer" } }] };
}
function json(value: unknown, status = 200) { return new Response(JSON.stringify(value), { status,
  headers: { "Content-Type": "application/json", ETag: '"version"' } }); }
function requests(overrides?: (url: URL, init?: RequestInit) => Promise<Response> | Response | undefined) {
  const fetcher = vi.fn((raw: string, init?: RequestInit) => {
    const url = new URL(raw); const override = overrides?.(url, init); if (override) return Promise.resolve(override);
    if (url.pathname === "/api/documents/work-list") return Promise.resolve(json([documentItem("a"), documentItem("b")]));
    for (const key of ["a", "b", "c"] as const) {
      if (url.pathname === `/api/documents/${ids[key]}/review`) return Promise.resolve(json(workspace(key)));
      if (url.pathname === `/api/documents/${ids[key]}`) return Promise.resolve(json(documentItem(key)));
    }
    throw new Error(`Unexpected request ${url}`);
  });
  vi.stubGlobal("fetch", fetcher); return fetcher;
}

describe("document deep links and browser history", () => {
  beforeEach(() => window.history.replaceState(null, "", "/"));
  afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); });

  it("keeps closed-check embedded documents read only", async () => {
    const fetcher = requests();
    render(<Home embedded readOnly documentId={ids.a}/>);
    await screen.findByRole("heading", { name: "invoice-a.txt" });
    await screen.findByText(/These extracted values are awaiting a reviewer/);
    expect(screen.queryByRole("button", { name: "Reprocess document" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Complete review" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Save correction" })).toBeNull();
    expect(fetcher.mock.calls.some(([, init]) => init?.method)).toBe(false);
  });

  it("does not focus a reused row number from an older extraction of the same document", async () => {
    requests();
    render(<Home embedded documentId={ids.a} focusField={{ key: "invoice_number", occurrence: 0, extractionRunId: "older-run" }}/>);
    await screen.findByText(/current item rows may be different/);
    expect(document.activeElement?.closest(".review-field")).toBeNull();
  });

  it("reopens the requested document instead of the first list item after remount", async () => {
    window.history.replaceState(null, "", `/?document=${ids.b}`);
    const fetcher = requests(); const first = render(<Home/>);
    await screen.findByDisplayValue("VALUE-b");
    expect(screen.getByRole("heading", { name: "invoice-b.txt" })).toBeTruthy();
    first.unmount(); render(<Home/>);
    await screen.findByDisplayValue("VALUE-b");
    expect(fetcher.mock.calls.filter(([url]) => url.endsWith(`/${ids.b}/review`))).toHaveLength(2);
    expect(fetcher.mock.calls.some(([url]) => url.endsWith(`/${ids.a}/review`))).toBe(false);
  });

  it("updates the URL on selection and restores the previous document through browser Back", async () => {
    window.history.replaceState(null, "", `/?document=${ids.a}&context=keep`);
    requests(); render(<Home/>);
    await screen.findByDisplayValue("VALUE-a");
    fireEvent.click(screen.getByRole("button", { name: /invoice-b\.txt/ }));
    await screen.findByDisplayValue("VALUE-b");
    expect(new URLSearchParams(location.search).get("document")).toBe(ids.b);
    expect(new URLSearchParams(location.search).get("context")).toBe("keep");
    act(() => window.history.back());
    await screen.findByDisplayValue("VALUE-a");
    expect(new URLSearchParams(location.search).get("document")).toBe(ids.a);
    expect(screen.getByRole("heading", { name: "invoice-a.txt" })).toBeTruthy();
  });

  it("retrieves an authorized document outside the first loaded list", async () => {
    window.history.replaceState(null, "", `/?document=${ids.c}`);
    const fetcher = requests(); render(<Home/>);
    await screen.findByDisplayValue("VALUE-c");
    expect(screen.getByRole("heading", { name: "invoice-c.txt" })).toBeTruthy();
    expect(fetcher.mock.calls.some(([url]) => new URL(url).pathname === `/api/documents/${ids.c}`)).toBe(true);
  });

  it.each(["invalid", "missing"])("keeps an %s document link visible without substituting another document", async kind => {
    const id = kind === "invalid" ? "not-a-document-id" : ids.c;
    window.history.replaceState(null, "", `/?document=${id}`);
    const fetcher = requests(url => url.pathname === `/api/documents/${ids.c}` ? json({}, 404) : undefined);
    render(<Home/>);
    await screen.findByText(kind === "invalid" ? "This document link is invalid." : "Document not found or access unavailable.");
    expect(screen.queryByRole("heading", { name: "invoice-a.txt" })).toBeNull();
    expect(screen.queryByLabelText("Reviewed value")).toBeNull();
    expect(new URLSearchParams(location.search).get("document")).toBe(id);
    expect(fetcher.mock.calls.some(([url]) => url.endsWith(`/${ids.a}/review`))).toBe(false);
    if (kind === "invalid") expect(fetcher.mock.calls.some(([url]) => url.includes("not-a-document-id"))).toBe(false);
  });

  it("ignores the prior deep-link response after a popstate selects another document", async () => {
    let resolve!: (value: Response) => void;
    const delayed = new Promise<Response>(done => { resolve = done; });
    window.history.replaceState(null, "", `/?document=${ids.a}`);
    const fetcher = requests(url => url.pathname === `/api/documents/${ids.a}/review` ? delayed : undefined);
    render(<Home/>);
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => url.endsWith(`/${ids.a}/review`))).toBe(true));
    act(() => { window.history.replaceState(null, "", `/?document=${ids.b}`); window.dispatchEvent(new PopStateEvent("popstate")); });
    await screen.findByDisplayValue("VALUE-b");
    await act(async () => { resolve(json(workspace("a"))); });
    expect((screen.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("VALUE-b");
    expect(screen.getByRole("heading", { name: "invoice-b.txt" })).toBeTruthy();
    expect(new URLSearchParams(location.search).get("document")).toBe(ids.b);
  });

  it("updates the bookmark when upload selects a newly created document", async () => {
    window.history.replaceState(null, "", `/?document=${ids.a}`);
    let uploaded = false;
    requests((url, init) => {
      if (url.pathname === "/api/documents" && init?.method === "POST") { uploaded = true; return json(documentItem("c"), 201); }
      if (url.pathname === "/api/documents/work-list") return json(uploaded ? [documentItem("c"), documentItem("a"), documentItem("b")] : [documentItem("a"), documentItem("b")]);
    });
    const view = render(<Home/>); await screen.findByDisplayValue("VALUE-a");
    const input = view.container.querySelector<HTMLInputElement>('input[type="file"]')!;
    fireEvent.change(input, { target: { files: [new File(["invoice"], "invoice-c.txt", { type: "text/plain" })] } });
    fireEvent.submit(input.closest("form")!);
    await screen.findByDisplayValue("VALUE-c");
    expect(new URLSearchParams(location.search).get("document")).toBe(ids.c);
  });

  it("keeps a deep-linked document outside the first list selected when processing refreshes", async () => {
    window.history.replaceState(null, "", `/?document=${ids.c}`);
    const fetcher = requests(url => url.pathname.endsWith("/reprocess") ? json({ jobId: "job-c", extractionRunId: "run-c", status: "Ready", attemptCount: 1 }) : undefined);
    render(<Home/>); await screen.findByDisplayValue("VALUE-c");
    fireEvent.click(screen.getByRole("button", { name: "Reprocess document" }));
    await screen.findByText("Reprocessing queued. Existing reviewable output stays visible until replacement succeeds.");
    expect((screen.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("VALUE-c");
    expect(new URLSearchParams(location.search).get("document")).toBe(ids.c);
    expect(fetcher.mock.calls.some(([url]) => url.endsWith(`/${ids.a}/review`))).toBe(false);
  });
});
