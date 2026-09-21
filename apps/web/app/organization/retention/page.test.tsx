import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import RetentionPage from "./page";

const state = vi.hoisted(() => ({ admin: true }));
vi.mock("../../security/enterprise-provider", () => ({ useEnterprise: () => ({ session: { canManageMembers: state.admin, organizationName: "Test organization" } }) }));
const base = "/api/organization/retention";
let policy = { version: "11111111-1111-1111-1111-111111111111", documentDays: null as number | null, quarantineHours: null as number | null };
const hold = { id: "hold-id", documentId: null, reason: "LegalHold", createdAtUtc: "2026-09-12T00:00:00Z", releasedAtUtc: null, version: "hold-version" };
let holds: typeof hold[] = [];
let fetcher: ReturnType<typeof vi.fn>;
beforeEach(() => {
  state.admin = true; holds = []; policy = { version: "11111111-1111-1111-1111-111111111111", documentDays: null, quarantineHours: null };
  fetcher = vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url, "http://localhost").pathname;
    if (init?.method === "PUT") return Response.json(policy);
    if (path === base) return Response.json(policy);
    if (path === `${base}/holds`) return Response.json(init?.method === "POST" ? hold : holds);
    if (path === `${base}/preview`) return Response.json({ documentDeletionEnabled: false, documents: [], nextOffset: null });
    if (path.endsWith("/release")) return Response.json(hold);
    if (path.endsWith("/cleanup")) return Response.json({ executed: JSON.parse(String(init?.body)).execute, eligible: 2, deleted: 0, preserved: 1, truncated: false });
    return new Response("", { status: 404 });
  }); vi.stubGlobal("fetch", fetcher);
});
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });
it("defaults to indefinite retention and disables cleanup", async () => {
  render(<RetentionPage/>); const button = await screen.findByRole("button", { name: "Preview cleanup" });
  expect((button as HTMLButtonElement).disabled).toBe(true);
  expect(screen.getByText("Document deletion disabled")).toBeTruthy();
  expect((screen.getByLabelText("Document retention (days)") as HTMLInputElement).value).toBe("");
});
it("does not fetch or render administration for a data-only member", () => {
  state.admin = false; const { container } = render(<RetentionPage/>); expect(container.textContent).toBe(""); expect(fetcher).not.toHaveBeenCalled();
});
it("sends the loaded version and explicit null policy values", async () => {
  render(<RetentionPage/>); fireEvent.click(await screen.findByRole("button", { name: "Save policy" }));
  await screen.findByText("Retention policy saved and audited.");
  const call = fetcher.mock.calls.find(([, init]) => init?.method === "PUT")!;
  expect(JSON.parse(String(call[1]?.body))).toEqual({ expectedVersion: policy.version, documentDays: null, quarantineHours: null });
});
it("requires preview and confirmation before requesting deletion", async () => {
  policy.quarantineHours = 24; render(<RetentionPage/>);
  expect(screen.queryByRole("button", { name: "Run quarantine cleanup" })).toBeNull();
  fireEvent.click(await screen.findByRole("button", { name: "Preview cleanup" }));
  const run = await screen.findByRole("button", { name: "Run quarantine cleanup" }); expect((run as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole("checkbox")); fireEvent.click(run);
  await screen.findByText("Quarantine cleanup completed. Review the audit trail for individual actions.");
  const calls = fetcher.mock.calls.filter(([url]) => url.endsWith("/cleanup"));
  expect(calls.map(([, init]) => JSON.parse(String(init?.body)))).toEqual([
    { expectedPolicyVersion: policy.version, execute: false }, { expectedPolicyVersion: policy.version, execute: true }]);
});
it("requires an explicit confirmation and the hold version to release", async () => {
  holds = [hold]; render(<RetentionPage/>); fireEvent.click(await screen.findByRole("button", { name: "Release hold" }));
  expect(fetcher.mock.calls.some(([url]) => url.endsWith("/release"))).toBe(false);
  fireEvent.click(screen.getByRole("button", { name: "Confirm release" })); await screen.findByText("Hold released and audited.");
  expect(JSON.parse(String(fetcher.mock.calls.find(([url]) => url.endsWith("/release"))![1]?.body))).toEqual({ expectedVersion: hold.version });
});
it("hides transport and response secrets and clears cleanup approval after failure", async () => {
  policy.quarantineHours = 24; render(<RetentionPage/>);
  fireEvent.click(await screen.findByRole("button", { name: "Preview cleanup" }));
  await screen.findByRole("button", { name: "Run quarantine cleanup" }); fireEvent.click(screen.getByRole("checkbox"));
  fetcher.mockRejectedValueOnce(new Error("PRIVATE_TOKEN")); fireEvent.click(screen.getByRole("button", { name: "Run quarantine cleanup" }));
  expect((await screen.findByRole("alert")).textContent).not.toContain("PRIVATE_TOKEN");
  await waitFor(() => expect(screen.queryByRole("button", { name: "Run quarantine cleanup" })).toBeNull());
});
