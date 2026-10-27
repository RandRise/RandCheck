import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { EnterpriseProvider, type Session } from "../security/enterprise-provider";
import ReconciliationPage from "./page";

vi.mock("next/navigation", () => ({ usePathname: () => "/reconciliation", useRouter: () => ({ replace: vi.fn() }) }));
const id = (n: number) => `00000000-0000-4000-8000-${n.toString().padStart(12, "0")}`;
const config = { mode: "Entra", tenantId: id(101), clientId: id(102), scope: `api://${id(103)}/DocumentIntelligence.Access`, api: "http://localhost:8080" };
const documents = [{ id: id(10), originalFilename: "Original invoice.txt", status: "Ready" }, { id: id(11), originalFilename: "Replacement invoice.txt", status: "Ready" }];
const decision = { id: id(35), sequenceNumber: 1, action: "Accepted", reason: "Original explanation", createdAt: "2026-09-30T08:00:00Z", actorId: id(106) };
const finding = { id: id(32), ruleCode: "TotalMismatch", severity: "Warning", explanation: "Totals differ", leftValue: "100", rightValue: "110", difference: 10, tolerance: .01, resolutions: [decision] };
const run = { id: id(30), runNumber: 1, status: "Completed", ruleSetVersion: "saved-rules", fields: [], findings: [finding] };
const savedCase = { id: id(1), name: "Existing procurement case", reference: "REC-2026-000001", category: "Equipment", status: "Evaluated", concurrencyToken: id(20),
  documents: [{ id: id(12), role: "Invoice", documentId: id(10), reviewSessionId: id(13), extractionRunId: id(14) }], runs: [run] };
