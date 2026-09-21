import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { DocumentDeletionControls } from "./document-deletion";

const id = "33333333-3333-3333-3333-333333333333";
const plan = { documentId: id, documentVersion: "document-version", policyVersion: "policy-version", enabled: true, blockers: [] as string[], extractionRuns: 1, extractionJobs: 1 };
const record = { ...plan, state: "PendingFileRemoval", requestedAtUtc: "2026-09-13T00:00:00Z", completedAtUtc: null };
let rows: typeof record[]; let blockers: string[]; let fetcher: ReturnType<typeof vi.fn>;
beforeEach(() => {
  rows = []; blockers = [];
  fetcher = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.includes("/deletions?")) return Response.json(rows);
    if (init?.method === "POST") { rows = [{ ...record, state: "Completed" }]; return Response.json(rows[0]); }
    return Response.json({ ...plan, blockers });
  }); vi.stubGlobal("fetch", fetcher);
});
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });
async function inspect() {
  render(<DocumentDeletionControls enabled/>); await screen.findByText("No deletion records on this page.");
  fireEvent.change(screen.getByLabelText("Document ID to inspect"), { target: { value: id } });
  fireEvent.click(screen.getByRole("button", { name: "Inspect deletion" }));
}
it("does not fetch or offer removal when disabled", () => {
  render(<DocumentDeletionControls enabled={false}/>);
  expect(fetcher).not.toHaveBeenCalled(); expect(screen.queryByRole("button")).toBeNull();
});
it("shows preservation blockers without a delete action", async () => {
  blockers = ["retention_hold_active", "historical_dependencies", "accounting_dependencies"];
  await inspect(); await screen.findByText("Deletion blocked");
  expect(screen.getByText("An active hold prevents removal.")).toBeTruthy();
  expect(screen.queryByRole("checkbox")).toBeNull(); expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});
it("requires inspection and confirmation and sends both original versions", async () => {
  await inspect(); const button = await screen.findByRole("button", { name: "Delete expired document" });
  expect((button as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole("checkbox")); fireEvent.click(button);
  await screen.findByText("Document deletion completed and audited.");
  expect(JSON.parse(String(fetcher.mock.calls.find(([, init]) => init?.method === "POST")![1]?.body))).toEqual({ expectedDocumentVersion: plan.documentVersion, expectedPolicyVersion: plan.policyVersion });
  expect(screen.queryByRole("checkbox")).toBeNull(); await screen.findByText("Completed");
});
it("resumes pending file removal with explicit confirmation", async () => {
  rows = [record]; render(<DocumentDeletionControls enabled/>);
  fireEvent.click(await screen.findByRole("button", { name: "Review pending removal" }));
  const button = screen.getByRole("button", { name: "Resume file removal" }); expect((button as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole("checkbox")); fireEvent.click(button);
  await screen.findByText("Document deletion completed and audited.");
});
it("clears approval and hides private transport details after a failed purge", async () => {
  await inspect(); const button = await screen.findByRole("button", { name: "Delete expired document" });
  fireEvent.click(screen.getByRole("checkbox")); fetcher.mockRejectedValueOnce(new Error("PRIVATE_STORAGE_TOKEN")); fireEvent.click(button);
  expect((await screen.findByRole("alert")).textContent).not.toContain("PRIVATE_STORAGE_TOKEN");
  await waitFor(() => expect((screen.getByRole("checkbox") as HTMLInputElement).checked).toBe(false));
  expect(screen.queryByText("Document deletion completed and audited.")).toBeNull();
});
it("invalidates an inspected target when its ID changes", async () => {
  await inspect(); await screen.findByText("Eligible for deletion"); fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.change(screen.getByLabelText("Document ID to inspect"), { target: { value: "44444444-4444-4444-4444-444444444444" } });
  expect(screen.queryByRole("checkbox")).toBeNull(); expect(screen.queryByRole("button", { name: "Delete expired document" })).toBeNull();
});
