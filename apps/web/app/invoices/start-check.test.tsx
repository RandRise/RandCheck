import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { StartInvoiceCheck } from "./components/start-invoice-check";
import type { Case, Document } from "./workflow.types";

const push = vi.hoisted(() => vi.fn());
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));
vi.mock("../security/api-fetch", () => ({ API: "http://test", apiFetch: (url: string, init?: RequestInit) => fetch(url, init) }));
vi.mock("../security/enterprise-provider", () => ({ useEnterprise: () => ({ session: { organizationId: "org", actorId: "actor" } }) }));

const id = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const invoice: Document = { id: id(10), originalFilename: "Invoice.txt", documentType: "Invoice", status: "Ready" };
const po: Document = { id: id(11), originalFilename: "Order.txt", documentType: "PurchaseOrder", status: "Ready" };
const quotation: Document = { id: id(12), originalFilename: "Old quotation.txt", documentType: "Quotation", status: "Ready" };
const emptyCase = (): Case => ({ id: id(1), name: "Invoice check", reference: "REC-2026-000001", status: "Draft", workflowKind: "InvoicePurchaseOrder", draftDocuments: [], documents: [], runs: [] });
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { ETag: `"${id(200)}"`, "Content-Type": "application/json" } });
const writes = (fetcher: ReturnType<typeof setup>["fetcher"]) => fetcher.mock.calls.filter(([, init]) => init?.method && init.method !== "GET");

function setup(override?: (url: URL, init: RequestInit | undefined, state: { item: Case }) => Response | Promise<Response> | undefined,
  permissions = { review: true, operate: true }, identity = "org:actor") {
  const state = { item: emptyCase() };
  const fetcher = vi.fn(async (raw: string, init?: RequestInit): Promise<Response> => {
    const url = new URL(raw);
    const answer = override?.(url, init, state);
    if (answer) return answer;
    if (url.pathname.endsWith("work-list")) return json([invoice, po, quotation]);
    if (url.pathname === "/api/reconciliation-cases" && init?.method === "POST") return json(state.item, 201);
    if (url.pathname === `/api/reconciliation-cases/${id(1)}`) return json(state.item);
    if (url.pathname.includes("draft-documents")) {
      const role = url.pathname.endsWith("Invoice") ? "Invoice" : "PurchaseOrder";
      state.item.draftDocuments = [...state.item.draftDocuments!.filter(draft => draft.role !== role), { role, documentId: JSON.parse(String(init?.body)).documentId }];
      return json(state.item);
    }
    if (url.pathname === "/api/documents" && init?.method === "POST") return json({ ...invoice, status: "Queued" }, 202);
    throw new Error(`Unexpected ${init?.method ?? "GET"} ${url.pathname}`);
  });
  vi.stubGlobal("fetch", fetcher);
  const mount = () => render(<StartInvoiceCheck identity={identity} canReview={permissions.review} canOperate={permissions.operate}/>);
  const view = mount();
  return { state, fetcher, view, mount };
}

async function choosePair() {
  await screen.findByRole("option", { name: "Invoice.txt · Ready" });
  for (const label of ["Use a saved invoice instead", "Use a saved purchase order instead"])
    if (!screen.getByText(label).closest("details")?.open) fireEvent.click(screen.getByText(label));
  fireEvent.change(screen.getByLabelText("Choose a saved invoice"), { target: { value: invoice.id } });
  fireEvent.change(screen.getByLabelText("Choose a saved purchase order"), { target: { value: po.id } });
}