function json(value: unknown, status = 200, etag = '"version-1"') { return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json", ETag: etag } }); }
function setup(role: string = "Staff", admin = false,
  override?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  const member = { organizationId: id(104), organizationName: "Example organization", actorId: id(105), displayName: "Test user", tenantId: config.tenantId, role };
  const fetcher = vi.fn((raw: string | URL | Request, init?: RequestInit) => {
    const url = new URL(String(raw));
    if (url.pathname.endsWith("/session")) return Promise.resolve(json(member));
    const response = override?.(url, init); if (response) return Promise.resolve(response);
    if (url.pathname === "/api/documents") return Promise.resolve(json(documents));
    if (url.pathname === "/api/reconciliation-cases") return Promise.resolve(json({ cases: [{ ...savedCase, createdAt: "2026-09-30T08:00:00Z", updatedAt: "2026-09-30T08:00:00Z", documentCount: 1, runCount: 1 }], nextOffset: null }));
    if (url.pathname === `/api/reconciliation-cases/${savedCase.id}`) return Promise.resolve(json(savedCase));
    if (url.pathname === `/api/documents/${id(11)}/review`) return Promise.resolve(json({ session: { id: id(15), status: "Completed" } }));
    throw new Error(`Unexpected fixture ${init?.method ?? "GET"} ${url.pathname}`);
  });
  vi.stubGlobal("fetch", fetcher);
  const identity = { initialize: vi.fn(async () => true), token: vi.fn(async () => "test-token"), signIn: vi.fn(async () => {}), signOut: vi.fn(async () => {}) };
  render(<EnterpriseProvider config={config} identityFactory={async () => identity}><ReconciliationPage/></EnterpriseProvider>);
  return fetcher;
}
function pending(name: string, key = id(91)) {
  sessionStorage.setItem(`case-creation:${id(104)}:${id(105)}`, JSON.stringify({ fingerprint: JSON.stringify([name, "Uncategorized"]), requestKey: key, workflowKind: "InvoicePurchaseOrder" }));
}
const writes = (fetcher: ReturnType<typeof setup>) => fetcher.mock.calls.filter(([, init]) => init?.method && init.method !== "GET");

describe("deliberate comparison actions and safe retries", () => {
  beforeEach(() => { window.history.replaceState(null, "", `/reconciliation?case=${savedCase.id}`); sessionStorage.clear(); });
  afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); sessionStorage.clear(); window.history.replaceState(null, "", "/reconciliation"); });

  it.each([
    { role: "Staff", admin: false, edit: true }, { role: "Staff", admin: true, edit: true },
    { role: "Staff", admin: false, edit: true }, { role: "Staff", admin: true, edit: true },
    { role: "Staff", admin: false, edit: true }, { role: "Staff", admin: true, edit: true },
  ] as const)("limits repeated-action controls by $role with administration=$admin", async ({ role, admin, edit }) => {
    const fetcher = setup(role, admin);
    await screen.findByRole("heading", { name: savedCase.name });
    for (const name of ["New comparison", "Revise decision", "Replace pinned review"])
      expect(!!screen.queryByRole("button", { name })).toBe(edit);
    expect(screen.queryByRole("button", { name: "Run comparison" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Accepted" })).toBeNull();
    expect(writes(fetcher)).toHaveLength(0);
  });

  it.each([false, true])("denies repeated workflow actions to None role with administration=%s", async admin => {
    const fetcher = setup("None", admin);
    await screen.findByRole("heading", { name: "Access needs attention." });
    expect(fetcher.mock.calls.some(([url]) => String(url).includes("/api/reconciliation-cases"))).toBe(false);
    expect(writes(fetcher)).toHaveLength(0);
  });

  it("requires explicit comparison intent, permits cancellation, and submits a deliberate rerun once", async () => {
    let finish!: (response: Response) => void;
    const fetcher = setup("Staff", false, (url, init) => url.pathname.endsWith("/evaluations") && init?.method === "POST" ? new Promise(resolve => { finish = resolve; }) : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "New comparison" }));
    fireEvent.click(screen.getByRole("button", { name: "Cancel new comparison" }));
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "New comparison" }));
    const form = screen.getByRole("button", { name: "Run comparison" }).closest("form")!;
    fireEvent.submit(form); fireEvent.submit(form);
    await waitFor(() => expect(writes(fetcher)).toHaveLength(1));
    await act(async () => { finish(json({ ...savedCase, runs: [{ ...run, id: id(31), runNumber: 2 }, run] }, 200, '"version-2"')); });
    await screen.findByText(/Evaluation 2 completed/);
    expect(screen.queryByRole("button", { name: "Run comparison" })).toBeNull();
    expect(screen.getByRole("button", { name: "New comparison" })).toBeTruthy();
    expect((screen.getByLabelText("Saved comparison") as HTMLSelectElement).value).toBe(id(31));
    expect(writes(fetcher)).toHaveLength(1);
  });

  it("blocks an identical decision and requires explicit revision for a changed explanation", async () => {
    const revised = { ...decision, id: id(36), sequenceNumber: 2, reason: "Additional evidence checked" };
    const fetcher = setup("Staff", false, (url, init) => url.pathname.endsWith("/resolutions") && init?.method === "POST"
      ? json({ ...savedCase, runs: [{ ...run, findings: [{ ...finding, resolutions: [decision, revised] }] }] }, 200, '"version-2"') : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "Revise decision" }));
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch: Your explanation"), { target: { value: "  Original explanation  " } });
    expect(screen.getByRole("button", { name: "Accepted" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Accepted" }));
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch: Your explanation"), { target: { value: revised.reason } });
    fireEvent.click(screen.getByRole("button", { name: "Accepted" }));
    await screen.findByText(/TotalMismatch recorded as accepted/);
    expect(screen.queryByLabelText("Reason for TotalMismatch: Your explanation")).toBeNull();
    expect(screen.getByRole("button", { name: "Revise decision" })).toBeTruthy();
    expect(screen.getByText("Decision history (2)")).toBeTruthy();
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body))).toEqual({ action: "Accepted", reason: revised.reason });
  });

  it("cancels a source replacement without writing and pins a chosen completed revision explicitly", async () => {
    const fetcher = setup("Admin", false, (url, init) => url.pathname.endsWith("/documents/Invoice") && init?.method === "PUT"
      ? json({ ...savedCase, documents: [{ ...savedCase.documents[0], documentId: id(11), reviewSessionId: id(15) }] }, 200, '"version-2"') : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "Replace pinned review" }));
    fireEvent.click(screen.getByRole("button", { name: "Cancel replacement" }));
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "Replace pinned review" }));
    fireEvent.change(screen.getByLabelText("Invoice document"), { target: { value: id(11) } });
    fireEvent.click(screen.getByRole("button", { name: "Pin replacement" }));
    await screen.findByText(/Invoice pinned review replaced/);
    expect(writes(fetcher)).toHaveLength(1);
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body))).toEqual({ documentId: id(11), reviewSessionId: id(15) });
    expect(screen.getByText("Decision history (1)")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Pin replacement" })).toBeNull();
  });

  it("recovers a stored earlier creation using its original key and legacy payload", async () => {
    window.history.replaceState(null, "", "/reconciliation");
    const requestKey = id(90);
    sessionStorage.setItem(`case-creation:${id(104)}:${id(105)}`, JSON.stringify({
      fingerprint: JSON.stringify(["Earlier pending case", "Uncategorized"]), requestKey,
    }));
    const fetcher = setup("Staff", false, (url, init) => url.pathname === "/api/reconciliation-cases" && init?.method === "POST"
      ? json({ ...savedCase, name: "Earlier pending case" }, 201) : undefined);
    fireEvent.change(await screen.findByLabelText("Case name"), { target: { value: "Earlier pending case" } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByText("Earlier case recovered. Its saved sources and history remain available.");
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body))).toEqual({ name: "Earlier pending case", category: "Uncategorized", requestKey });
    expect(screen.getByLabelText("Saved comparison")).toBeTruthy();
  });

  it("retains the earlier request key after uncertain failure and rejects fresh creation after recovery", async () => {
    window.history.replaceState(null, "", "/reconciliation");
    pending("Repeatable title");
    let attempts = 0;
    const fetcher = setup("Staff", false, (url, init) => {
      if (url.pathname === "/api/reconciliation-cases" && init?.method === "POST") {
        if (++attempts === 1) return Promise.reject(new Error("Connection lost after submission"));
        return json({ ...savedCase, name: "Repeatable title", documents: [], runs: [] }, 201);
      }
    });
    fireEvent.change(await screen.findByLabelText("Case name"), { target: { value: "Repeatable title" } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByText(/Connection lost after submission/);
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByRole("heading", { name: "Repeatable title" });
    const bodies = () => writes(fetcher).map(([, init]) => JSON.parse(String(init?.body)));
    expect(bodies()[0].requestKey).toBeTruthy(); expect(bodies()[1].requestKey).toBe(bodies()[0].requestKey);
    fireEvent.click(screen.getByRole("button", { name: "All cases" }));
    fireEvent.change(await screen.findByLabelText("Case name"), { target: { value: "Repeatable title" } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByText(/No matching earlier request/);
    expect(bodies()).toHaveLength(2);
  });

  it("does not write when the chosen document has the exact review version already pinned", async () => {
    const fetcher = setup("Staff", false, url => url.pathname === `/api/documents/${id(10)}/review`
      ? json({ session: { id: id(13), status: "Completed" } }) : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "Replace pinned review" }));
    fireEvent.click(screen.getByRole("button", { name: "Pin replacement" }));
    await screen.findByText(/That review version is already pinned/);
    expect(writes(fetcher)).toHaveLength(0);
    expect(screen.queryByRole("button", { name: "Pin replacement" })).toBeNull();
    expect(screen.getByText("Decision history (1)")).toBeTruthy();
  });

  it("keeps decision and source drafts readable when a stale reload discovers a closed case", async () => {
    let reads = 0;
    const fetcher = setup("Staff", false, (url, init) => {
      if (url.pathname.endsWith("/resolutions") && init?.method === "POST") return json({}, 412);
      if (url.pathname === `/api/reconciliation-cases/${savedCase.id}`) return json({ ...savedCase, status: ++reads === 1 ? "Evaluated" : "Closed" });
    });
    fireEvent.click(await screen.findByRole("button", { name: "Replace pinned review" }));
    fireEvent.change(screen.getByLabelText("Invoice document"), { target: { value: id(11) } });
    fireEvent.click(screen.getByRole("button", { name: "Revise decision" }));
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch: Your explanation"), { target: { value: "Pending explanation to preserve" } });
    fireEvent.click(screen.getByRole("button", { name: "Resolved" }));
    fireEvent.click(await screen.findByRole("button", { name: "Reload case" }));
    const decisions = await screen.findByRole("region", { name: "Unsaved decision drafts" });
    expect(within(decisions).getByText("Pending explanation to preserve")).toBeTruthy();
    expect(within(screen.getByRole("region", { name: "Unsaved source selections" })).getByText("Replacement invoice.txt")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Resolved" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Pin replacement" })).toBeNull();
    expect(writes(fetcher)).toHaveLength(1);
  });

  it("requires an explicit reload if a successful mutation response body is lost", async () => {
    const fetcher = setup("Staff", false, (url, init) => {
      if (url.pathname.endsWith("/resolutions") && init?.method === "POST") {
        const response = json({});
        vi.spyOn(response, "json").mockRejectedValue(new Error("Response body interrupted"));
        return response;
      }
    });
    fireEvent.click(await screen.findByRole("button", { name: "Revise decision" }));
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch: Your explanation"), { target: { value: "Retain after uncertain response" } });
    fireEvent.click(screen.getByRole("button", { name: "Resolved" }));
    await screen.findByRole("button", { name: "Reload case" });
    expect(screen.getByRole("button", { name: "Resolved" }).hasAttribute("disabled")).toBe(true);
    expect((screen.getByLabelText("Reason for TotalMismatch: Your explanation") as HTMLInputElement).value).toBe("Retain after uncertain response");
    expect(writes(fetcher)).toHaveLength(1);
  });

  it("preserves chosen comparison settings across uncertain failure and reload without rerunning", async () => {
    const fetcher = setup("Staff", false, (url, init) => url.pathname.endsWith("/evaluations") && init?.method === "POST"
      ? Promise.reject(new Error("Lost evaluation response")) : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "New comparison" }));
    fireEvent.click(screen.getByText("Settings", { selector: "summary" }));
    const quantity = screen.getByRole("spinbutton", { name: "Quantity" });
    fireEvent.change(quantity, { target: { value: "3.5" } });
    fireEvent.click(screen.getByRole("button", { name: "Run comparison" }));
    fireEvent.click(await screen.findByRole("button", { name: "Reload case" }));
    await screen.findByText(/Latest saved case loaded/);
    expect((screen.getByRole("spinbutton", { name: "Quantity" }) as HTMLInputElement).value).toBe("3.5");
    expect(writes(fetcher)).toHaveLength(1);
    expect(screen.getByRole("button", { name: "Run comparison" }).hasAttribute("disabled")).toBe(false);
  });

  it("requires metadata reload after an uncertain save and retains the edited name", async () => {
    const fetcher = setup("Staff", false, (url, init) => url.pathname === `/api/reconciliation-cases/${savedCase.id}` && init?.method === "PUT"
      ? Promise.reject(new Error("Lost details response")) : undefined);
    fireEvent.click(await screen.findByRole("button", { name: "Edit case details" }));
    const form = within(screen.getByRole("form", { name: "Case details" }));
    fireEvent.change(form.getByLabelText("Case name"), { target: { value: "Edited draft name" } });
    fireEvent.click(form.getByRole("button", { name: "Save case details" }));
    expect(await screen.findByRole("button", { name: "Reload case details" })).toBeTruthy();
    expect(form.getByRole("button", { name: "Save case details" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Reload case details" }));
    await screen.findByText(/Latest saved case loaded/);
    expect((form.getByLabelText("Case name") as HTMLInputElement).value).toBe("Edited draft name");
    expect(writes(fetcher)).toHaveLength(1);
  });

  it("reloads a stale decision without automatic resubmission or loss of its draft", async () => {
    let reads = 0; let mutations = 0;
    const fetcher = setup("Staff", false, (url, init) => {
      if (url.pathname.endsWith("/resolutions") && init?.method === "POST") return ++mutations === 1 ? json({}, 412)
        : json({ ...savedCase, runs: [{ ...run, findings: [{ ...finding, resolutions: [decision, { ...decision, id: id(36), sequenceNumber: 2, action: "Resolved", reason: "Local revised explanation" }] }] }] }, 200, '"saved-version"');
      if (url.pathname === `/api/reconciliation-cases/${savedCase.id}`) return json(savedCase, 200, ++reads === 1 ? '"version-1"' : '"refreshed-version"');
    });
    fireEvent.click(await screen.findByRole("button", { name: "Revise decision" }));
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByLabelText("Reason for TotalMismatch: Your explanation"), { target: { value: "Local revised explanation" } });
    fireEvent.click(screen.getByRole("button", { name: "Resolved" }));
    fireEvent.click(await screen.findByRole("button", { name: "Reload case" }));
    await waitFor(() => expect(reads).toBe(2));
    await waitFor(() => expect(screen.getByRole("button", { name: "Resolved" }).hasAttribute("disabled")).toBe(false));
    expect((screen.getByLabelText("Reason for TotalMismatch: Your explanation") as HTMLInputElement).value).toBe("Local revised explanation");
    expect(mutations).toBe(1);
    fireEvent.click(screen.getByRole("button", { name: "Resolved" }));
    await screen.findByText(/TotalMismatch recorded as resolved/);
    expect(new Headers(writes(fetcher)[1][1]?.headers).get("If-Match")).toBe('"refreshed-version"');
  });

  it("recovers an uncertain case creation with the same key after remount in the same account", async () => {
    window.history.replaceState(null, "", "/reconciliation");
    pending("Recover after reload");
    let firstKey = "";
    setup("Staff", false, (url, init) => {
      if (url.pathname === "/api/reconciliation-cases" && init?.method === "POST") {
        firstKey = JSON.parse(String(init.body)).requestKey;
        return Promise.reject(new Error("Creation response was lost"));
      }
    });
    fireEvent.change(await screen.findByLabelText("Case name"), { target: { value: "Recover after reload" } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByText(/Creation response was lost/);
    cleanup();
    const fetcher = setup("Staff", false, (url, init) => url.pathname === "/api/reconciliation-cases" && init?.method === "POST"
      ? json({ ...savedCase, name: "Recover after reload", documents: [], runs: [] }, 201) : undefined);
    fireEvent.change(await screen.findByLabelText("Case name"), { target: { value: "Recover after reload" } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByRole("heading", { name: "Recover after reload" });
    expect(firstKey).toBeTruthy();
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body)).requestKey).toBe(firstKey);
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body)).workflowKind).toBe("InvoicePurchaseOrder");
  });

  it("does not let an older successful creation erase the retry identity of a newer pending creation", async () => {
    window.history.replaceState(null, "", "/reconciliation");
    pending("First submission");
    let completeFirst!: (response: Response) => void;
    let failSecond!: (error: Error) => void;
    let attempts = 0;
    const fetcher = setup("Staff", false, (url, init) => {
      if (url.pathname !== "/api/reconciliation-cases" || init?.method !== "POST") return;
      if (++attempts === 1) return new Promise(resolve => { completeFirst = resolve; });
      if (attempts === 2) return new Promise((_, reject) => { failSecond = reject; });
      return json({ ...savedCase, name: "Second submission", documents: [], runs: [] }, 201);
    });
    fireEvent.change(await screen.findByLabelText("Case name"), { target: { value: "First submission" } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await waitFor(() => expect(writes(fetcher)).toHaveLength(1));
    fireEvent.click(await screen.findByRole("link", { name: savedCase.name }));
    await screen.findByRole("heading", { name: savedCase.name });
    fireEvent.click(screen.getByRole("button", { name: "All cases" }));
    pending("Second submission", id(92));
    fireEvent.change(await screen.findByLabelText("Case name"), { target: { value: "Second submission" } });
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await waitFor(() => expect(writes(fetcher)).toHaveLength(2));
    const secondKey = JSON.parse(String(writes(fetcher)[1][1]?.body)).requestKey;
    await act(async () => { completeFirst(json({ ...savedCase, id: id(2), name: "First submission" }, 201)); });
    await act(async () => { failSecond(new Error("Second response lost")); });
    await screen.findByText(/Second response lost/);
    if (!screen.getByText("Recover an earlier request").closest("details")?.open) fireEvent.click(screen.getByText("Recover an earlier request"));
    fireEvent.click(screen.getByRole("button", { name: "Recover request" }));
    await screen.findByRole("heading", { name: "Second submission" });
    expect(JSON.parse(String(writes(fetcher)[2][1]?.body)).requestKey).toBe(secondKey);
  });
});
