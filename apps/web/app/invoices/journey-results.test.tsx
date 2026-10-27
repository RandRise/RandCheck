import { useEffect } from "react";
import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { InvoiceJourney } from "./components/invoice-journey";

const id = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
vi.mock("next/navigation", () => ({ usePathname: () => "/invoices" }));
vi.mock("../security/api-fetch", () => ({ API: "http://test", apiFetch: (url: string, init?: RequestInit) => fetch(url, init) }));
// Keep results, decisions, the workflow hook and services real. Only extraction UI is substituted.
vi.mock("../document-workspace", () => ({
  default: function ReviewStub({ documentId, onState, focusField }: {
    documentId: string; onState?: (value: object) => void;
    focusField?: { key: string; occurrence: number; extractionRunId?: string };
  }) {
    useEffect(() => onState?.({ review: { documentId, session: {
      id: "00000000-0000-4000-8000-000000000020", status: "Completed", revisionNumber: 1,
    } }, dirty: false, busy: false, unavailable: false }), [documentId, onState]);
    return <p>Current document {documentId}{focusField && ` field ${focusField.key} row ${focusField.occurrence} extraction ${focusField.extractionRunId}`}</p>;
  },
}));

function fixture() {
  return {
    id: id(1), name: "Invoice check", reference: "REC-1", category: "Uncategorized",
    status: "Evaluated", workflowKind: "InvoicePurchaseOrder", concurrencyToken: id(200),
    draftDocuments: [{ role: "Invoice", documentId: id(10) }, { role: "PurchaseOrder", documentId: id(11) }],
    documents: [{ id: id(13), role: "Invoice", documentId: id(10), reviewSessionId: id(20) }],
    runs: [{
      id: id(30), runNumber: 1, status: "Completed", ruleSetVersion: "invoice-po-2.0",
      sources: [{ id: id(40), documentId: id(10), role: "Invoice", extractionRunId: id(50) }],
      fields: [{ id: id(41), documentSnapshotId: id(40), role: "Invoice", key: "line_unit_price",
        occurrence: 7, value: "110", page: 1, quote: "Item X 110" }],
      findings: [{ id: id(31), ruleCode: "PAIR-NUMBER", severity: "Warning", explanation: "Prices differ",
        fieldKey: "line_unit_price", occurrence: 2, rightFieldSnapshotId: id(41),
        leftRole: "PurchaseOrder", rightRole: "Invoice", leftValue: "100", rightValue: "110",
        resolutions: [] as { id: string; action: string; reason: string; sequenceNumber: number; createdAt: string }[] }],
    }],
  };
}
function json(value: unknown, token = 200) {
  return new Response(JSON.stringify(value), { headers: { "Content-Type": "application/json", ETag: `"${id(token)}"` } });
}
function setup(replaced = false, concurrentPin = false, multipleRuns = false) {
  const saved = fixture();
  if (multipleRuns) saved.runs.push({ ...saved.runs[0], id: id(29), runNumber: 0 });
  if (replaced) saved.draftDocuments[0].documentId = id(99);
  let caseReads = 0;
  let finish!: () => void;
  const fetcher = vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url).pathname;
    if (path.endsWith("/resolutions")) return new Promise<Response>(resolve => {
      finish = () => {
        saved.runs[0].findings[0].resolutions.push({
          id: id(32), action: "Accepted", reason: "Agreed price", sequenceNumber: 1, createdAt: "2026-10-20T00:00:00Z",
        });
        resolve(json(saved, 201));
      };
    });
    if (init?.method === "PUT") return json(saved, 202);
    if (path === "/api/documents" || path.endsWith("/work-list")) return json([]);
    if (path === "/api/reconciliation-cases") return json({ cases: [saved], nextOffset: null });
    if (++caseReads > 1 && concurrentPin) {
      saved.documents[0].reviewSessionId = id(999);
      return json(saved, 205);
    }
    return json(saved);
  });
  vi.stubGlobal("fetch", fetcher);
  render(<InvoiceJourney canReview canOperate identity="test" />);
  return { fetcher, finish: () => finish() };
}

