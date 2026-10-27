import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import ReconciliationPage from "./page";
import type { Session } from "../security/enterprise-provider";

const access = vi.hoisted(() => ({ role: "Staff", admin: false }));
vi.mock("../security/enterprise-provider", async importOriginal => ({
  ...await importOriginal<typeof import("../security/enterprise-provider")>(),
  useEnterprise: () => ({ session: { organizationId: "org", actorId: "actor", role: access.role,
    canManageMembers: access.admin, tenantId: "tenant", displayName: "Tester", organizationName: "Test" } as Session }),
}));
const id = "11111111-1111-4111-8111-111111111111";
const documentId = "22222222-2222-4222-8222-222222222222";
function run(number: number) {
  return { id: `run-${number}`, runNumber: number, status: "Completed", ruleSetVersion: "1.0",
    fields: [{ id: `snapshot-${number}`, key: "total", occurrence: 0, value: `${number}00`, page: number + 1,
      quote: number === 1 ? "القيمة الأصلية 100" : "New comparison source 200" }],
    findings: [{ id: `finding-${number}`, ruleCode: `AMT-00${number}`, severity: "Error", leftRole: "Invoice",
      leftFieldSnapshotId: `snapshot-${number}`, leftValue: `${number}00`, explanation: `Saved difference ${number}`,
      resolutions: [{ id: `decision-${number}`, sequenceNumber: 1, action: "Accepted", reason: `Original reason ${number}`,
        actorId: "previous-actor", createdAt: "2026-09-30T08:00:00Z" }] }] };
}
function savedCase() { return { id, name: "Procurement example", status: "Evaluated", concurrencyToken: "v1",
  documents: [{ id: "link", role: "Invoice", documentId, reviewSessionId: "pinned-review", extractionRunId: "pinned-extraction" }],
  runs: [run(2), run(1)] }; }
function json(value: unknown, status = 200, etag = '"case-v1"') {
  return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json", ETag: etag } });
}
function requests(override?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  const fetcher = vi.fn((raw: string, init?: RequestInit) => {
    const url = new URL(raw); const replacement = override?.(url, init); if (replacement) return Promise.resolve(replacement);
    if (url.pathname === "/api/documents") return Promise.resolve(json([{ id: documentId, originalFilename: "Current invoice.pdf", status: "Ready" }]));
    if (url.pathname === "/api/reconciliation-cases") return Promise.resolve(json({ cases: [{ ...savedCase(), documentCount: 1, runCount: 2, createdAt: "2026-09-30T08:00:00Z" }], nextOffset: null }));
    if (url.pathname === `/api/reconciliation-cases/${id}`) return Promise.resolve(json(savedCase()));
    throw new Error(`Unexpected request ${init?.method ?? "GET"} ${url}`);
  });
  vi.stubGlobal("fetch", fetcher); return fetcher;
}
beforeEach(() => { access.role = "Staff"; access.admin = false; window.history.replaceState(null, "", `/reconciliation?case=${id}`); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); });

