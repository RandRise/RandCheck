import { useEffect } from "react";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { InvoiceJourney } from "./components/invoice-journey";
import { InvoiceHome } from "./components/invoice-home";

const fixture = vi.hoisted(() => ({
  push: vi.fn(),
  id: (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`,
  reviewStatus: "Completed",
}));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: fixture.push }), usePathname: () => "/invoices" }));
vi.mock("../security/api-fetch", () => ({ API: "http://test", apiFetch: (url: string, init?: RequestInit) => fetch(url, init) }));
vi.mock("../security/enterprise-provider", () => ({
  useEnterprise: () => ({ session: { organizationId: fixture.id(100), actorId: fixture.id(101), role: "Staff" } }),
  can: () => true,
}));
vi.mock("../document-workspace", () => ({
  default: function ReviewStub({ documentId, onState, readOnly }: { documentId: string; onState?: (value: object) => void; readOnly?: boolean }) {
    useEffect(() => { onState?.({ review: { documentId, session: {
      id: fixture.id(documentId === fixture.id(10) ? 20 : 21), status: fixture.reviewStatus, revisionNumber: 3,
    }, fields: [] }, dirty: false, busy: false, unavailable: false }); }, [documentId, onState]);
    return <section><p>Inspecting {documentId}</p>
      {!readOnly && <button onClick={() => onState?.({ review: null, dirty: true, busy: false, unavailable: true })}>Type an unsaved correction</button>}
    </section>;
  },
}));
vi.mock("../reconciliation/reconciliation-workspace", () => ({ default: () => <p>Saved result evidence and decisions</p> }));

const id = fixture.id;
const docList = [
  { id: id(10), originalFilename: "Invoice.pdf", status: "Ready", documentType: "Invoice" },
  { id: id(11), originalFilename: "PO.pdf", status: "Ready", documentType: "PurchaseOrder" },
  { id: id(12), originalFilename: "Old quotation.pdf", status: "Ready", documentType: "Quotation" },
];
function pair() {
  return { id: id(1), name: "Invoice check", reference: "REC-1", status: "Draft", workflowKind: "InvoicePurchaseOrder",
    draftDocuments: [{ role: "Invoice", documentId: id(10) }, { role: "PurchaseOrder", documentId: id(11) }],
    documents: [] as { role: string; documentId: string; reviewSessionId: string }[],
    runs: [] as { id: string; runNumber: number; status: string; ruleSetVersion: string }[] };
}
function json(value: unknown, status = 200, token = 200) {
  return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json", ETag: `"${id(token)}"` } });
}
function setup(options: { failedPin?: boolean; failedCompare?: boolean; noPair?: boolean; readonly?: boolean; wrongPo?: boolean;
  documentResponse?: Promise<Response>; wrongUpload?: boolean } = {}) {
  let saved = pair();
  if (options.noPair) saved.draftDocuments = [];
  if (options.wrongPo) saved.draftDocuments[1].documentId = id(13);
  let token = 200;
  const fetcher = vi.fn(async (url: string | URL | Request, init?: RequestInit) => {
    const path = new URL(String(url)).pathname;
    if (path === "/api/documents/work-list") return options.documentResponse ?? json([...docList, { id: id(13), originalFilename: "Second invoice.pdf", documentType: "invoice", status: "Ready" }]);
    if (path === "/api/documents" && options.wrongUpload) return json({ id: id(99), originalFilename: "Wrong file.pdf", documentType: "invoice", status: "Ready" }, 202);
    if (path === "/api/reconciliation-cases") return json({ cases: [saved], nextOffset: null });
    if (!init?.method) return json(saved, 200, token);
    if (path.includes("/documents/")) {
      if (options.failedPin) return json({ detail: "Changed elsewhere" }, 412);
      const role = path.split("/").at(-1)!;
      const body = JSON.parse(String(init.body));
      saved = { ...saved, documents: [...saved.documents.filter(value => value.role !== role), { role, ...body }] };
      return json(saved, 200, ++token);
    }
    if (path.endsWith("/evaluations")) {
      if (options.failedCompare) throw new Error("Lost response");
      saved = { ...saved, runs: [{ id: id(30), runNumber: 1, ruleSetVersion: "invoice-po-2.0", status: "Completed" }] };
      return json(saved, 200, ++token);
    }
    throw new Error(`Unexpected mutation ${path}`);
  });
  vi.stubGlobal("fetch", fetcher);
  const rendered = render(<InvoiceJourney canReview={!options.readonly} canOperate={!options.readonly} identity="test" />);
  return { fetcher, rendered };
}
const writes = (fetcher: ReturnType<typeof setup>["fetcher"]) => fetcher.mock.calls.filter(([, init]) => init?.method);

beforeEach(() => {
  fixture.reviewStatus = "Completed"; fixture.push.mockClear(); sessionStorage.clear();
  window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=2`);
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe("numbered invoice journey", () => {
  const progress = (name: string) => screen.getByRole("heading", { name }).closest("li")?.getAttribute("data-progress");

  it("advances the badges only after both exact reviews are saved", async () => {
    const { fetcher } = setup();
    await waitFor(() => expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(false));
    expect(progress("1. Add documents")).toBe("saved");
    expect(progress("2. Check details")).toBe("current");
    expect(progress("3. See results")).toBe("upcoming");
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "Continue to purchase order" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Show results" }).hasAttribute("disabled")).toBe(false));
    expect(progress("2. Check details")).toBe("current");
    fireEvent.click(screen.getByRole("button", { name: "Show results" }));
    await screen.findByText("Saved result evidence and decisions");
    expect(progress("2. Check details")).toBe("saved");
    expect(progress("3. See results")).toBe("current");
    expect(screen.getByText("Reviews saved")).toBeTruthy();
    expect(document.querySelectorAll('.workflow-phase[aria-current="step"]')).toHaveLength(1);
  });

  it("does not mark a wrong-type saved pair as finished", async () => {
    setup({ wrongPo: true });
    await screen.findByText("A selected file has the wrong type. Change it below.");
    expect(progress("1. Add documents")).toBe("upcoming");
    expect(screen.queryByText("Files saved")).toBeNull();
    expect(screen.queryByText("Reviews saved")).toBeNull();
  });

  it("does not turn completed document reviews into a finished phase when saving the pin fails", async () => {
    setup({ failedPin: true });
    await waitFor(() => expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(screen.getByRole("button", { name: "Continue to purchase order" }));
    await screen.findByText("A save could not be confirmed. Check what reached the server before continuing.");
    expect(progress("2. Check details")).toBe("current");
    expect(progress("3. See results")).toBe("upcoming");
    expect(screen.queryByText("Reviews saved")).toBeNull();
  });

  it("shows the whole workflow without duplicate step navigation", async () => {
    window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=1`);
    setup({ noPair: true });
    for (const name of ["1. Add documents", "2. Check details", "3. See results"])
      expect(await screen.findByRole("heading", { name })).toBeTruthy();
    expect(screen.queryByRole("navigation", { name: "Review progress" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Back to Home" })).toBeNull();
    expect(screen.getByLabelText("Upload invoice")).toBeTruthy();
    expect(screen.getByText("Use a saved invoice instead").closest("details")?.open).toBe(false);
    expect(screen.getByRole("button", { name: "Continue to check values" }).hasAttribute("disabled")).toBe(true);
  });

  it("keeps the inspected invoice through optional details and only evaluates after Show results", async () => {
    const { fetcher } = setup();
    await waitFor(() => expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(screen.getByRole("button", { name: "Continue to purchase order" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Show results" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(screen.getByRole("button", { name: "Add documents details" }));
    await screen.findByRole("heading", { name: "File details" });
    expect(screen.queryByRole("heading", { name: "Your invoice check" })).toBeNull();
    expect(writes(fetcher)).toHaveLength(1);
    fireEvent.click(screen.getByRole("button", { name: "Return to workflow" }));
    expect(screen.getByRole("button", { name: "Show results" }).hasAttribute("disabled")).toBe(false);
    expect(document.activeElement).toBe(screen.getByRole("heading", { name: "2. Check details" }));
    expect(new URLSearchParams(window.location.search).get("role")).toBe("PurchaseOrder");
    fireEvent.click(screen.getByRole("button", { name: "Show results" }));
    await screen.findByText("Saved result evidence and decisions");
    expect(writes(fetcher)).toHaveLength(3);
  });

  it("returns from a directly opened details link and through browser history without writes", async () => {
    window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=2&role=PurchaseOrder&view=details&details=1`);
    const { fetcher } = setup();
    await screen.findByRole("heading", { name: "File details" });
    const detailUrl = window.location.href;
    fireEvent.click(screen.getByRole("button", { name: "Return to workflow" }));
    expect(screen.getByRole("button", { name: "Show results" }).hasAttribute("disabled")).toBe(true);
    await act(async () => {
      window.history.replaceState(null, "", detailUrl);
      window.dispatchEvent(new PopStateEvent("popstate"));
    });
    expect(screen.getByRole("heading", { name: "File details" })).toBeTruthy();
    await act(async () => {
      window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=2&role=PurchaseOrder`);
      window.dispatchEvent(new PopStateEvent("popstate"));
    });
    expect(screen.getByRole("heading", { name: "2. Check details" })).toBeTruthy();
    expect(writes(fetcher)).toHaveLength(0);
  });

  it("keeps unsaved edits on the workflow when Details is requested", async () => {
    const { fetcher } = setup();
    fireEvent.click(await screen.findByRole("button", { name: "Type an unsaved correction" }));
    fireEvent.click(screen.getByRole("button", { name: "Add documents details" }));
    await screen.findByText(/Save or discard your edits before opening details/);
    expect(screen.queryByRole("button", { name: "Return to workflow" })).toBeNull();
    expect(new URLSearchParams(window.location.search).get("view")).toBeNull();
    expect(writes(fetcher)).toHaveLength(0);
  });

  it("blocks browser Back into Details while edits are unsaved, without hiding the work", async () => {
    const { fetcher } = setup();
    fireEvent.click(await screen.findByRole("button", { name: "Add documents details" }));
    const detailsUrl = window.location.href;
    fireEvent.click(screen.getByRole("button", { name: "Return to workflow" }));
    fireEvent.click(screen.getByRole("button", { name: "Type an unsaved correction" }));
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
    await act(async () => {
      window.history.replaceState(null, "", detailsUrl);
      window.dispatchEvent(new PopStateEvent("popstate"));
    });
    expect(screen.queryByRole("button", { name: "Return to workflow" })).toBeNull();
    expect(screen.getByRole("button", { name: "Type an unsaved correction" })).toBeTruthy();
    expect(screen.getByText(/Save or discard your edits before opening details/)).toBeTruthy();
    expect(new URLSearchParams(window.location.search).get("view")).toBeNull();
    expect(confirm).not.toHaveBeenCalled();
    expect(writes(fetcher)).toHaveLength(0);
  });
  it("pins only the exact reviews displayed and evaluates with the last returned ETag", async () => {
    const { fetcher } = setup();
    await screen.findByRole("button", { name: "Continue to purchase order" });
    await waitFor(() => expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(screen.getByRole("button", { name: "Continue to purchase order" }));
    await screen.findByText(`Inspecting ${id(11)}`);
    await waitFor(() => expect(screen.getByRole("button", { name: "Show results" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(screen.getByRole("button", { name: "Show results" }));
    await screen.findByText("Saved result evidence and decisions");
    const changes = writes(fetcher);
    expect(changes).toHaveLength(3);
    expect(JSON.parse(String(changes[0][1]?.body)).reviewSessionId).toBe(id(20));
    expect(JSON.parse(String(changes[1][1]?.body)).reviewSessionId).toBe(id(21));
    expect(JSON.parse(String(changes[2][1]?.body))).toEqual({ ruleSetVersion: "invoice-po-2.0" });
    expect((changes[2][1]?.headers as Record<string, string>)["If-Match"]).toBe(`"${id(202)}"`);
    expect(fetcher.mock.calls.some(([url]) => String(url).endsWith("/review"))).toBe(false);
    expect(window.location.pathname).toBe("/invoices");
    expect(new URLSearchParams(window.location.search).get("step")).toBe("3");
  });

  it("does not confirm or pin an unfinished document", async () => {
    fixture.reviewStatus = "InProgress";
    const { fetcher } = setup();
    expect((await screen.findByRole("button", { name: "Continue to purchase order" })).hasAttribute("disabled")).toBe(true);
    expect(writes(fetcher)).toHaveLength(0);
  });

  it("stops on a stale pin and reloads without replaying the write", async () => {
    const { fetcher } = setup({ failedPin: true });
    await screen.findByRole("button", { name: "Continue to purchase order" });
    await waitFor(() => expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(screen.getByRole("button", { name: "Continue to purchase order" }));
    fireEvent.click(await screen.findByRole("button", { name: "Check saved progress" }));
    await waitFor(() => expect(screen.queryByRole("button", { name: "Check saved progress" })).toBeNull());
    expect(writes(fetcher)).toHaveLength(1);
    expect(screen.getByText(`Inspecting ${id(10)}`)).toBeTruthy();
  });

  it("keeps partial pin saves after a lost comparison response and does not replay evaluation on reload", async () => {
    const { fetcher } = setup({ failedCompare: true });
    await screen.findByRole("button", { name: "Continue to purchase order" });
    await waitFor(() => expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(screen.getByRole("button", { name: "Continue to purchase order" }));
    await screen.findByText(`Inspecting ${id(11)}`);
    await waitFor(() => expect(screen.getByRole("button", { name: "Show results" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(screen.getByRole("button", { name: "Show results" }));
    fireEvent.click(await screen.findByRole("button", { name: "Check saved progress" }));
    await waitFor(() => expect(screen.queryByRole("button", { name: "Check saved progress" })).toBeNull());
    expect(writes(fetcher)).toHaveLength(3);
  });

  it("keeps unsaved corrections when navigation is cancelled", async () => {
    setup();
    // Wait for the stub's initial review receipt before making a new edit.
    await waitFor(() => expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(await screen.findByRole("button", { name: "Type an unsaved correction" }));
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
    fireEvent.click(screen.getByRole("button", { name: "Change documents" }));
    expect(confirm).toHaveBeenCalledOnce();
    expect(screen.getByText(`Inspecting ${id(10)}`)).toBeTruthy();
    expect(new URLSearchParams(window.location.search).get("step")).toBe("2");
  });

  it("cancels cross-case browser Back before the loader can discard a draft", async () => {
    const { fetcher } = setup();
    // The stub publishes its loaded review in an effect; wait for that receipt before making an edit.
    await waitFor(() => expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(false));
    fireEvent.click(await screen.findByRole("button", { name: "Type an unsaved correction" }));
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
    await act(async () => {
      window.history.replaceState(null, "", `/invoices?case=${id(2)}&step=1`);
      window.dispatchEvent(new PopStateEvent("popstate"));
    });
    expect(confirm).toHaveBeenCalledOnce();
    expect(new URLSearchParams(window.location.search).get("case")).toBe(id(1));
    expect(screen.getByText(`Inspecting ${id(10)}`)).toBeTruthy();
    expect(fetcher.mock.calls.some(([url]) => String(url).endsWith(`/reconciliation-cases/${id(2)}`))).toBe(false);
    expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(true);
  });

  it("does not clear dirty review state when the active step or document is clicked", async () => {
    setup();
    fireEvent.click(await screen.findByRole("button", { name: "Type an unsaved correction" }));
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
    expect(screen.queryByRole("button", { name: "2. Check details" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Invoice" }));
    fireEvent.click(screen.getByRole("button", { name: "Change documents" }));
    expect(confirm).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(true);
  });

  it("prevents jumping to results before a result is saved", async () => {
    setup();
    await screen.findByRole("heading", { name: "3. See results" });
    expect(screen.queryByRole("button", { name: "View results" })).toBeNull();
  });

  it("keeps quotations out of new selections and offers upload in the same workspace", async () => {
    window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=1`);
    setup({ noPair: true });
    const select = await screen.findByLabelText("Choose a saved invoice");
    await waitFor(() => expect(select.textContent).toContain("Invoice.pdf"));
    expect(select.textContent).not.toContain("Old quotation.pdf");
    expect(select.textContent).not.toContain("PO.pdf");
    const purchaseOrder = screen.getByLabelText("Choose a saved purchase order");
    expect(purchaseOrder.textContent).toContain("PO.pdf");
    expect(purchaseOrder.textContent).not.toContain("Invoice.pdf");
    expect(purchaseOrder.textContent).not.toContain("Second invoice.pdf");
    expect(screen.getByLabelText("Upload invoice")).toBeTruthy();
    expect(screen.queryByRole("link", { name: /Open invoice review/ })).toBeNull();
  });

  it("explains an existing invoice in the PO slot before any pin or comparison request", async () => {
    window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=2&role=PurchaseOrder`);
    const { fetcher } = setup({ wrongPo: true });
    const showResults = await screen.findByRole("button", { name: "Show results" });
    await screen.findByText(/This file is identified as an invoice/);
    expect(showResults.hasAttribute("disabled")).toBe(true);
    expect(screen.getByText(`Inspecting ${id(13)}`)).toBeTruthy();
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "Change documents" }));
    const selected = screen.getByLabelText("Choose a saved purchase order") as HTMLSelectElement;
    expect(selected.value).toBe(id(13));
    expect(selected.selectedOptions[0].disabled).toBe(true);
    expect(selected.textContent).toContain("PO.pdf");
    expect(screen.getByRole("button", { name: "Continue to check values" }).hasAttribute("disabled")).toBe(true);
    expect(writes(fetcher)).toHaveLength(0);
  });

  it("keeps the open draft when a delayed document list reveals the wrong type", async () => {
    let finish!: (response: Response) => void;
    const documentResponse = new Promise<Response>(resolve => { finish = resolve; });
    window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=2&role=PurchaseOrder`);
    const { fetcher } = setup({ wrongPo: true, documentResponse });
    const edit = await screen.findByRole("button", { name: "Type an unsaved correction" });
    fireEvent.click(edit);
    await act(async () => finish(json([{ id: id(13), originalFilename: "Invoice.pdf", documentType: "invoice", status: "Ready" }])));
    await screen.findByText(/This file is identified as an invoice/);
    expect(screen.getByRole("button", { name: "Type an unsaved correction" })).toBe(edit);
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
    fireEvent.click(screen.getByRole("button", { name: "Change documents" }));
    expect(confirm).toHaveBeenCalledOnce();
    expect(screen.getByRole("heading", { name: "2. Check details" })).toBeTruthy();
    expect(writes(fetcher)).toHaveLength(0);
  });

  it("keeps a wrong-type upload receipt without saving it into the purchase-order slot", async () => {
    window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=1`);
    const { fetcher } = setup({ noPair: true, wrongUpload: true });
    fireEvent.change(await screen.findByLabelText("Upload purchase order"), {
      target: { files: [new File(["invoice"], "Invoice.pdf", { type: "application/pdf" })] },
    });
    await screen.findByText(/accepted and is kept in Documents/);
    expect(writes(fetcher)).toHaveLength(1);
    expect(String(writes(fetcher)[0][0])).toBe("http://test/api/documents");
    expect(screen.queryByRole("button", { name: "Retry upload" })).toBeNull();
  });

  it("does not allow a read-only session to pin or compare", async () => {
    const { fetcher } = setup({ readonly: true });
    await screen.findByText(`Inspecting ${id(10)}`);
    expect(screen.queryByRole("button", { name: "Continue to purchase order" })).toBeNull();
    expect(writes(fetcher)).toHaveLength(0);
  });

  it("returns to the requested document with browser Back without a mutation", async () => {
    const { fetcher } = setup();
    await screen.findByText(`Inspecting ${id(10)}`);
    await act(async () => {
      window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=2&role=PurchaseOrder`);
      window.dispatchEvent(new PopStateEvent("popstate"));
    });
    await screen.findByText(`Inspecting ${id(11)}`);
    expect(writes(fetcher)).toHaveLength(0);
  });
});

it("Home opens document selection without creating a check", async () => {
  window.history.replaceState(null, "", "/");
  const fetcher = vi.fn(async (url: string | URL | Request, init?: RequestInit) => {
    if (init?.method && init.method !== "GET") throw new Error("Home must not create a check.");
    return new URL(String(url)).pathname.endsWith("work-list") ? json(docList) : json({ cases: [], nextOffset: null });
  });
  vi.stubGlobal("fetch", fetcher);
  render(<InvoiceHome />);
  await screen.findByText("No saved checks yet. Your work will appear here after you choose both files and continue to review.");
  expect(screen.queryByRole("link", { name: "Document library" })).toBeNull();
  expect(screen.queryByRole("link", { name: "View all checks" })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Start with my files" }));
  expect(fixture.push).toHaveBeenCalledWith("/invoices/new");
  expect(writes(fetcher)).toHaveLength(0);
  expect(screen.queryByLabelText(/Case name/)).toBeNull();
});
