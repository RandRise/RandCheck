import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import ReconciliationPage from "./page";

const caseId = "11111111-1111-4111-8111-111111111111";
const existingRun = { id: "22222222-2222-4222-8222-222222222222", runNumber: 1, status: "Completed", ruleSetVersion: "1.0", fields: [], findings: [] };
const caseItem = { id: caseId, name: "Default comparison", status: "Draft", concurrencyToken: "version",
  documents: [{ id: "link", role: "Invoice", documentId: "document", reviewSessionId: "review", extractionRunId: "extraction" }], runs: [] as typeof existingRun[] };
const json = (value: unknown) => new Response(JSON.stringify(value), { headers: { "Content-Type": "application/json", ETag: '"version"' } });
function setup(saved = false) {
  const item = saved ? { ...caseItem, status: "Evaluated", runs: [existingRun] } : caseItem;
  const fetcher = vi.fn((raw: string, init?: RequestInit) => {
    const url = new URL(raw);
    if (url.pathname === "/api/documents") return Promise.resolve(json([]));
    if (url.pathname === "/api/reconciliation-cases") return Promise.resolve(json({ cases: [item], nextOffset: null }));
    if (url.pathname === `/api/reconciliation-cases/${caseId}`) return Promise.resolve(json(item));
    if (url.pathname.endsWith("/evaluations") && init?.method === "POST") return Promise.resolve(json({ ...item, status: "Evaluated", runs: [existingRun] }));
    throw new Error(`Unexpected request ${url.pathname}`);
  }); vi.stubGlobal("fetch", fetcher); render(<ReconciliationPage/>); return fetcher;
}
const writes = (fetcher: ReturnType<typeof setup>) => fetcher.mock.calls.filter(([, init]) => init?.method === "POST");
beforeEach(() => window.history.replaceState(null, "", `/reconciliation?case=${caseId}`));
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe("comparison settings disclosure", () => {
  it("runs with established defaults while technical controls stay collapsed", async () => {
    const fetcher = setup(); const run = await screen.findByRole("button", { name: "Run comparison" });
    const settings = screen.getByText("Settings", { selector: "summary" }).closest("details")!;
    expect(settings.open).toBe(false);
    expect(screen.getByRole("spinbutton", { name: "Quantity" }).closest("details")).toBe(settings);
    fireEvent.submit(run.closest("form")!); await screen.findByText(/Evaluation 1 completed/);
    expect(writes(fetcher)).toHaveLength(1);
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body))).toEqual({ quantityTolerance: 0, unitPriceTolerance: .01,
      taxTolerance: .01, lineTotalTolerance: .01, documentTotalTolerance: .01 });
    expect(new Headers(writes(fetcher)[0][1]?.headers).get("If-Match")).toBe('"version"');
  });

  it("retains edited tolerances when settings are collapsed and sends them in the deliberate run", async () => {
    const fetcher = setup(); await screen.findByRole("button", { name: "Run comparison" });
    const settings = screen.getByText("Settings", { selector: "summary" }); fireEvent.click(settings);
    fireEvent.change(screen.getByRole("spinbutton", { name: "Document total" }), { target: { value: "1.25" } });
    fireEvent.click(settings); expect(settings.closest("details")!.open).toBe(false); expect(writes(fetcher)).toHaveLength(0);
    fireEvent.submit(screen.getByRole("button", { name: "Run comparison" }).closest("form")!);
    await screen.findByText(/Evaluation 1 completed/);
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body)).documentTotalTolerance).toBe(1.25);
  });

  it("cancels an edited new comparison without changing its saved snapshot", async () => {
    const fetcher = setup(true); fireEvent.click(await screen.findByRole("button", { name: "New comparison" }));
    fireEvent.click(screen.getByText("Settings", { selector: "summary" }));
    fireEvent.change(screen.getByRole("spinbutton", { name: "Quantity" }), { target: { value: "3.5" } });
    fireEvent.click(screen.getByRole("button", { name: "Cancel new comparison" }));
    expect(screen.queryByRole("button", { name: "Run comparison" })).toBeNull(); expect(writes(fetcher)).toHaveLength(0);
    expect(screen.getByRole("heading", { name: /Saved comparison 1/ })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "New comparison" }));
    expect(screen.getByText("Settings", { selector: "summary" }).closest("details")!.open).toBe(false);
    fireEvent.click(screen.getByText("Settings", { selector: "summary" }));
    expect((screen.getByRole("spinbutton", { name: "Quantity" }) as HTMLInputElement).value).toBe("0");
  });

  it("reopens settings when a collapsed invalid tolerance blocks native submission", async () => {
    const fetcher = setup(); const run = await screen.findByRole("button", { name: "Run comparison" });
    const summary = screen.getByText("Settings", { selector: "summary" }); fireEvent.click(summary);
    const quantity = screen.getByRole("spinbutton", { name: "Quantity" });
    fireEvent.change(quantity, { target: { value: "-1" } }); fireEvent.click(summary);
    expect(summary.closest("details")!.open).toBe(false);
    fireEvent.click(run);
    expect(writes(fetcher)).toHaveLength(0); expect(summary.closest("details")!.open).toBe(true);
    expect(screen.getByRole("spinbutton", { name: "Quantity" })).toBe(quantity);
  });
});