describe("comparison roles and saved context", () => {
  it.each([
    { actorId: "22222222-2222-2222-2222-222222222222", expected: "Development user" },
    { actorId: "previous-actor", expected: "previous-actor" },
    { actorId: "", expected: null },
  ])("shows only known decision attribution without changing history: $actorId", async ({ actorId, expected }) => {
    const saved = savedCase();
    saved.runs[0].findings[0].resolutions[0].actorId = actorId;
    const original = JSON.stringify(saved);
    const fetcher = requests(url => url.pathname === `/api/reconciliation-cases/${id}` ? json(saved) : undefined);
    render(<ReconciliationPage />);
    await screen.findByLabelText("Saved comparison");
    fireEvent.click(screen.getByText("Decision history (1)"));
    await screen.findByText("Original reason 2", { selector: "li bdi" });

    if (expected) expect(screen.getByText(expected)).toBeTruthy();
    if (expected === "Development user") {
      expect(screen.getByTitle(`User ID: ${actorId}`).textContent).toContain("(test identity)");
    } else {
      expect(screen.queryByText("Development user")).toBeNull();
    }
    if (!actorId) expect(screen.queryByText(/Recorded by/)).toBeNull();
    expect(screen.queryByText("Tester")).toBeNull();
    expect(JSON.stringify(saved)).toBe(original);
    expect(fetcher.mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
  });

  it.each(["Staff", "Admin", "Viewer", "None"].flatMap(role => [false, true].map(admin => ({ role, admin }))))(
    "$role with administrator=$admin gets only its permitted comparison actions", async ({ role, admin }) => {
      access.role = role; access.admin = admin;
      const fetcher = requests(); render(<ReconciliationPage/>);
      if (!["Staff", "Admin"].includes(role)) {
        expect(screen.getByRole("heading", { name: "Comparison access required" })).toBeTruthy();
        await Promise.resolve(); expect(fetcher).not.toHaveBeenCalled(); return;
      }
      await screen.findByRole("heading", { name: "Procurement example" });
      expect(screen.getByLabelText("Saved comparison")).toBeTruthy();
      expect(screen.getByText("New comparison source 200")).toBeTruthy();
      const editable = role !== "Viewer";
      for (const name of ["New comparison", "Revise decision", "Replace pinned review"]) {
        expect(!!screen.queryByRole("button", { name })).toBe(editable);
      }
      expect(!!screen.queryByLabelText("Quotation document")).toBe(editable);
      fireEvent.click(screen.getByRole("button", { name: "All cases" }));
      await screen.findByRole("region", { name: "Saved cases" });
      await screen.findByRole("link", { name: "Procurement example" });
      if (editable) fireEvent.click(screen.getByText("Recover an earlier request"));
      expect(!!screen.queryByRole("button", { name: "Recover request" })).toBe(editable);
      expect(fetcher.mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
    });

  it.each(["Staff", "Admin"])("%s reopens, pins, evaluates and decides in an earlier case", async role => {
    access.role = role; window.history.replaceState(null, "", `/reconciliation?case=${id}`);
    const empty = { ...savedCase(), documents: [], runs: [] as ReturnType<typeof run>[] };
    const linked = { ...empty, documents: savedCase().documents };
    const fetcher = requests((url, init) => {
      if (url.pathname === `/api/reconciliation-cases/${id}` && !init?.method) return json(empty);
      if (url.pathname.endsWith("/review")) return json({ session: { id: "pinned-review", status: "Completed" } });
      if (init?.method === "PUT") return json(linked, 200, '"case-linked"');
      if (url.pathname.endsWith("/evaluations") && init?.method === "POST") return json(savedCase(), 200, '"case-evaluated"');
      if (url.pathname.endsWith("/resolutions") && init?.method === "POST") return json(savedCase(), 200, '"case-decided"');
    });
    render(<ReconciliationPage/>);
    await screen.findByLabelText("Invoice document");
    fireEvent.change(screen.getByLabelText("Invoice document"), { target: { value: documentId } });
    const pin = screen.getAllByRole("button", { name: "Pin review" }).find(button => !(button as HTMLButtonElement).disabled)!;
    fireEvent.click(pin); await screen.findByText("Invoice review pinned to the case.");
    fireEvent.click(screen.getByRole("button", { name: "Run comparison" }));
    await screen.findByText("Evaluation 2 completed with 1 findings.");
    fireEvent.click(screen.getByRole("button", { name: "Revise decision" }));
    fireEvent.change(screen.getByLabelText("Reason for AMT-002 choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByLabelText("Reason for AMT-002: Your explanation"), { target: { value: "Checked original source" } });
    fireEvent.click(screen.getByRole("button", { name: "Resolved" }));
    await screen.findByText("AMT-002 recorded as resolved.");
    const writes = fetcher.mock.calls.filter(([, init]) => init?.method && init.method !== "GET");
    expect(writes.map(([, init]) => init?.method)).toEqual(["PUT", "POST", "POST"]);
    expect(JSON.parse(String(writes[0][1]?.body))).toEqual({ documentId, reviewSessionId: "pinned-review" });
    expect(new Headers(writes[1][1]?.headers).get("If-Match")).toBe('"case-linked"');
    expect(new Headers(writes[2][1]?.headers).get("If-Match")).toBe('"case-evaluated"');
    expect(writes[2][0]).toContain("/runs/run-2/findings/finding-2/resolutions");
  });

  it("shows the selected run's own evidence and history and preserves other-run reason drafts", async () => {
    const fetcher = requests((url, init) => url.pathname.endsWith("/resolutions") && init?.method === "POST" ? json(savedCase(), 200, '"case-v2"') : undefined);
    render(<ReconciliationPage/>); await screen.findByLabelText("Saved comparison");
    fireEvent.click(screen.getByRole("button", { name: "Revise decision" }));
    fireEvent.change(screen.getByLabelText("Reason for AMT-002 choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByLabelText("Reason for AMT-002: Your explanation"), { target: { value: "Unsaved newest decision" } });
    fireEvent.change(screen.getByLabelText("Saved comparison"), { target: { value: "run-1" } });
    expect(screen.getByRole("blockquote").textContent).toBe("القيمة الأصلية 100");
    expect(screen.queryByText("New comparison source 200")).toBeNull();
    expect(screen.getByText("Page 2")).toBeTruthy();
    fireEvent.click(screen.getByText("Decision history (1)"));
    await waitFor(() => expect(screen.getByText(/Original reason 1/, { selector: "li bdi" })).toBeTruthy());
    expect(screen.getByText("Source page unavailable")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Revise decision" }));
    fireEvent.change(screen.getByLabelText("Reason for AMT-001 choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByLabelText("Reason for AMT-001: Your explanation"), { target: { value: "Decision about older snapshot" } });
    fireEvent.click(screen.getByRole("button", { name: "Dismissed" }));
    await screen.findByText("AMT-001 recorded as dismissed.");
    expect((screen.getByLabelText("Saved comparison") as HTMLSelectElement).value).toBe("run-1");
    expect(fetcher.mock.calls.find(([url]) => url.endsWith("/resolutions"))?.[0]).toContain("/runs/run-1/findings/finding-1/");
    fireEvent.change(screen.getByLabelText("Saved comparison"), { target: { value: "run-2" } });
    expect((screen.getByLabelText("Reason for AMT-002: Your explanation") as HTMLInputElement).value).toBe("Unsaved newest decision");
  });

  it("clears private comparison state when document access is removed", async () => {
    requests(); const view = render(<ReconciliationPage/>); await screen.findByLabelText("Saved comparison");
    access.role = "None"; access.admin = true; view.rerender(<ReconciliationPage/>);
    expect(screen.queryByText("Procurement example")).toBeNull();
    expect(screen.queryByLabelText("Saved comparison")).toBeNull();
    await waitFor(() => expect(screen.getByRole("heading", { name: "Comparison access required" })).toBeTruthy());
  });

  it("reopens an older saved comparison after refresh and browser Back", async () => {
    requests(); const view = render(<ReconciliationPage/>); await screen.findByLabelText("Saved comparison");
    fireEvent.change(screen.getByLabelText("Saved comparison"), { target: { value: "run-1" } });
    expect(new URLSearchParams(location.search).get("run")).toBe("run-1");
    view.unmount(); render(<ReconciliationPage/>);
    await waitFor(() => expect((screen.getByLabelText("Saved comparison") as HTMLSelectElement).value).toBe("run-1"));
    expect(screen.getByRole("blockquote").textContent).toBe("القيمة الأصلية 100");
    fireEvent.change(screen.getByLabelText("Saved comparison"), { target: { value: "run-2" } });
    act(() => window.history.back());
    await waitFor(() => expect((screen.getByLabelText("Saved comparison") as HTMLSelectElement).value).toBe("run-1"));
    expect(screen.queryByText("New comparison source 200")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "All cases" }));
    expect(new URLSearchParams(location.search).has("run")).toBe(false);
  });

  it.each(["invalid", "other-case-run"])("does not silently show latest for an unavailable run: %s", async runId => {
    window.history.replaceState(null, "", `/reconciliation?case=${id}&run=${runId}`);
    requests(); render(<ReconciliationPage/>);
    await screen.findByText("The requested saved comparison is invalid or unavailable in this case.");
    expect(screen.queryByLabelText("Saved comparison")).toBeNull();
    expect(screen.queryByText("New comparison source 200")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Show latest comparison" }));
    expect((screen.getByLabelText("Saved comparison") as HTMLSelectElement).value).toBe("run-2");
    expect(new URLSearchParams(location.search).has("run")).toBe(false);
  });
});
