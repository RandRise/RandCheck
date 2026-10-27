import { clearStorageRecovery } from "./storage-recovery";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import RetentionPage from "./page";

const state = vi.hoisted(() => ({ admin: true }));
vi.mock("../../security/enterprise-provider", () => ({ useEnterprise: () => ({ session: { role: state.admin ? "Admin" : "Staff", organizationName: "Test organization" } }) }));
const base = "/api/organization/retention";
let policy = { version: "11111111-1111-1111-1111-111111111111", documentDays: null as number | null, quarantineHours: null as number | null };
const hold = { id: "hold-id", documentId: null, reason: "LegalHold", createdAtUtc: "2026-09-12T00:00:00Z", releasedAtUtc: null, version: "hold-version" };
let holds: typeof hold[] = [];
let fetcher: ReturnType<typeof vi.fn>;
beforeEach(() => {
  state.admin = true; holds = []; policy = { version: "11111111-1111-1111-1111-111111111111", documentDays: null, quarantineHours: null };
  fetcher = vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url, "http://localhost").pathname;
    if (init?.method === "PUT") {
      const body = JSON.parse(String(init.body));
      policy = { version: "saved-version", documentDays: body.documentDays, quarantineHours: body.quarantineHours };
      return Response.json(policy);
    }
    if (path === base) return Response.json(policy);
    if (path === `${base}/holds`) return Response.json(init?.method === "POST" ? hold : holds);
    if (path === `${base}/preview`) return Response.json({ documentDeletionEnabled: false, documents: [], nextOffset: null });
    if (path.endsWith("/release")) return Response.json({ ...hold, version: "released-version", releasedAtUtc: "2026-10-27T10:00:00Z" });
    if (path.endsWith("/cleanup")) return Response.json({ executed: JSON.parse(String(init?.body)).execute, eligible: 2, deleted: 0, preserved: 1, truncated: false });
    return new Response("", { status: 404 });
  }); vi.stubGlobal("fetch", fetcher);
});
afterEach(() => { clearStorageRecovery("undefined:undefined:storage"); clearStorageRecovery("undefined:undefined:deletion"); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
it("defaults to indefinite retention and disables cleanup", async () => {
  render(<RetentionPage/>); await screen.findByRole("button", { name: "Save settings" }); fireEvent.click(screen.getByText("Removal controls")); const button = await screen.findByRole("button", { name: "Preview cleanup" });
  expect((button as HTMLButtonElement).disabled).toBe(true);
  expect(screen.getByText("Stored file removal is disabled")).toBeTruthy();
  expect((screen.getByLabelText("Check stored documents after (days)") as HTMLInputElement).value).toBe("");
});
it("does not fetch or render administration for a data-only member", () => {
  state.admin = false; const { container } = render(<RetentionPage/>); expect(container.textContent).toBe(""); expect(fetcher).not.toHaveBeenCalled();
});
it("sends the loaded version and explicit null policy values", async () => {
  const originalVersion = policy.version;
  render(<RetentionPage/>); fireEvent.click(await screen.findByRole("button", { name: "Save settings" }));
  await screen.findByText("Storage settings saved and recorded.");
  const call = fetcher.mock.calls.find(([, init]) => init?.method === "PUT")!;
  expect(JSON.parse(String(call[1]?.body))).toEqual({ expectedVersion: originalVersion, documentDays: null, quarantineHours: null });
});

it("preserves a settings draft and its original version across refresh", async () => {
  render(<RetentionPage/>);
  const input = await screen.findByLabelText("Check stored documents after (days)");
  fireEvent.change(input, { target: { value: "30" } });
  policy = { version: "changed-by-another-admin", documentDays: 90, quarantineHours: 24 };
  fireEvent.click(screen.getByRole("button", { name: "Refresh controls" }));
  await screen.findByText(/Saved settings changed/);
  expect((input as HTMLInputElement).value).toBe("30");
  expect((screen.getByRole("button", { name: "Save settings" }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: "Discard entries and load saved settings" }));
  expect((input as HTMLInputElement).value).toBe("90");
  fireEvent.click(screen.getByRole("button", { name: "Save settings" }));
  await screen.findByText("Storage settings saved and recorded.");
  expect(JSON.parse(String(fetcher.mock.calls.find(([, init]) => init?.method === "PUT")![1]?.body)).expectedVersion).toBe("changed-by-another-admin");
});

it("does not show a saved or disabled state when the initial read fails", async () => {
  fetcher.mockRejectedValue(new Error("PRIVATE"));
  render(<RetentionPage/>);
  await screen.findByRole("alert");
  expect(screen.queryByText("Stored file removal is disabled")).toBeNull();
  expect(screen.queryByRole("button", { name: "Save settings" })).toBeNull();
});

it("blocks duplicate submissions and treats malformed success as uncertain across remount", async () => {
  const view = render(<RetentionPage/>);
  const button = await screen.findByRole("button", { name: "Save settings" });
  fetcher.mockResolvedValueOnce(Response.json({}));
  fireEvent.submit(button.closest("form")!); fireEvent.submit(button.closest("form")!);
  await screen.findByRole("alert");
  expect(fetcher.mock.calls.filter(([, init]) => init?.method === "PUT")).toHaveLength(1);
  expect(screen.queryByText("Storage settings saved and recorded.")).toBeNull();
  view.unmount(); render(<RetentionPage/>);
  await screen.findByRole("button", { name: "Save settings" });
  expect((screen.getByRole("button", { name: "Save settings" }) as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: "Refresh controls" }));
  await waitFor(() => expect((screen.getByRole("button", { name: "Save settings" }) as HTMLButtonElement).disabled).toBe(false));
  expect(fetcher.mock.calls.filter(([, init]) => init?.method === "PUT")).toHaveLength(1);
});

it("blocks parent storage writes during removal and removal during a settings write", async () => {
  const id = "33333333-3333-3333-3333-333333333333";
  const original = fetcher.getMockImplementation()! as (url: string, init?: RequestInit) => Promise<Response>;
  let release!: (value: Response) => void;
  fetcher.mockImplementation(async (url: string, init?: RequestInit) => {
    if (url.includes("/preview?")) return Response.json({ documentDeletionEnabled: true, documents: [], nextOffset: null });
    if (url.includes("/deletions?")) return Response.json([]);
    if (url.includes(`/documents/${id}/deletion`)) {
      if (init?.method === "POST") return new Promise<Response>(resolve => { release = resolve; });
      return Response.json({ documentId: id, documentVersion: "dv", policyVersion: policy.version, enabled: true, blockers: [], extractionRuns: 0, extractionJobs: 0 });
    }
    return original(url, init);
  });
  policy.quarantineHours = 24;
  render(<RetentionPage/>); await screen.findByRole("button", { name: "Save settings" });
  fireEvent.click(screen.getByText("Removal controls")); await screen.findByText("No deletion records on this page.");
  fireEvent.change(screen.getByLabelText("Document ID to inspect"), { target: { value: id } });
  fireEvent.click(screen.getByRole("button", { name: "Inspect deletion" }));
  const remove = await screen.findByRole("button", { name: "Delete expired document" });
  fireEvent.click(screen.getByRole("checkbox")); fireEvent.click(remove);
  for (const name of ["Save settings", "Add protection", "Preview cleanup", "Refresh controls"]) expect(screen.getByRole("button", { name }).matches(":disabled")).toBe(true);
  fireEvent.submit(screen.getByRole("button", { name: "Save settings" }).closest("form")!);
  expect(fetcher.mock.calls.some(([, init]) => init?.method === "PUT")).toBe(false);
  await act(async () => release(Response.json({ documentId: id, documentVersion: "dv", policyVersion: policy.version, state: "Completed", requestedAtUtc: "2026-10-27T00:00:00Z", completedAtUtc: "2026-10-27T00:00:01Z" })));
  await waitFor(() => expect(screen.getByRole("button", { name: "Save settings" }).matches(":disabled")).toBe(false));
  fetcher.mockImplementationOnce(() => new Promise<Response>(resolve => { release = resolve; }));
  fireEvent.click(screen.getByRole("button", { name: "Save settings" }));
  expect(screen.getByRole("button", { name: "Inspect deletion" }).matches(":disabled")).toBe(true);
  const count = fetcher.mock.calls.length;
  fireEvent.submit(screen.getByRole("button", { name: "Inspect deletion" }).closest("form")!);
  expect(fetcher.mock.calls).toHaveLength(count);
  policy = { ...policy, version: "updated" };
  await act(async () => release(Response.json(policy)));
  await screen.findByText("Storage settings saved and recorded.");
});
it("requires preview and confirmation before requesting deletion", async () => {
  policy.quarantineHours = 24; render(<RetentionPage/>);
  expect(screen.queryByRole("button", { name: "Remove unfinished uploads" })).toBeNull();
  await screen.findByRole("button", { name: "Save settings" }); fireEvent.click(screen.getByText("Removal controls"));
  fireEvent.click(await screen.findByRole("button", { name: "Preview cleanup" }));
  const run = await screen.findByRole("button", { name: "Remove unfinished uploads" }); expect((run as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole("checkbox")); fireEvent.click(run);
  await screen.findByText("Abandoned upload cleanup finished. Activity history records the individual actions.");
  const calls = fetcher.mock.calls.filter(([url]) => url.endsWith("/cleanup"));
  expect(calls.map(([, init]) => JSON.parse(String(init?.body)))).toEqual([
    { expectedPolicyVersion: policy.version, execute: false }, { expectedPolicyVersion: policy.version, execute: true }]);
});
it("requires an explicit confirmation and the hold version to release", async () => {
  holds = [hold]; render(<RetentionPage/>); fireEvent.click(await screen.findByRole("button", { name: "Release protection" }));
  expect(fetcher.mock.calls.some(([url]) => url.endsWith("/release"))).toBe(false);
  fireEvent.click(screen.getByRole("button", { name: "Confirm release" })); await screen.findByText("Protection released and recorded. No document was deleted.");
  expect(JSON.parse(String(fetcher.mock.calls.find(([url]) => url.endsWith("/release"))![1]?.body))).toEqual({ expectedVersion: hold.version });
});
it("hides transport and response secrets and clears cleanup approval after failure", async () => {
  policy.quarantineHours = 24; render(<RetentionPage/>);
  await screen.findByRole("button", { name: "Save settings" }); fireEvent.click(screen.getByText("Removal controls"));
  fireEvent.click(await screen.findByRole("button", { name: "Preview cleanup" }));
  await screen.findByRole("button", { name: "Remove unfinished uploads" }); fireEvent.click(screen.getByRole("checkbox"));
  fetcher.mockRejectedValueOnce(new Error("PRIVATE_TOKEN")); fireEvent.click(screen.getByRole("button", { name: "Remove unfinished uploads" }));
  expect((await screen.findByRole("alert")).textContent).not.toContain("PRIVATE_TOKEN");
  await waitFor(() => expect(screen.queryByRole("button", { name: "Remove unfinished uploads" })).toBeNull());
});
