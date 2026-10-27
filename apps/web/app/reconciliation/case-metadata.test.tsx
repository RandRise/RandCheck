import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { EnterpriseProvider, type Session } from "../security/enterprise-provider";
import ReconciliationPage from "./page";

vi.mock("next/navigation", () => ({ usePathname: () => "/reconciliation", useRouter: () => ({ replace: vi.fn() }) }));
const id = (n: number) => `00000000-0000-4000-8000-${n.toString().padStart(12, "0")}`;
const config = { mode: "Entra", tenantId: id(101), clientId: id(102),
  scope: `api://${id(103)}/DocumentIntelligence.Access`, api: "http://localhost:8080" };
const original = { id: id(1), name: "Original equipment case", reference: "REC-2026-000042", category: "Equipment", status: "Draft",
  concurrencyToken: id(20), documents: [], runs: [{ id: id(30), runNumber: 1, status: "Completed", ruleSetVersion: "saved-rules", fields: [], findings: [] }] };
const other = { ...original, id: id(2), name: "Other case", reference: "REC-2026-000043", category: "Services", runs: [] };
const summary = (item = original) => ({ ...item, createdAt: "2026-09-30T08:00:00Z", updatedAt: "2026-09-30T08:00:00Z", documentCount: 0, runCount: item.runs.length });
function json(value: unknown, status = 200, etag = '"version-1"') { return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json", ETag: etag } }); }
function setup(role: string = "Staff", admin = false,
  override?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  const member = { organizationId: id(104), organizationName: "Example organization", actorId: id(105), displayName: "Test user",
    tenantId: config.tenantId, role };
  const fetcher = vi.fn((raw: string | URL | Request, init?: RequestInit) => {
    const url = new URL(String(raw));
    if (url.pathname.endsWith("/session")) return Promise.resolve(json(member));
    const response = override?.(url, init); if (response) return Promise.resolve(response);
    if (url.pathname === "/api/documents") return Promise.resolve(json([]));
    if (url.pathname === "/api/reconciliation-cases") return Promise.resolve(json({ cases: [summary()], nextOffset: null }));
    if (url.pathname === `/api/reconciliation-cases/${original.id}`) return Promise.resolve(json(original));
    if (url.pathname === `/api/reconciliation-cases/${other.id}`) return Promise.resolve(json(other));
    throw new Error(`Unexpected fixture ${init?.method ?? "GET"} ${url.pathname}`);
  });
  vi.stubGlobal("fetch", fetcher);
  const identity = { initialize: vi.fn(async () => true), token: vi.fn(async () => "test-token"), signIn: vi.fn(async () => {}), signOut: vi.fn(async () => {}) };
  render(<EnterpriseProvider config={config} identityFactory={async () => identity}><ReconciliationPage/></EnterpriseProvider>);
  return fetcher;
}
function details() { return within(screen.getByRole("form", { name: "Case details" })); }

describe("case references, categories and metadata", () => {
  beforeEach(() => { sessionStorage.clear(); window.history.replaceState(null, "", "/reconciliation"); });
  afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/reconciliation"); });

  it.each([
    { role: "Staff", admin: false, edit: true }, { role: "Staff", admin: true, edit: true },
    { role: "Staff", admin: false, edit: true }, { role: "Staff", admin: true, edit: true },
    { role: "Staff", admin: false, edit: true }, { role: "Staff", admin: true, edit: true },
  ] as const)("applies metadata permissions for $role with administration=$admin", async ({ role, admin, edit }) => {
    window.history.replaceState(null, "", `/reconciliation?case=${original.id}`);
    setup(role, admin);
    await screen.findByRole("heading", { name: original.name });
    expect(screen.getByText(original.reference)).toBeTruthy();
    expect(!!screen.queryByRole("button", { name: "Edit case details" })).toBe(edit);
  });

  it.each([false, true])("denies metadata access for None role with administration=%s", async admin => {
    window.history.replaceState(null, "", `/reconciliation?case=${original.id}`);
    const fetcher = setup("None", admin);
    await screen.findByRole("heading", { name: "Access needs attention." });
    expect(fetcher.mock.calls.some(([url]) => String(url).includes("/api/reconciliation-cases"))).toBe(false);
  });

  it.each(["Uncategorized", "OfficeSupplies"])("recovers a readable Arabic title with category %s and accepts only the server reference", async category => {
    const created = { ...other, workflowKind: "InvoicePurchaseOrder", name: "توريد المعدات · September", category, reference: "REC-2027-000901" };
    sessionStorage.setItem(`case-creation:${id(104)}:${id(105)}`, JSON.stringify({ fingerprint: JSON.stringify([created.name, category]), requestKey: id(90), workflowKind: "InvoicePurchaseOrder" }));
    const fetcher = setup("Staff", false, (url, init) => url.pathname === "/api/reconciliation-cases" && init?.method === "POST" ? json(created, 201) : undefined);
    await screen.findByLabelText("Case name");
    expect((screen.getByLabelText("Category") as HTMLSelectElement).value).toBe("Uncategorized");
    fireEvent.change(screen.getByLabelText("Case name"), { target: { value: created.name } });
    if (category !== "Uncategorized") fireEvent.change(screen.getByLabelText("Category"), { target: { value: category } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByRole("heading", { name: created.name });
    expect(screen.getByText(created.reference)).toBeTruthy();
    expect(screen.queryByLabelText("Quotation document")).toBeNull();
    expect(screen.getByRole("link", { name: "Return to invoice check" }).getAttribute("href")).toBe(`/invoices?case=${created.id}`);
    const post = fetcher.mock.calls.find(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ name: created.name, category, requestKey: expect.any(String), workflowKind: "InvoicePurchaseOrder" });
    expect(new URLSearchParams(window.location.search).get("case")).toBe(created.id);
  });

  it("saves name and category with If-Match without changing the reference, case link or saved comparison", async () => {
    window.history.replaceState(null, "", `/reconciliation?case=${original.id}&run=${id(30)}`);
    const renamed = { ...original, name: "Renamed case — حالة", category: "Services" };
    const fetcher = setup("Admin", false, (url, init) => url.pathname === `/api/reconciliation-cases/${original.id}` && init?.method === "PUT" ? json(renamed, 200, '"version-2"') : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "Edit case details" }));
    fireEvent.change(details().getByLabelText("Case name"), { target: { value: renamed.name } });
    fireEvent.change(details().getByLabelText("Category"), { target: { value: renamed.category } });
    fireEvent.click(details().getByRole("button", { name: "Save case details" }));
    await screen.findByRole("heading", { name: renamed.name });
    expect(screen.getByText(original.reference)).toBeTruthy();
    expect((screen.getByLabelText("Saved comparison") as HTMLSelectElement).value).toBe(id(30));
    expect(window.location.search).toBe(`?case=${original.id}&run=${id(30)}`);
    const update = fetcher.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(new Headers(update?.[1]?.headers).get("If-Match")).toBe('"version-1"');
    expect(JSON.parse(String(update?.[1]?.body))).toEqual({ name: renamed.name, category: renamed.category });
  });

  it("keeps rejected metadata drafts through explicit reload and resaves with the refreshed ETag", async () => {
    window.history.replaceState(null, "", `/reconciliation?case=${original.id}`);
    let reads = 0; let writes = 0;
    const fetcher = setup("Staff", false, (url, init) => {
      if (url.pathname !== `/api/reconciliation-cases/${original.id}`) return;
      if (init?.method === "PUT") return ++writes === 1 ? json({ detail: "Case details changed elsewhere." }, 412)
        : json({ ...original, name: "My retained title", category: "OfficeSupplies" }, 200, '"saved-version"');
      return ++reads === 1 ? json(original) : json({ ...original, name: "Remote title", category: "Services" }, 200, '"remote-version"');
    });
    fireEvent.click(await screen.findByRole("button", { name: "Edit case details" }));
    fireEvent.change(details().getByLabelText("Case name"), { target: { value: "My retained title" } });
    fireEvent.change(details().getByLabelText("Category"), { target: { value: "OfficeSupplies" } });
    fireEvent.click(details().getByRole("button", { name: "Save case details" }));
    fireEvent.click(await screen.findByRole("button", { name: "Reload case details" }));
    await screen.findByRole("heading", { name: "Remote title" });
    expect((details().getByLabelText("Case name") as HTMLInputElement).value).toBe("My retained title");
    expect((details().getByLabelText("Category") as HTMLSelectElement).value).toBe("OfficeSupplies");
    fireEvent.click(details().getByRole("button", { name: "Save case details" }));
    await screen.findByRole("heading", { name: "My retained title" });
    const updates = fetcher.mock.calls.filter(([, init]) => init?.method === "PUT");
    expect(updates).toHaveLength(2);
    expect(new Headers(updates[1][1]?.headers).get("If-Match")).toBe('"remote-version"');
  });

  it("keeps closed case metadata read-only for an Operator", async () => {
    window.history.replaceState(null, "", `/reconciliation?case=${original.id}`);
    setup("Admin", true, url => url.pathname === `/api/reconciliation-cases/${original.id}` ? json({ ...original, status: "Closed" }) : undefined);
    await screen.findByRole("heading", { name: original.name });
    expect(screen.getByText(original.reference)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Edit case details" })).toBeNull();
  });

  it("applies reference, category, status and UTC dates on the server and retains filters during pagination", async () => {
    const fetcher = setup("Staff", false, url => url.pathname === "/api/reconciliation-cases"
      ? json({ cases: Number(url.searchParams.get("offset")) === 50 ? [summary(other)] : [summary()], nextOffset: Number(url.searchParams.get("offset")) === 50 ? null : 50 }) : undefined);
    await screen.findByLabelText("Search cases");
    const listCalls = () => fetcher.mock.calls.filter(([url]) => new URL(String(url)).pathname === "/api/reconciliation-cases");
    await waitFor(() => expect(listCalls().length).toBeGreaterThan(0));
    const initialCount = listCalls().length;
    fireEvent.change(screen.getByLabelText("Search cases"), { target: { value: "REC-2026-000042" } });
    fireEvent.change(screen.getByLabelText("Category filter"), { target: { value: "Equipment" } });
    fireEvent.change(screen.getByLabelText("Status filter"), { target: { value: "Draft" } });
    fireEvent.change(screen.getByLabelText("Created from (UTC)"), { target: { value: "2026-09-01" } });
    fireEvent.change(screen.getByLabelText("Created to (UTC)"), { target: { value: "2026-09-30" } });
    expect(listCalls()).toHaveLength(initialCount);
    fireEvent.click(screen.getByRole("button", { name: "Apply filters" }));
    await waitFor(() => expect(listCalls()).toHaveLength(initialCount + 1));
    const applied = new URL(String(listCalls().at(-1)?.[0]));
    expect(Object.fromEntries(applied.searchParams)).toEqual({ offset: "0", q: "REC-2026-000042", category: "Equipment", status: "Draft", createdFrom: "2026-09-01", createdTo: "2026-09-30" });
    await waitFor(() => expect(screen.getByRole("button", { name: "Load more cases" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(screen.getByRole("button", { name: "Load more cases" }));
    await waitFor(() => expect(listCalls()).toHaveLength(initialCount + 2));
    const next = new URL(String(listCalls().at(-1)?.[0]));
    expect(Object.fromEntries(next.searchParams)).toEqual({ ...Object.fromEntries(applied.searchParams), offset: "50" });
    await screen.findByRole("link", { name: other.name });
    fireEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    await waitFor(() => expect(listCalls()).toHaveLength(initialCount + 3));
    expect(Object.fromEntries(new URL(String(listCalls().at(-1)?.[0])).searchParams)).toEqual({ offset: "0" });
    await waitFor(() => expect(screen.queryByRole("link", { name: other.name })).toBeNull());
  });

  it("ignores a late metadata save after navigating to a different case", async () => {
    window.history.replaceState(null, "", `/reconciliation?case=${original.id}`);
    let finish!: (response: Response) => void;
    const fetcher = setup("Staff", false, (url, init) => url.pathname === `/api/reconciliation-cases/${original.id}` && init?.method === "PUT"
      ? new Promise(resolve => { finish = resolve; }) : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "Edit case details" }));
    fireEvent.change(details().getByLabelText("Case name"), { target: { value: "Delayed title" } });
    fireEvent.click(details().getByRole("button", { name: "Save case details" }));
    await waitFor(() => expect(fetcher.mock.calls.some(([, init]) => init?.method === "PUT")).toBe(true));
    act(() => { window.history.pushState(null, "", `/reconciliation?case=${other.id}`); window.dispatchEvent(new PopStateEvent("popstate")); });
    await screen.findByRole("heading", { name: other.name });
    await act(async () => { finish(json({ ...original, name: "Delayed title" })); });
    expect(screen.queryByRole("heading", { name: "Delayed title" })).toBeNull();
    expect(screen.getByRole("heading", { name: other.name })).toBeTruthy();
    expect(screen.getByText(other.reference)).toBeTruthy();
  });

  it("retains an unsaved title for copying when stale reload reveals a closed case", async () => {
    window.history.replaceState(null, "", `/reconciliation?case=${original.id}`);
    let reads = 0;
    const fetcher = setup("Staff", false, (url, init) => {
      if (url.pathname !== `/api/reconciliation-cases/${original.id}`) return;
      if (init?.method === "PUT") return json({}, 412);
      return json(++reads === 1 ? original : { ...original, status: "Closed" }, 200, '"closed-version"');
    });
    fireEvent.click(await screen.findByRole("button", { name: "Edit case details" }));
    fireEvent.change(details().getByLabelText("Case name"), { target: { value: "Unsent title to preserve" } });
    fireEvent.click(details().getByRole("button", { name: "Save case details" }));
    fireEvent.click(await screen.findByRole("button", { name: "Reload case details" }));
    await screen.findByText("This case is now closed. Your unsaved details remain below for copying; they cannot be saved.");
    expect((details().getByLabelText("Case name") as HTMLInputElement).value).toBe("Unsent title to preserve");
    expect(details().getByRole("button", { name: "Save case details" }).hasAttribute("disabled")).toBe(true);
    expect(fetcher.mock.calls.filter(([, init]) => init?.method === "PUT")).toHaveLength(1);
  });

  it("does not let an older filtered response replace the latest search results", async () => {
    let finish!: (response: Response) => void;
    const fetcher = setup("Staff", false, url => {
      if (url.pathname !== "/api/reconciliation-cases") return;
      if (url.searchParams.get("q") === "Older search") return new Promise(resolve => { finish = resolve; });
      if (url.searchParams.get("q") === "Latest search") return json({ cases: [summary(other)], nextOffset: null });
    });
    await screen.findByLabelText("Search cases");
    fireEvent.change(screen.getByLabelText("Search cases"), { target: { value: "Older search" } });
    fireEvent.click(screen.getByRole("button", { name: "Apply filters" }));
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => new URL(String(url)).searchParams.get("q") === "Older search")).toBe(true));
    fireEvent.change(screen.getByLabelText("Search cases"), { target: { value: "Latest search" } });
    fireEvent.click(screen.getByRole("button", { name: "Apply filters" }));
    await screen.findByRole("link", { name: other.name });
    await act(async () => { finish(json({ cases: [summary()], nextOffset: 50 })); });
    expect(screen.queryByRole("link", { name: original.name })).toBeNull();
    expect(screen.getByRole("link", { name: other.name })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Load more cases" })).toBeNull();
  });
});
