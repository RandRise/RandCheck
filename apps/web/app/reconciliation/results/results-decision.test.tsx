import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import ReconciliationWorkspace from "../reconciliation-workspace";
import { CaseRun } from "./results.types";

vi.mock("../../security/api-fetch", () => ({ API: "http://test", apiFetch: (url: string, init?: RequestInit) => fetch(url, init) }));
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

it("saves an explicit missing-check reason through the existing endpoint without changing the outcome", async () => {
  const id = "00000000-0000-4000-8000-000000000001";
  const run: CaseRun = {
    id: "run", runNumber: 1, status: "Completed", ruleSetVersion: "invoice-po-2.0", fields: [],
    coverage: { status: "Incomplete", conclusion: "", couldNotCheck: 1 },
    checks: [{ id: "check", fieldKey: "total", status: "CouldNotCheck", reasonCode: "amount_basis_unavailable", explanation: "Tax treatment not known", findingId: "finding" }],
    findings: [{ id: "finding", ruleCode: "PAIR-NUMBER", fieldKey: "total", severity: "Warning", explanation: "Tax treatment not known", resolutions: [] }],
  };
  const item = { id, name: "Test pair", workflowKind: "InvoicePurchaseOrder", status: "Evaluated", documents: [], runs: [run], concurrencyToken: id };
  const requests: RequestInit[] = [];
  vi.stubGlobal("fetch", vi.fn(async (_url: string, init?: RequestInit) => {
    if (init?.method === "POST") {
      requests.push(init);
      const body = JSON.parse(String(init.body));
      run.findings[0].resolutions.push({ id: "decision", sequenceNumber: 1, action: body.action, reason: body.reason, createdAt: "2026-10-23T00:00:00Z" });
    }
    return new Response(JSON.stringify(item), { headers: { "Content-Type": "application/json", ETag: `"${id}"` } });
  }));
  window.history.replaceState(null, "", "/invoices");
  render(<ReconciliationWorkspace canReview creationScope="test" embedded fixedCaseId={id}/>);
  const selection = await screen.findByLabelText("Reason for Document total choices");
  expect((selection as HTMLSelectElement).value).toBe("");
  expect(screen.getByRole("button", { name: "Acknowledge missing check" }).hasAttribute("disabled")).toBe(true);
  fireEvent.change(selection, { target: { value: "Requested the missing information" } });
  fireEvent.click(screen.getByRole("button", { name: "Acknowledge missing check" }));
  await screen.findByRole("button", { name: "Revise decision" });
  expect(requests).toHaveLength(1);
  expect(JSON.parse(String(requests[0].body))).toEqual({ action: "Accepted", reason: "Requested the missing information" });
  expect(screen.getByRole("heading", { name: "Checks incomplete" })).toBeTruthy();
  expect(screen.getByLabelText("Check counts").textContent).toBe("0 Passed0 Differed1 Could not be checked1 Total checks");
  fireEvent.click(screen.getByRole("button", { name: "Revise decision" }));
  await waitFor(() => expect(screen.getByRole("button", { name: "Acknowledge missing check" }).hasAttribute("disabled")).toBe(true));
});