beforeEach(() => { sessionStorage.clear(); push.mockReset(); window.history.replaceState(null, "", "/invoices/new"); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe("document-first invoice checks", () => {
  it("creates nothing for an empty or single-file choice, then saves both links without adopting reviews", async () => {
    const { fetcher } = setup();
    await screen.findByRole("option", { name: "Invoice.txt · Ready" });
    expect(screen.getByRole("button", { name: "Continue to review" }).hasAttribute("disabled")).toBe(true);
    fireEvent.change(screen.getByLabelText("Choose a saved invoice"), { target: { value: invoice.id } });
    expect(screen.getByRole("button", { name: "Continue to review" }).hasAttribute("disabled")).toBe(true);
    expect(writes(fetcher)).toHaveLength(0);
    await choosePair();
    fireEvent.click(screen.getByRole("button", { name: "Continue to review" }));
    await waitFor(() => expect(push).toHaveBeenCalledWith(`/invoices?case=${id(1)}&step=2`));
    expect(writes(fetcher).map(([url]) => new URL(url).pathname)).toEqual([
      "/api/reconciliation-cases", `/api/reconciliation-cases/${id(1)}/draft-documents/Invoice`, `/api/reconciliation-cases/${id(1)}/draft-documents/PurchaseOrder`,
    ]);
    expect(JSON.parse(String(writes(fetcher)[1][1]?.body)).documentId).toBe(invoice.id);
    expect(JSON.parse(String(writes(fetcher)[2][1]?.body)).documentId).toBe(po.id);
  });

  it("keeps an uploaded single file without creating a case", async () => {
    const { fetcher } = setup();
    await screen.findByRole("option", { name: "Invoice.txt · Ready" });
    fireEvent.change(screen.getByLabelText("Upload invoice"), { target: { files: [new File(["sample invoice"], "Invoice.txt", { type: "text/plain" })] } });
    await screen.findByText("File uploaded. Its values still need checking.");
    expect(screen.getByText("Selected: Invoice.txt")).toBeTruthy();
    expect(writes(fetcher).map(([url]) => new URL(url).pathname)).toEqual(["/api/documents"]);
    expect(screen.getByRole("button", { name: "Continue to review" }).hasAttribute("disabled")).toBe(true);
  });

  it("filters quotations and known wrong roles; the same file cannot fill both slots", async () => {
    setup(); await choosePair();
    expect(screen.queryByRole("option", { name: /Old quotation/ })).toBeNull();
    const choices = screen.getByLabelText("Choose a saved purchase order") as HTMLSelectElement;
    expect([...choices.options].some(option => option.value === invoice.id)).toBe(false);
  });

  it("locks rapid clicks to one creation and one save per role", async () => {
    const { fetcher } = setup(); await choosePair();
    const button = screen.getByRole("button", { name: "Continue to review" });
    act(() => { fireEvent.click(button); fireEvent.click(button); });
    await waitFor(() => expect(push).toHaveBeenCalledOnce());
    expect(writes(fetcher)).toHaveLength(3);
  });

  it("recovers an uncertain creation after remount with the same key and both choices", async () => {
    let attempts = 0;
    const fixture = setup((url, init) => url.pathname === "/api/reconciliation-cases" && init?.method === "POST" && ++attempts === 1 ? Promise.reject(new Error("Lost creation reply")) : undefined);
    await choosePair(); fireEvent.click(screen.getByRole("button", { name: "Continue to review" }));
    await screen.findByRole("alert"); fixture.view.unmount(); fixture.mount();
    await screen.findByRole("option", { name: "Invoice.txt · Ready" });
    fireEvent.click(screen.getByRole("button", { name: "Retry preparation" }));
    await waitFor(() => expect(push).toHaveBeenCalledOnce());
    const creates = writes(fixture.fetcher).filter(([, init]) => init?.method === "POST");
    expect(creates).toHaveLength(2);
    expect(JSON.parse(String(creates[0][1]?.body))).toEqual(JSON.parse(String(creates[1][1]?.body)));
  });

  it("reads a saved pair after a lost PO reply without repeating either link", async () => {
    const fixture = setup((url, init, state) => {
      if (url.pathname.endsWith("draft-documents/PurchaseOrder")) {
        state.item.draftDocuments!.push({ role: "PurchaseOrder", documentId: po.id });
        return Promise.reject(new Error("Lost PO reply"));
      }
    });
    await choosePair(); fireEvent.click(screen.getByRole("button", { name: "Continue to review" }));
    await screen.findByRole("alert");
    fixture.view.unmount(); fixture.mount(); await screen.findByRole("option", { name: "Invoice.txt · Ready" });
    fireEvent.click(screen.getByRole("button", { name: "Retry preparation" }));
    await waitFor(() => expect(push).toHaveBeenCalledOnce());
    expect(writes(fixture.fetcher)).toHaveLength(3);
    expect(fixture.fetcher.mock.calls.at(-1)?.[0]).toBe(`http://test/api/reconciliation-cases/${id(1)}`);
  });

  it.each(["missing", "wrong type"])("recovers a lost creation when the PO becomes %s without saving invalid links", async changed => {
    let changedList = false, creates = 0;
    const fixture = setup((url, init) => {
      if (url.pathname.endsWith("work-list") && changedList)
        return json(changed === "missing" ? [invoice] : [invoice, { ...po, documentType: "Quotation" }]);
      if (url.pathname === "/api/reconciliation-cases" && init?.method === "POST" && ++creates === 1)
        return Promise.reject(new Error("Lost reply"));
    });
    await choosePair(); fireEvent.click(screen.getByRole("button", { name: "Continue to review" }));
    await screen.findByRole("alert");
    fixture.view.unmount(); changedList = true; fixture.mount();
    await waitFor(() => expect(screen.queryByText("Loading saved documents…")).toBeNull());
    const retry = screen.getByRole("button", { name: "Retry preparation" });
    expect(retry.hasAttribute("disabled")).toBe(false);
    fireEvent.click(retry);
    await screen.findByText(/No document links were changed/);
    expect(writes(fixture.fetcher)).toHaveLength(2);
    expect(writes(fixture.fetcher).every(([, init]) => init?.method === "POST")).toBe(true);
    expect(JSON.parse(String(writes(fixture.fetcher)[0][1]?.body)))
      .toEqual(JSON.parse(String(writes(fixture.fetcher)[1][1]?.body)));
    expect(push).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Open saved check" }));
    expect(push).toHaveBeenCalledOnce();
  });

  it("does not overwrite a changed draft on recovery", async () => {
    const fixture = setup((url, init, state) => {
      if (url.pathname.endsWith("draft-documents/Invoice")) {
        state.item.draftDocuments = [{ role: "Invoice", documentId: id(99) }];
        return json({ detail: "Changed elsewhere" }, 412);
      }
    });
    await choosePair(); fireEvent.click(screen.getByRole("button", { name: "Continue to review" }));
    await screen.findByRole("alert"); fireEvent.click(screen.getByRole("button", { name: "Retry preparation" }));
    await screen.findByText(/saved document choices changed/);
    expect(writes(fixture.fetcher)).toHaveLength(2);
    expect(push).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Open saved check" }));
    expect(push).toHaveBeenCalledOnce();
  });

  it("adopts an older interrupted Home creation key without changing its payload", async () => {
    sessionStorage.setItem("invoice-check-creation:org:actor", JSON.stringify({ name: "Invoice check", key: id(80) }));
    const { fetcher } = setup(); await choosePair();
    fireEvent.click(screen.getByRole("button", { name: "Continue to review" }));
    await waitFor(() => expect(push).toHaveBeenCalledOnce());
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body))).toEqual({ name: "Invoice check", requestKey: id(80), workflowKind: "InvoicePurchaseOrder" });
    expect(sessionStorage.getItem("invoice-check-creation:org:actor")).toBeNull();
  });

  it("stops after an abandoned creation response and retains its recovery key", async () => {
    let finish!: (value: Response) => void;
    const fixture = setup((url, init) => url.pathname === "/api/reconciliation-cases" && init?.method === "POST" ? new Promise(resolve => { finish = resolve; }) : undefined);
    await choosePair(); fireEvent.click(screen.getByRole("button", { name: "Continue to review" }));
    fixture.view.unmount(); await act(async () => finish(json(emptyCase(), 201)));
    expect(writes(fixture.fetcher)).toHaveLength(1);
    expect(push).not.toHaveBeenCalled();
    expect(JSON.parse(sessionStorage.getItem("invoice-pair-start:org:actor")!).intent.creationKey).toBeTruthy();
  });

  it("requires review permission and offers no uploads without operation permission", async () => {
    const denied = setup(undefined, { review: false, operate: false });
    expect(screen.queryByRole("button", { name: "Continue to review" })).toBeNull();
    expect(denied.fetcher).not.toHaveBeenCalled();
    denied.view.unmount();
    setup(undefined, { review: true, operate: false }); await choosePair();
    expect(screen.queryByLabelText("Upload invoice")).toBeNull();
    expect(screen.getByRole("button", { name: "Continue to review" }).hasAttribute("disabled")).toBe(false);
  });

  it("does not restore another account's selected files or retry keys", async () => {
    const first = setup(); await choosePair(); first.view.unmount();
    setup(undefined, { review: true, operate: true }, "other:actor");
    await screen.findByRole("option", { name: "Invoice.txt · Ready" });
    expect((screen.getByLabelText("Choose a saved invoice") as HTMLSelectElement).value).toBe("");
    expect(screen.getByRole("button", { name: "Continue to review" }).hasAttribute("disabled")).toBe(true);
  });
});
