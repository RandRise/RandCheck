import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { EnterpriseProvider, type Session } from "../security/enterprise-provider";
import { InvoiceWorkflow } from "./components/invoice-workflow";
import { can, useEnterprise } from "../security/enterprise-provider";
// Legacy control-contract regression suite. journey.test.tsx exercises the new routed flow.
function InvoicesPage() {
  const { session } = useEnterprise();
  return <InvoiceWorkflow key={`${session.organizationId}:${session.actorId}:${session.role}`}
    canReview={can(session, "Review")} canOperate={can(session, "Operate")}
    identity={`${session.organizationId}:${session.actorId}`} />;
}
import { InvoiceReviewContext } from "./review-context";

vi.mock("next/navigation", () => ({ usePathname: () => "/invoices", useRouter: () => ({ replace: vi.fn() }) }));
const id = (n: number) => `00000000-0000-4000-8000-${n.toString().padStart(12, "0")}`;
const config = { mode: "Entra", tenantId: id(101), clientId: id(102), scope: `api://${id(103)}/DocumentIntelligence.Access`, api: "http://localhost:8080" };
const documents = [
  { id: id(10), originalFilename: "Invoice.pdf", status: "Processing", latestExtractionStatus: "Queued", reviewState: "NeedsReview" },
  { id: id(11), originalFilename: "PO.pdf", status: "Ready", reviewState: "Reviewed" },
  { id: id(12), originalFilename: "Replacement.pdf", status: "Ready", reviewState: "Reviewed" },
];
const saved = { id: id(1), name: "Saved supplier pair", reference: "REC-2026-000001", status: "Draft", workflowKind: "InvoicePurchaseOrder",
  draftDocuments: [{ role: "Invoice", documentId: id(10) }, { role: "PurchaseOrder", documentId: id(11) }], documents: [] as {role: string; documentId: string; reviewSessionId: string}[], runs: [] as {id: string; runNumber: number; ruleSetVersion: string; status: string}[] };
