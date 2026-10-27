import { clearStorageRecovery } from "./storage-recovery";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
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
afterEach(() => { clearStorageRecovery("::deletion"); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
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
  await waitFor(() => expect(screen.queryByRole("checkbox")).toBeNull());
  expect(screen.getByRole("button", { name: "Inspect deletion" }).matches(":disabled")).toBe(true);
  expect(screen.queryByText("Document deletion completed and audited.")).toBeNull();
});
it("invalidates an inspected target when its ID changes", async () => {
  await inspect(); await screen.findByText("Eligible for deletion"); fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.change(screen.getByLabelText("Document ID to inspect"), { target: { value: "44444444-4444-4444-4444-444444444444" } });
  expect(screen.queryByRole("checkbox")).toBeNull(); expect(screen.queryByRole("button", { name: "Delete expired document" })).toBeNull();
});

it("waits for a pending removal across remount without replay or read-ahead", async () => {
  const view = render(<DocumentDeletionControls enabled/>);
  await screen.findByText("No deletion records on this page.");
  fireEvent.change(screen.getByLabelText("Document ID to inspect"), { target: { value: id } });
  fireEvent.click(screen.getByRole("button", { name: "Inspect deletion" }));
  const remove = await screen.findByRole("button", { name: "Delete expired document" });
  let reject!: (error: Error) => void;
  fetcher.mockImplementationOnce(() => new Promise<Response>((_, fail) => { reject = fail; }));
  fireEvent.click(screen.getByRole("checkbox")); fireEvent.click(remove);
  view.unmount(); render(<DocumentDeletionControls enabled/>);
  await act(async () => { await Promise.resolve(); });
  expect(fetcher.mock.calls.filter(([url]) => url.includes("/deletions?"))).toHaveLength(1);
  expect(screen.getByRole("button", { name: "Inspect deletion" }).matches(":disabled")).toBe(true);
  await act(async () => reject(new Error("lost response")));
  await screen.findByRole("alert");
  expect(fetcher.mock.calls.filter(([url]) => url.includes("/deletions?"))).toHaveLength(2);
  expect(fetcher.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(1);
  expect(screen.queryByRole("checkbox")).toBeNull();
  expect(screen.getByRole("button", { name: "Inspect deletion" }).matches(":disabled")).toBe(true);
});

it("keeps failed recovery locked until an explicit successful refresh and fresh confirmation", async () => {
  await inspect(); const remove = await screen.findByRole("button", { name: "Delete expired document" });
  fireEvent.click(screen.getByRole("checkbox")); fetcher.mockRejectedValueOnce(new Error("lost")); fireEvent.click(remove);
  await screen.findByRole("alert");
  fetcher.mockRejectedValueOnce(new Error("history unavailable"));
  fireEvent.click(screen.getByRole("button", { name: "Refresh deletion history" }));
  await screen.findByText("Deletion history could not be loaded. Refresh before retrying a deletion.");
  expect(screen.queryByText("No deletion records on this page.")).toBeNull();
  expect(screen.getByRole("button", { name: "Inspect deletion" }).matches(":disabled")).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: "Refresh deletion history" }));
  await waitFor(() => expect(screen.getByRole("button", { name: "Inspect deletion" }).matches(":disabled")).toBe(false));
  expect(screen.queryByRole("checkbox")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Inspect deletion" }));
  const fresh = await screen.findByRole("button", { name: "Delete expired document" });
  expect(fresh.matches(":disabled")).toBe(true);
  expect(fetcher.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(1);
});