beforeEach(() => window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=3`));
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it("keeps a Details result selection separate from the workflow and preserves it through browser history", async () => {
  const { fetcher } = setup(false, false, true);
  const workflow = await screen.findByLabelText("Saved comparison");
  const originalUrl = window.location.href;
  fireEvent.click(screen.getByRole("button", { name: "See results details" }));
  const details = await screen.findByRole("main");
  const selection = await within(details).findByLabelText("Saved comparison");
  fireEvent.change(selection, { target: { value: id(29) } });
  expect((selection as HTMLSelectElement).value).toBe(id(29));
  expect(new URLSearchParams(window.location.search).get("run")).toBeNull();
  expect(within(details).queryByRole("button", { name: "Accepted" })).toBeNull();
  const detailsUrl = window.location.href;
  const reads = fetcher.mock.calls.length;
  fireEvent.click(screen.getByRole("button", { name: "Return to workflow" }));
  expect(screen.getByLabelText("Saved comparison")).toBe(workflow);
  expect((workflow as HTMLSelectElement).value).toBe(id(30));
  await act(async () => {
    window.history.replaceState(null, "", detailsUrl);
    window.dispatchEvent(new PopStateEvent("popstate"));
  });
  await within(screen.getByRole("main")).findByLabelText("Saved comparison");
  const afterDetailsReads = fetcher.mock.calls.length;
  const caseReads = () => fetcher.mock.calls.filter(([url]) => new URL(url).pathname === `/api/reconciliation-cases/${id(1)}`).length;
  const afterDetailsCaseReads = caseReads();
  expect(afterDetailsReads).toBeGreaterThan(reads); // Details may fetch, but the main results stay mounted.
  await act(async () => {
    window.history.replaceState(null, "", originalUrl);
    window.dispatchEvent(new PopStateEvent("popstate"));
  });
  expect(screen.getByLabelText("Saved comparison")).toBe(workflow);
  expect((workflow as HTMLSelectElement).value).toBe(id(30));
  // The parent may refresh library/list summaries on popstate; the mounted results must not reload.
  expect(caseReads()).toBe(afterDetailsCaseReads);
  expect(fetcher.mock.calls.filter(([, init]) => init?.method && init.method !== "GET")).toHaveLength(0);
});

it("keeps the selected result URL and decision draft when browser Back is cancelled", async () => {
  const { fetcher } = setup(false, false, true);
  fireEvent.change(await screen.findByLabelText("Saved comparison"), { target: { value: id(29) } });
  fireEvent.change(await screen.findByLabelText("Reason for PAIR-NUMBER choices"), { target: { value: "other" } });
  fireEvent.change(screen.getByLabelText("Reason for PAIR-NUMBER: Your explanation"), { target: { value: "Agreed price" } });
  const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
  const reads = fetcher.mock.calls.length;
  await act(async () => {
    window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=3`);
    window.dispatchEvent(new PopStateEvent("popstate"));
  });
  expect(confirm).toHaveBeenCalledOnce();
  expect(new URLSearchParams(window.location.search).get("run")).toBe(id(29));
  expect((screen.getByLabelText("Saved comparison") as HTMLSelectElement).value).toBe(id(29));
  expect((screen.getByLabelText("Reason for PAIR-NUMBER: Your explanation") as HTMLInputElement).value).toBe("Agreed price");
  expect(fetcher.mock.calls).toHaveLength(reads);
  expect(fetcher.mock.calls.filter(([, init]) => init?.method && init.method !== "GET")).toHaveLength(0);
});

it("blocks leaving during a decision save and uses its returned token for the next pin", async () => {
  const { fetcher, finish } = setup();
  fireEvent.change(await screen.findByLabelText("Reason for PAIR-NUMBER choices"), { target: { value: "other" } });
  fireEvent.change(screen.getByLabelText("Reason for PAIR-NUMBER: Your explanation"), { target: { value: "Agreed price" } });
  fireEvent.click(screen.getByRole("button", { name: "Accepted" }));
  await waitFor(() => expect(screen.getByRole("button", { name: "Continue checking" }).hasAttribute("disabled")).toBe(true));
  await act(async () => {
    window.history.replaceState(null, "", `/invoices?case=${id(1)}&step=2`);
    window.dispatchEvent(new PopStateEvent("popstate"));
  });
  expect(new URLSearchParams(window.location.search).get("step")).toBe("3");
  expect(screen.getByRole("heading", { name: "3. See results" })).toBeTruthy();
  await act(async () => finish());
  await screen.findByText("PAIR-NUMBER recorded as accepted.");
  fireEvent.click(screen.getByRole("button", { name: "Continue checking" }));
  await waitFor(() => expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(false));
  fireEvent.click(screen.getByRole("button", { name: "Continue to purchase order" }));
  await waitFor(() => expect(fetcher.mock.calls.some(([, init]) => init?.method === "PUT")).toBe(true));
  const pin = fetcher.mock.calls.find(([, init]) => init?.method === "PUT")!;
  expect((pin[1]?.headers as Record<string, string>)["If-Match"]).toBe(`"${id(201)}"`);
});

it("uses the invoice's frozen row and extraction rather than the PO row", async () => {
  setup();
  fireEvent.click(await screen.findByRole("button", { name: "Check this value" }));
  await screen.findByText(`Current document ${id(10)} field line_unit_price row 7 extraction ${id(50)}`);
});

it("keeps evidence visible when the result belongs to a replaced document", async () => {
  setup(true);
  fireEvent.click(await screen.findByRole("button", { name: "Check this value" }));
  await screen.findByText(/This result uses an earlier document/);
  expect(screen.getByRole("heading", { name: "3. See results" })).toBeTruthy();
  expect(screen.getByText("Item X 110")).toBeTruthy();
});

it("requires recovery and reinspection when results loaded another user's invoice pin", async () => {
  const { fetcher, finish } = setup(false, true);
  fireEvent.change(await screen.findByLabelText("Reason for PAIR-NUMBER choices"), { target: { value: "other" } });
  fireEvent.change(screen.getByLabelText("Reason for PAIR-NUMBER: Your explanation"), { target: { value: "Agreed price" } });
  fireEvent.click(screen.getByRole("button", { name: "Accepted" }));
  await waitFor(() => expect(screen.getByRole("button", { name: "Continue checking" }).hasAttribute("disabled")).toBe(true));
  await act(async () => finish());
  await screen.findByText(/saved documents changed while results were open/);
  fireEvent.click(screen.getByRole("button", { name: "Continue checking" }));
  expect(screen.getByRole("button", { name: "Continue to purchase order" }).hasAttribute("disabled")).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: "Check saved progress" }));
  await waitFor(() => expect(screen.queryByRole("button", { name: "Check saved progress" })).toBeNull());
  fireEvent.click(screen.getByRole("button", { name: "Purchase order" }));
  expect(screen.getByRole("button", { name: "Show results" }).hasAttribute("disabled")).toBe(true);
  expect(fetcher.mock.calls.filter(([, init]) => init?.method === "PUT")).toHaveLength(0);
  expect(fetcher.mock.calls.some(([url]) => url.endsWith("/evaluations"))).toBe(false);
});