const ready = { ...saved, documents: [{ role: "Invoice", documentId: id(10), reviewSessionId: id(20) }, { role: "PurchaseOrder", documentId: id(11), reviewSessionId: id(21) }] };
function json(value: unknown, status = 200, etag = '"00000000-0000-4000-8000-000000000201"') { return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json", ETag: etag } }); }
function setup(role: string = "Admin", admin = false,
  override?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  const member = { organizationId: id(104), organizationName: "Example", actorId: id(105), displayName: "Tester", tenantId: config.tenantId, role };
  const fetcher = vi.fn((raw: string | URL | Request, init?: RequestInit) => {
    const url = new URL(String(raw));
    const response = override?.(url, init); if (response) return Promise.resolve(response);
    if (url.pathname === "/api/session") return Promise.resolve(json(member));
    if (url.pathname === "/api/documents/work-list") return Promise.resolve(json(documents));
    if (url.pathname === "/api/reconciliation-cases") return Promise.resolve(json({ cases: [saved], nextOffset: null }));
    if (url.pathname === `/api/reconciliation-cases/${saved.id}`) return Promise.resolve(json(saved));
    if (url.pathname.startsWith("/api/documents/") && url.pathname.endsWith("/review")) return Promise.resolve(json({ documentId: url.pathname.split("/")[3], session: { id: id(22), status: "Completed", revisionNumber: 3 } }));
    throw new Error(`Unexpected ${init?.method ?? "GET"} ${url.pathname}`);
  });
  vi.stubGlobal("fetch", fetcher);
  const identity = { initialize: vi.fn(async () => true), token: vi.fn(async () => "token"), signIn: vi.fn(async () => {}), signOut: vi.fn(async () => {}) };
  const rendered = render(<EnterpriseProvider config={config} identityFactory={async () => identity}><InvoicesPage/></EnterpriseProvider>);
  return { fetcher, rendered };
}
type Fetcher = ReturnType<typeof setup>["fetcher"];
const writes = (fetcher: Fetcher) => fetcher.mock.calls.filter(([, init]) => init?.method && init.method !== "GET");
const body = (fetcher: Fetcher, index = 0) => JSON.parse(String(writes(fetcher)[index][1]?.body));

describe("saved invoice and purchase order workflow", () => {
  beforeEach(() => { window.history.replaceState(null, "", `/invoices?case=${saved.id}`); sessionStorage.clear(); });
  afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); sessionStorage.clear(); window.history.replaceState(null, "", "/invoices"); });

  it("offers no known quotation for either side while leaving unknown uploads available", async () => {
    const quotations = ["Quotation", "quote", "عرض سعر", "عرض أسعار"].map((documentType, i) => ({
      id: id(80 + i), originalFilename: `Old quotation ${i}.pdf`, status: "Ready", documentType,
    }));
    const { fetcher } = setup("Admin", false, url => url.pathname === "/api/documents/work-list" ? json([...documents, ...quotations]) : undefined);
    const invoice = await screen.findByLabelText("Invoice document") as HTMLSelectElement;
    const order = screen.getByLabelText("Purchase order document") as HTMLSelectElement;
    await waitFor(() => expect(invoice.textContent).toContain("Replacement.pdf"));
    for (const select of [invoice, order]) {
      expect(select.textContent).not.toContain("Old quotation");
      expect(select.textContent).toContain("Invoice.pdf");
    }
    expect(writes(fetcher)).toHaveLength(0);
  });

  it("keeps a previously saved quotation visible without clearing or replacing it", async () => {
    const { fetcher } = setup("Admin", false, url => url.pathname === "/api/documents/work-list"
      ? json(documents.map(doc => doc.id === id(10) ? { ...doc, documentType: "Quotation", originalFilename: "Earlier quotation.pdf" } : doc)) : undefined);
    await screen.findByText(/This saved document is a quotation/);
    expect((screen.getByLabelText("Invoice document") as HTMLSelectElement).value).toBe(id(10));
    expect(screen.getByRole("link", { name: "Open invoice review" }).getAttribute("href")).toContain(`document=${id(10)}`);
    expect(screen.getByRole("button", { name: "Compare invoice and purchase order" }).hasAttribute("disabled")).toBe(true);
    expect(writes(fetcher)).toHaveLength(0);
  });

  it.each([
    { role: "Staff", admin: false, edit: true }, { role: "Staff", admin: true, edit: true },
    { role: "Staff", admin: false, edit: true }, { role: "Staff", admin: true, edit: true },
    { role: "Staff", admin: false, edit: true }, { role: "Admin", admin: true, edit: true },
  ] as const)("reopens without writes for $role administration=$admin", async ({ role, admin, edit }) => {
    const { fetcher } = setup(role, admin);
    await screen.findByRole("heading", { name: saved.name });
    expect(!!screen.queryByLabelText("Invoice document")).toBe(edit);
    expect(screen.getByRole("link", { name: "Open invoice review" }).getAttribute("href")).toBe(`/?document=${id(10)}&pairCase=${saved.id}&pairRole=Invoice`);
    expect(screen.getByText(/Processing: Queued/)).toBeTruthy();
    expect(writes(fetcher)).toHaveLength(0);
    const list = fetcher.mock.calls.find(([url]) => new URL(String(url)).pathname === "/api/reconciliation-cases");
    expect(new URL(String(list?.[0])).searchParams.get("workflowKind")).toBe("InvoicePurchaseOrder");
  });
  it.each([false, true])("administration=%s does not grant None role access", async admin => {
    const { fetcher } = setup("None", admin);
    await screen.findByRole("heading", { name: "Access needs attention." });
    expect(fetcher.mock.calls.some(([url]) => String(url).includes("/api/reconciliation-cases"))).toBe(false);
    expect(writes(fetcher)).toHaveLength(0);
  });
  it("saves an unreviewed draft once without pinning or comparison", async () => {
    let finish!: (value: Response) => void;
    const { fetcher } = setup("Admin", false, (url, init) => url.pathname.endsWith("/draft-documents/Invoice") && init?.method === "PUT" ? new Promise(resolve => { finish = resolve; }) : undefined);
    fireEvent.change(await screen.findByLabelText("Invoice document"), { target: { value: id(12) } });
    const save = screen.getByRole("button", { name: "Save invoice selection" });
    fireEvent.click(save); fireEvent.click(save);
    await waitFor(() => expect(writes(fetcher)).toHaveLength(1));
    expect(body(fetcher).documentId).toBe(id(12)); expect(body(fetcher).requestKey).toMatch(/^[0-9a-f-]{36}$/);
    expect(new Headers(writes(fetcher)[0][1]?.headers).get("If-Match")).toBe('"00000000-0000-4000-8000-000000000201"');
    await act(async () => finish(json({ ...saved, draftDocuments: [{role: "Invoice", documentId: id(12)}, saved.draftDocuments[1]] }, 200, '"00000000-0000-4000-8000-000000000202"')));
    await screen.findByText(/Invoice selection saved/);
    expect(writes(fetcher)).toHaveLength(1);
    expect(screen.getByRole("button", { name: "Compare invoice and purchase order" }).hasAttribute("disabled")).toBe(true);
  });
  it.each([412, 503, "network", "body"])("requires read recovery for uncertain %s save and preserves unsaved selection", async failure => {
    let reads = 0;
    const { fetcher } = setup("Admin", false, (url, init) => {
      if (url.pathname.endsWith("/draft-documents/Invoice") && init?.method === "PUT") {
        if (failure === "network") return Promise.reject(new Error("Lost acknowledgement"));
        if (failure === "body") return new Response("not-json", {status: 200});
        return json({}, Number(failure));
      }
      if (url.pathname === `/api/reconciliation-cases/${saved.id}`) return json(saved, 200, ++reads === 1 ? '"00000000-0000-4000-8000-000000000201"' : '"00000000-0000-4000-8000-000000000203"');
    });
    fireEvent.change(await screen.findByLabelText("Invoice document"), { target: { value: id(12) } });
    fireEvent.click(screen.getByRole("button", { name: "Save invoice selection" }));
    await screen.findByText(/Check the saved result before another write/);
    expect(writes(fetcher)).toHaveLength(1);
    fireEvent.click(screen.getByRole("button", { name: "Reload saved check" }));
    await screen.findByText(/Your unsaved selections remain below/);
    expect((screen.getByLabelText("Invoice document") as HTMLSelectElement).value).toBe(id(12));
    expect(screen.getByText("Invoice.pdf", {selector: "bdi"}).parentElement?.textContent).toBe("Saved document: Invoice.pdf");
    expect(writes(fetcher)).toHaveLength(1);
    expect(screen.getByRole("button", { name: "Save invoice selection" }).hasAttribute("disabled")).toBe(false);
  });
  it("requires explicit completed revision confirmation before the exact pin write", async () => {
    const { fetcher } = setup("Admin", false, (url, init) => url.pathname.endsWith("/documents/Invoice") && init?.method === "PUT" ? json({ ...saved, documents: [{role: "Invoice", documentId: id(10), reviewSessionId: id(22)}] }) : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "Choose invoice reviewed revision" }));
    await screen.findByText(/Use completed review 3:/);
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "Cancel review choice" }));
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "Choose invoice reviewed revision" }));
    fireEvent.click(await screen.findByRole("button", { name: "Use invoice reviewed revision" }));
    await screen.findByText(/Invoice reviewed revision saved/);
    expect(body(fetcher)).toEqual({documentId: id(10), reviewSessionId: id(22)});
    expect(writes(fetcher)).toHaveLength(1);
  });
  it("does not pin an in-progress review or compare a draft without both pins", async () => {
    const { fetcher } = setup("Admin", false, url => url.pathname.endsWith("/review") ? json({documentId: id(10), session: {id: id(22), status: "InProgress"}}) : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "Choose invoice reviewed revision" }));
    await screen.findByText(/Complete the invoice review first/);
    expect(screen.queryByRole("button", { name: "Use invoice reviewed revision" })).toBeNull();
    fireEvent.change(screen.getByLabelText("Invoice scope"), {target: {value: "FullOrder"}});
    fireEvent.submit(screen.getByRole("button", {name: "Compare invoice and purchase order"}).closest("form")!);
    expect(writes(fetcher)).toHaveLength(0);
  });
  it.each(["Unknown", "FullOrder", "Partial"])("requires chosen scope and sends explicit pair policy for %s", async scope => {
    const { fetcher } = setup("Admin", false, (url, init) => {
      if (url.pathname.endsWith("/evaluations") && init?.method === "POST") return json({...ready, runs: [{id: id(30), runNumber: 1, ruleSetVersion: "invoice-po-1.0", status: "Completed"}]});
      if (url.pathname === `/api/reconciliation-cases/${saved.id}`) return json(ready);
    });
    const compare = await screen.findByRole("button", {name: "Compare invoice and purchase order"});
    expect(compare.hasAttribute("disabled")).toBe(true);
    fireEvent.submit(compare.closest("form")!); expect(writes(fetcher)).toHaveLength(0);
    fireEvent.change(screen.getByLabelText("Invoice scope"), {target: {value: scope}});
    fireEvent.submit(compare.closest("form")!); fireEvent.submit(compare.closest("form")!);
    await screen.findByText(/Comparison saved/);
    expect(writes(fetcher)).toHaveLength(1); expect(body(fetcher)).toEqual({ruleSetVersion: "invoice-po-1.0", orderScope: scope});
    expect(screen.getByRole("link", {name: "Inspect comparison 1"}).getAttribute("href")).toBe(`/reconciliation?case=${saved.id}&run=${id(30)}`);
    expect(screen.queryByRole("button", {name: "Compare invoice and purchase order"})).toBeNull();
  });
  it.each(["aed_currency_required", "aed_currency_unverified"])("shows the backend currency message without saving or retrying: %s", async code => {
    const detail = "Check the currency on the invoice. This check supports AED only. Your saved work is kept.";
    const { fetcher } = setup("Admin", false, (url, init) => {
      if (url.pathname.endsWith("/evaluations") && init?.method === "POST") return json({ code, detail }, 400);
      if (url.pathname === `/api/reconciliation-cases/${saved.id}`) return json(ready);
    });
    fireEvent.change(await screen.findByLabelText("Invoice scope"), { target: { value: "Unknown" } });
    expect(screen.getByText(/We do not convert money/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Compare invoice and purchase order" }));
    await screen.findByText(detail);
    expect(writes(fetcher)).toHaveLength(1);
    expect(screen.queryByText(/Comparison saved/)).toBeNull();
    expect((screen.getByLabelText("Invoice document") as HTMLSelectElement).value).toBe(id(10));
    expect((screen.getByLabelText("Purchase order document") as HTMLSelectElement).value).toBe(id(11));
    expect(screen.getByRole("link", { name: "Open invoice review" })).toBeTruthy();
  });

  it("withholds comparison when unsaved selection differs from pinned drafts", async () => {
    const { fetcher } = setup("Admin", false, url => url.pathname === `/api/reconciliation-cases/${saved.id}` ? json(ready) : undefined);
    fireEvent.change(await screen.findByLabelText("Invoice scope"), {target: {value: "FullOrder"}});
    fireEvent.change(screen.getByLabelText("Invoice document"), {target: {value: id(12)}});
    const compare = screen.getByRole("button", {name: "Compare invoice and purchase order"});
    expect(compare.hasAttribute("disabled")).toBe(true); fireEvent.submit(compare.closest("form")!);
    expect(writes(fetcher)).toHaveLength(0);
  });
  it("rejects a legacy case deep link without mutation", async () => {
    const { fetcher } = setup("Admin", false, url => url.pathname === `/api/reconciliation-cases/${saved.id}` ? json({...saved, workflowKind: "Legacy"}) : undefined);
    await screen.findByText(/This is an existing comparison case/);
    expect(screen.queryByLabelText("Invoice document")).toBeNull(); expect(writes(fetcher)).toHaveLength(0);
  });
  it("invalid case URL makes no case read or write", async () => {
    window.history.replaceState(null, "", "/invoices?case=not-a-uuid");
    const { fetcher } = setup(); await screen.findByText(/This invoice check link is invalid/);
    expect(fetcher.mock.calls.some(([raw]) => new URL(String(raw)).pathname.includes("/api/reconciliation-cases/"))).toBe(false);
    expect(writes(fetcher)).toHaveLength(0);
  });
  it("retains the exact creation key across uncertain retry and fresh remount", async () => {
    window.history.replaceState(null, "", "/invoices"); let attempts = 0;
    const override = (url: URL, init?: RequestInit) => url.pathname === "/api/reconciliation-cases" && init?.method === "POST" ? (++attempts === 1 ? Promise.reject(new Error("Lost creation reply")) : json({...saved, name: "New pair", draftDocuments: [], documents: [], runs: []}, 201)) : undefined;
    const first = setup("Admin", false, override);
    fireEvent.change(await screen.findByLabelText("Check name"), {target: {value: "New pair"}});
    fireEvent.click(screen.getByRole("button", {name: "Create invoice check"})); await screen.findByText(/Retry the same name/);
    const original = body(first.fetcher); first.rendered.unmount();
    const second = setup("Admin", false, override);
    fireEvent.change(await screen.findByLabelText("Check name"), {target: {value: "New pair"}});
    fireEvent.click(screen.getByRole("button", {name: "Create invoice check"})); await screen.findByRole("heading", {name: "New pair"});
    expect(body(second.fetcher)).toEqual(original); expect(original.workflowKind).toBe("InvoicePurchaseOrder");
    expect(new URL(window.location.href).searchParams.get("case")).toBe(saved.id);
  });
  it("late case read after navigation cannot restore prior private state", async () => {
    let finish!: (response: Response) => void;
    const {fetcher} = setup("Admin", false, url => url.pathname === `/api/reconciliation-cases/${saved.id}` ? new Promise(resolve => {finish = resolve;}) : undefined);
    await waitFor(() => expect(fetcher.mock.calls.some(([raw]) => String(raw).endsWith(saved.id))).toBe(true));
    await act(async () => {window.history.replaceState(null, "", "/invoices"); window.dispatchEvent(new PopStateEvent("popstate"));});
    await screen.findByLabelText("Check name");
    await act(async () => finish(json(saved)));
    expect(screen.queryByRole("heading", {name: saved.name})).toBeNull(); expect(writes(fetcher)).toHaveLength(0);
  });

  it("malformed successful creation retains key until a verified recovery", async () => {
    window.history.replaceState(null, "", "/invoices"); let attempts = 0;
    const {fetcher} = setup("Admin", false, (url, init) => url.pathname === "/api/reconciliation-cases" && init?.method === "POST"
      ? (++attempts === 1 ? json({...saved, id: "invalid"}, 201) : json({...saved, name: "Verified pair"}, 201)) : undefined);
    fireEvent.change(await screen.findByLabelText("Check name"), {target: {value: "Verified pair"}});
    fireEvent.click(screen.getByRole("button", {name: "Create invoice check"})); await screen.findByText(/Retry the same name/);
    expect(screen.queryByRole("heading", {name: saved.name})).toBeNull();
    fireEvent.click(screen.getByRole("button", {name: "Create invoice check"})); await screen.findByRole("heading", {name: "Verified pair"});
    expect(body(fetcher, 1)).toEqual(body(fetcher));
  });
  it.each(["wrong-id", "bad-etag"])("successful malformed %s save still demands read recovery", async malformed => {
    const {fetcher} = setup("Admin", false, (url, init) => url.pathname.endsWith("/draft-documents/Invoice") && init?.method === "PUT"
      ? json({...saved, id: malformed === "wrong-id" ? id(99) : saved.id}, 200, malformed === "bad-etag" ? '"not-a-version"' : `"${id(203)}"`) : undefined);
    fireEvent.change(await screen.findByLabelText("Invoice document"), {target: {value: id(12)}});
    fireEvent.click(screen.getByRole("button", {name: "Save invoice selection"})); await screen.findByText(/Check the saved result before another write/);
    expect(screen.getByRole("button", {name: "Save invoice selection"}).hasAttribute("disabled")).toBe(true);
    expect(writes(fetcher)).toHaveLength(1); expect((screen.getByLabelText("Invoice document") as HTMLSelectElement).value).toBe(id(12));
  });
  it("lost later comparison acknowledgement reload disarms new comparison and scope", async () => {
    const firstRun = {id: id(30), runNumber: 1, ruleSetVersion: "invoice-po-1.0", status: "Completed"}; let reads = 0;
    const {fetcher} = setup("Admin", false, (url, init) => {
      if (url.pathname.endsWith("/evaluations") && init?.method === "POST") return Promise.reject(new Error("Lost comparison reply"));
      if (url.pathname === `/api/reconciliation-cases/${saved.id}`) return json({...ready, runs: ++reads === 1 ? [firstRun] : [{...firstRun, id: id(31), runNumber: 2}, firstRun]});
    });
    fireEvent.click(await screen.findByRole("button", {name: "Start another comparison"}));
    fireEvent.change(screen.getByLabelText("Invoice scope"), {target: {value: "FullOrder"}});
    fireEvent.submit(screen.getByRole("button", {name: "Compare invoice and purchase order"}).closest("form")!);
    await screen.findByText(/Check the saved result before another write/);
    fireEvent.click(screen.getByRole("button", {name: "Reload saved check"})); await screen.findByRole("link", {name: "Inspect comparison 2"});
    expect(writes(fetcher)).toHaveLength(1); expect(screen.queryByRole("button", {name: "Compare invoice and purchase order"})).toBeNull();
    fireEvent.click(screen.getByRole("button", {name: "Start another comparison"}));
    expect((screen.getByLabelText("Invoice scope") as HTMLSelectElement).value).toBe("");
    expect(screen.getByRole("button", {name: "Compare invoice and purchase order"}).hasAttribute("disabled")).toBe(true);
  });
  it("review context hides the prior case immediately while next context is pending", async () => {
    window.history.replaceState(null, "", `/?document=${id(10)}&pairCase=${saved.id}&pairRole=Invoice`);
    let finish!: (response: Response) => void;
    const fetcher = vi.fn((raw: string | URL | Request) => new URL(String(raw)).pathname.endsWith(id(99))
      ? new Promise<Response>(resolve => {finish = resolve;}) : Promise.resolve(json(saved)));
    vi.stubGlobal("fetch", fetcher);
    const view = render(<InvoiceReviewContext documentId={id(10)}/>);
    await screen.findByRole("link", {name: "Return to invoice check"});
    await act(async () => {window.history.replaceState(null, "", `/?document=${id(12)}&pairCase=${id(99)}&pairRole=Invoice`); view.rerender(<InvoiceReviewContext documentId={id(12)}/>);});
    expect(screen.queryByText(/Invoice check: Saved supplier pair/)).toBeNull(); expect(screen.queryByRole("link", {name: "Return to invoice check"})).toBeNull();
    await act(async () => finish(json({}, 404)));
    expect(screen.queryByRole("link", {name: "Return to invoice check"})).toBeNull();
  });
  it("role refresh removes edit state and late prior write cannot restore it", async () => {
    let sessionReads = 0; let finish!: (response: Response) => void;
    const member = {organizationId: id(104), organizationName: "Example", actorId: id(105), displayName: "Tester", tenantId: config.tenantId, role: "Viewer"};
    const {fetcher} = setup("Admin", false, (url, init) => {
      if (url.pathname === "/api/session" && ++sessionReads > 1) return json(member);
      if (url.pathname.endsWith("/draft-documents/Invoice") && init?.method === "PUT") return new Promise(resolve => {finish = resolve;});
    });
    fireEvent.change(await screen.findByLabelText("Invoice document"), {target: {value: id(12)}}); fireEvent.click(screen.getByRole("button", {name: "Save invoice selection"}));
    await waitFor(() => expect(writes(fetcher)).toHaveLength(1));
    await act(async () => window.dispatchEvent(new Event("focus")));
    await screen.findByRole("heading", { name: "Access needs attention." }); expect(screen.queryByLabelText("Invoice document")).toBeNull();
    await act(async () => finish(json({...saved, name: "Late private draft", draftDocuments: [{role: "Invoice", documentId: id(12)}]})));
    expect(screen.queryByRole("heading", {name: "Late private draft"})).toBeNull(); expect(screen.queryByLabelText("Invoice document")).toBeNull(); expect(writes(fetcher)).toHaveLength(1);
  });
});
