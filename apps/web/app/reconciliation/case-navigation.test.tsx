import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import ReconciliationPage from "./page";

const ids = { a: "11111111-1111-4111-8111-111111111111", b: "22222222-2222-4222-8222-222222222222", c: "33333333-3333-4333-8333-333333333333" };
function caseItem(id: string, name: string) { return { id, name, status: "Draft", concurrencyToken: `version-${id}`, documents: [], runs: [] }; }
const a = caseItem(ids.a, "Office supplies case");
const b = caseItem(ids.b, "Consulting case");
const c = caseItem(ids.c, "New equipment case");
function summary(item: ReturnType<typeof caseItem>) { return { id: item.id, name: item.name, status: item.status,
  createdAt: "2026-09-30T08:00:00Z", updatedAt: "2026-09-30T09:00:00Z", documentCount: item.documents.length, runCount: item.runs.length }; }
function json(value: unknown, status = 200, etag = '"case-version"') { return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json", ETag: etag } }); }
function deferred() { let resolve!: (value: Response) => void; const promise = new Promise<Response>(done => { resolve = done; }); return { promise, resolve }; }
function navigate(query: string) { act(() => { window.history.replaceState(null, "", `/reconciliation${query}`); window.dispatchEvent(new PopStateEvent("popstate")); }); }
function requests(overrides?: (url: URL, init?: RequestInit) => Promise<Response> | Response | undefined) {
  const fetcher = vi.fn((raw: string, init?: RequestInit) => {
    const url = new URL(raw);
    const override = overrides?.(url, init); if (override) return Promise.resolve(override);
    if (url.pathname === "/api/documents") return Promise.resolve(json([]));
    if (url.pathname === "/api/reconciliation-cases" && (!init?.method || init.method === "GET")) return Promise.resolve(json({ cases: [summary(a), summary(b)], nextOffset: null }));
    if (url.pathname === `/api/reconciliation-cases/${ids.a}`) return Promise.resolve(json(a));
    if (url.pathname === `/api/reconciliation-cases/${ids.b}`) return Promise.resolve(json(b));
    throw new Error(`Unexpected request ${init?.method ?? "GET"} ${url}`);
  });
  vi.stubGlobal("fetch", fetcher); return fetcher;
}

describe("saved comparison case navigation", () => {
  beforeEach(() => { sessionStorage.clear(); window.history.replaceState(null, "", "/reconciliation"); });
  afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); });

  it.each([false, true])("does not create a new empty case through History with a mismatched request=%s", async mismatch => {
    const earlier = JSON.stringify({ fingerprint: JSON.stringify(["Earlier request", "Uncategorized"]), requestKey: ids.c });
    if (mismatch) sessionStorage.setItem("case-creation::", earlier);
    const fetcher = requests(); render(<ReconciliationPage/>);
    fireEvent.change(await screen.findByLabelText("Case name"), { target: { value: "Fresh empty check" } });
    fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByText(/No matching earlier request/);
    expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
    expect(sessionStorage.getItem("case-creation::")).toBe(mismatch ? earlier : null);
  });

  it("opens a case directly from its URL again after remount", async () => {
    window.history.replaceState(null, "", `/reconciliation?case=${ids.b}`);
    const fetcher = requests();
    const first = render(<ReconciliationPage/>);
    await screen.findByRole("heading", { name: b.name });
    expect(screen.queryByRole("heading", { name: a.name })).toBeNull();
    first.unmount(); render(<ReconciliationPage/>);
    await screen.findByRole("heading", { name: b.name });
    expect(fetcher.mock.calls.filter(([url]) => new URL(url).pathname === `/api/reconciliation-cases/${ids.b}`)).toHaveLength(2);
    expect(new URLSearchParams(location.search).get("case")).toBe(ids.b);
  });

  it("opens saved cases from the list and restores the previous case through browser Back", async () => {
    requests(); render(<ReconciliationPage/>);
    expect(screen.getByRole("list", { name: "How to read case history" })).toBeTruthy();
    await screen.findByRole("link", { name: a.name });
    expect(screen.getByText("Recover an earlier request").closest("details")?.open).toBe(false);
    expect(screen.getByRole("button", { name: "Recover request" }).closest("details"))
      .toBe(screen.getByText("Recover an earlier request").closest("details"));
    fireEvent.click(await screen.findByRole("link", { name: a.name }));
    await screen.findByRole("heading", { name: a.name });
    expect(new URLSearchParams(location.search).get("case")).toBe(ids.a);
    fireEvent.click(screen.getByRole("button", { name: "All cases" }));
    await screen.findByRole("link", { name: b.name });
    expect(new URLSearchParams(location.search).has("case")).toBe(false);
    fireEvent.click(screen.getByRole("link", { name: b.name }));
    await screen.findByRole("heading", { name: b.name });
    act(() => window.history.back());
    await waitFor(() => expect(new URLSearchParams(location.search).has("case")).toBe(false));
    await screen.findByRole("link", { name: a.name });
    act(() => window.history.back());
    await screen.findByRole("heading", { name: a.name });
  });

  it("recovers an earlier request with a stable URL and can reopen it from the saved list", async () => {
    sessionStorage.setItem("case-creation::", JSON.stringify({ fingerprint: JSON.stringify([c.name, "Uncategorized"]), requestKey: ids.c, workflowKind: "InvoicePurchaseOrder" }));
    let created = false;
    requests((url, init) => {
      if (url.pathname === "/api/reconciliation-cases") {
        if (init?.method === "POST") { created = true; return json(c, 201); }
        return json({ cases: created ? [summary(c), summary(a), summary(b)] : [summary(a), summary(b)], nextOffset: null });
      }
      if (url.pathname === `/api/reconciliation-cases/${ids.c}`) return json(c);
    });
    render(<ReconciliationPage/>);
    fireEvent.change(await screen.findByLabelText("Case name"), { target: { value: c.name } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByRole("heading", { name: c.name });
    expect(new URLSearchParams(location.search).get("case")).toBe(ids.c);
    fireEvent.click(screen.getByRole("button", { name: "All cases" }));
    fireEvent.click(await screen.findByRole("link", { name: c.name }));
    await screen.findByRole("heading", { name: c.name });
  });

  it("refreshes the saved list when browser Back leaves a recovered case", async () => {
    sessionStorage.setItem("case-creation::", JSON.stringify({ fingerprint: JSON.stringify([c.name, "Uncategorized"]), requestKey: ids.c, workflowKind: "InvoicePurchaseOrder" }));
    let created = false;
    requests((url, init) => {
      if (url.pathname === "/api/reconciliation-cases") {
        if (init?.method === "POST") { created = true; return json(c, 201); }
        return json({ cases: created ? [summary(c), summary(a)] : [summary(a)], nextOffset: null });
      }
    });
    render(<ReconciliationPage/>);
    await screen.findByRole("link", { name: a.name });
    fireEvent.change(screen.getByLabelText("Case name"), { target: { value: c.name } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByRole("heading", { name: c.name });
    act(() => window.history.back());
    await waitFor(() => expect(new URLSearchParams(location.search).has("case")).toBe(false));
    await screen.findByRole("link", { name: c.name });
  });

  it("does not replace the current case with an earlier delayed case response", async () => {
    const delayed = deferred();
    const fetcher = requests(url => url.pathname === `/api/reconciliation-cases/${ids.a}` ? delayed.promise : undefined);
    window.history.replaceState(null, "", `/reconciliation?case=${ids.a}`);
    render(<ReconciliationPage/>);
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => new URL(url).pathname === `/api/reconciliation-cases/${ids.a}`)).toBe(true));
    navigate(`?case=${ids.b}`);
    await screen.findByRole("heading", { name: b.name });
    await act(async () => { delayed.resolve(json(a)); });
    expect(screen.getByRole("heading", { name: b.name })).toBeTruthy();
    expect(screen.queryByRole("heading", { name: a.name })).toBeNull();
    expect(new URLSearchParams(location.search).get("case")).toBe(ids.b);
  });

  it("does not show an abandoned case evaluation after navigating to another case", async () => {
    const delayed = deferred();
    const linkedA = { ...a, documents: [{ id: "link-a", role: "Invoice", documentId: ids.c, reviewSessionId: ids.c }] };
    const fetcher = requests((url, init) => {
      if (url.pathname.endsWith("/evaluations") && init?.method === "POST") return delayed.promise;
      if (url.pathname === `/api/reconciliation-cases/${ids.a}`) return json(linkedA);
    });
    window.history.replaceState(null, "", `/reconciliation?case=${ids.a}`);
    render(<ReconciliationPage/>);
    fireEvent.click(await screen.findByRole("button", { name: "Run comparison" }));
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => url.endsWith("/evaluations"))).toBe(true));
    navigate(`?case=${ids.b}`);
    await screen.findByRole("heading", { name: b.name });
    await act(async () => { delayed.resolve(json({ ...linkedA, runs: [{ id: "run-a", runNumber: 1, status: "Completed", ruleSetVersion: "1", fields: [], findings: [] }] })); });
    expect(screen.getByRole("heading", { name: b.name })).toBeTruthy();
    expect(screen.queryByText("Exact match")).toBeNull();
    expect(screen.queryByText(/Evaluation 1 completed/)).toBeNull();
  });

  it.each(["invalid", "missing"])("does not silently select another case for an %s deep link", async kind => {
    const id = kind === "invalid" ? "not-a-case-id" : ids.c;
    const fetcher = requests(url => url.pathname === `/api/reconciliation-cases/${ids.c}` ? json({ detail: "Case not found or access unavailable." }, 404) : undefined);
    window.history.replaceState(null, "", `/reconciliation?case=${id}`);
    render(<ReconciliationPage/>);
    await screen.findByText(kind === "invalid" ? "This case link is invalid." : "Case not found or access unavailable.");
    expect(screen.queryByRole("heading", { name: a.name })).toBeNull();
    expect(screen.queryByLabelText("Case name")).toBeNull();
    expect(new URLSearchParams(location.search).get("case")).toBe(id);
    if (kind === "invalid") expect(fetcher.mock.calls.some(([url]) => url.includes("not-a-case-id"))).toBe(false);
    fireEvent.click(screen.getByRole("button", { name: "All cases" }));
    await screen.findByLabelText("Case name");
  });

  it("adds another bounded page on request", async () => {
    const fetcher = requests(url => url.pathname === "/api/reconciliation-cases" ? json(url.searchParams.get("offset") === "50"
      ? { cases: [summary(b)], nextOffset: null } : { cases: [summary(a)], nextOffset: 50 }) : undefined);
    render(<ReconciliationPage/>);
    await screen.findByRole("link", { name: a.name });
    expect(screen.queryByRole("link", { name: b.name })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Load more cases" }));
    await screen.findByRole("link", { name: b.name });
    expect(fetcher.mock.calls.some(([url]) => new URL(url).searchParams.get("offset") === "50")).toBe(true);
    expect(screen.getByRole("link", { name: a.name })).toBeTruthy();
    expect(screen.getByRole("link", { name: b.name })).toBeTruthy();
  });
});
