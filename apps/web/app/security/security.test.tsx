import { clearMemberRecovery } from "../organization/people/member-recovery";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { secureTransport, apiFetch } from "./api-fetch";
import { EnterpriseProvider } from "./enterprise-provider";
import { validIdentityConfig } from "./identity-client";
import OrganizationPage from "../organization/page";
import AuditPage from "../organization/audit/page";
import Home from "../document-workspace";
import { StrictMode } from "react";

const navigation = vi.hoisted(() => ({ path: "/organization", replace: vi.fn() }));
vi.mock("next/navigation", () => ({ usePathname: () => navigation.path, useRouter: () => ({ replace: navigation.replace }) }));
const config = { mode: "Entra", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", clientId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  scope: "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/DocumentIntelligence.Access", api: "http://localhost:8080" };
const session = { organizationId: "org-a", organizationName: "Fictional enterprise", actorId: "admin-a", displayName: "Test administrator",
  tenantId: config.tenantId, role: "Admin" };
const member = { id: "member-a", actorId: "person-a", tenantId: config.tenantId, objectId: "dddddddd-dddd-dddd-dddd-dddddddddddd", displayName: "Test member", role: "Staff", isActive: true, version: "version-one" };
const identity = () => ({ initialize: vi.fn(async () => true), token: vi.fn(async () => "test-token"), signIn: vi.fn(async () => {}), signOut: vi.fn(async () => {}) });
afterEach(() => { clearMemberRecovery("org-a:admin-a"); vi.unstubAllGlobals(); navigation.path = "/organization"; });
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

describe("authenticated transport", () => {
  it("preserves concurrency headers and bodies while restricting credentials and redirects", async () => {
    const fetcher = vi.fn(async (_input: string | URL | Request, _init?: RequestInit) => json({}));
    await secureTransport(config.api, async () => "token", vi.fn(), new AbortController().signal, fetcher)(`${config.api}/api/documents/id/reviews`, {
      method: "POST", headers: { "If-Match": "etag-1" }, body: "same-body", credentials: "include", redirect: "follow"
    });
    const options = fetcher.mock.calls[0][1] as RequestInit;
    expect(new Headers(options.headers).get("If-Match")).toBe("etag-1");
    expect(new Headers(options.headers).get("Authorization")).toBe("Bearer token");
    expect(options).toMatchObject({ body: "same-body", credentials: "omit", redirect: "error", cache: "no-store" });
  });
  it.each(["https://outside.example/api/documents", "//outside.example/api/documents", "/not-api", "http://x:y@localhost:8080/api/documents"])("never sends a token to %s", async url => {
    const token = vi.fn(); const fetcher = vi.fn();
    await expect(secureTransport(config.api, token, vi.fn(), new AbortController().signal, fetcher)(url)).rejects.toThrow();
    expect(token).not.toHaveBeenCalled(); expect(fetcher).not.toHaveBeenCalled();
  });
  it("does not retry a denied mutation", async () => {
    const invalid = vi.fn(); const fetcher = vi.fn(async () => json({}, 403));
    await secureTransport(config.api, async () => "t", invalid, new AbortController().signal, fetcher)("/api/documents", { method: "POST" });
    expect(invalid).toHaveBeenCalledOnce(); expect(fetcher).toHaveBeenCalledOnce();
  });
  it("blocks late token completion after sign-out", async () => {
    let finish!: (token: string) => void; const fetcher = vi.fn(); const controller = new AbortController();
    const pending = secureTransport(config.api, () => new Promise<string>(r => { finish = r; }), vi.fn(), controller.signal, fetcher)("/api/documents");
    controller.abort(); finish("old-token"); await expect(pending).rejects.toThrow(); expect(fetcher).not.toHaveBeenCalled();
  });
  it("fails closed for insecure origins and incomplete identity configuration", () => {
    expect(() => secureTransport("http://remote.example", vi.fn(), vi.fn(), new AbortController().signal)).toThrow();
    expect(validIdentityConfig({ ...config, tenantId: "common" })).toBe(false);
    expect(validIdentityConfig({ ...config, scope: "https://graph.microsoft.com/.default" })).toBe(false);
  });
});
describe("organization access screens", () => {
  it("initializes identity only once under React strict mode", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => json(session))); const client = identity(); const factory = vi.fn(async () => client);
    render(<StrictMode><EnterpriseProvider config={config} identityFactory={factory}><p>Private workspace</p></EnterpriseProvider></StrictMode>);
    await screen.findByText("Private workspace"); expect(factory).toHaveBeenCalledOnce(); expect(client.initialize).toHaveBeenCalledOnce();
  });
  it("excludes the redirect bridge from identity initialization", () => {
    navigation.path = "/auth/callback"; const factory = vi.fn();
    render(<EnterpriseProvider config={config} identityFactory={factory}><p>Bridge response</p></EnterpriseProvider>);
    expect(screen.getByText("Bridge response")).toBeTruthy(); expect(factory).not.toHaveBeenCalled();
  });
  it("allows Staff uploads in the document workspace", async () => {
    navigation.path = "/";
    const fetcher = vi.fn(async (url: string, _init?: RequestInit) => json(url.endsWith("/session") ? { ...session, role: "Staff" } : []));
    vi.stubGlobal("fetch", fetcher);
    const { container } = render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><Home/></EnterpriseProvider>);
    await screen.findByRole("heading", { name: "Documents" });
    expect(screen.getByRole("button", { name: "Upload & extract" })).toBeTruthy();
    expect(container.querySelector('input[type="file"]')).toBeTruthy();
    expect(fetcher.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(0);
    expect(screen.queryByRole("link", { name: "Organization" })).toBeNull();
  });
  it("gates the workspace until membership resolves and clears it on a denied request", async () => {
    let resolve!: (response: Response) => void;
    vi.stubGlobal("fetch", vi.fn((url: string) => url.endsWith("/session") ? new Promise<Response>(r => { resolve = r; }) : Promise.resolve(json({}, 403))));
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><p>Private workspace</p></EnterpriseProvider>);
    expect(screen.queryByText("Private workspace")).toBeNull(); await waitFor(() => expect(resolve).toBeDefined()); resolve(json(session));
    await screen.findByText("Private workspace"); await act(async () => { await apiFetch(`${config.api}/api/organization/memberships`); });
    await screen.findByText("Access needs attention."); expect(screen.queryByText("Private workspace")).toBeNull();
  });
  it("does not show administrator content to a viewer", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => json({ ...session, role: "Staff" })));
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><p>Member controls</p></EnterpriseProvider>);
    await screen.findByText("Administration access required."); expect(screen.queryByText("Member controls")).toBeNull();
  });
  it("clears workspace before sign-out completes", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => json(session))); const client = identity();
    client.signOut = vi.fn(() => new Promise<void>(() => {}));
    render(<EnterpriseProvider config={config} identityFactory={async () => client}><p>Private workspace</p></EnterpriseProvider>);
    await screen.findByText("Private workspace"); fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    await screen.findByRole("button", { name: "Continue with Microsoft" }); expect(screen.queryByText("Private workspace")).toBeNull();
  });
  it("sends the selected version and requires reloading after a stale membership conflict", async () => {
    const fetcher = vi.fn(async (url: string, init?: RequestInit) => url.endsWith("/session") ? json(session) : init?.method === "PUT" ? json({ code: "membership_version_conflict" }, 409) : json([member]));
    vi.stubGlobal("fetch", fetcher);
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><OrganizationPage/></EnterpriseProvider>);
    fireEvent.click(await screen.findByRole("button", { name: "Edit Test member" }));
    fireEvent.change(screen.getByLabelText("Role"), { target: { value: "Admin" } });
    fireEvent.click(screen.getByRole("button", { name: "Grant Admin access" }));
    await screen.findByRole("alert"); const change = fetcher.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(JSON.parse(change![1]!.body as string)).toMatchObject({ expectedVersion: "version-one", role: "Admin" });
    expect((screen.getByRole("button", { name: "Grant Admin access" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByRole("button", { name: "Reload members" })).toBeTruthy();
  });
  it("asks for a concrete revocation confirmation before submitting", async () => {
    const fetcher = vi.fn(async (url: string, _init?: RequestInit) => url.endsWith("/session") ? json(session) : json([member])); vi.stubGlobal("fetch", fetcher);
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><OrganizationPage/></EnterpriseProvider>);
    fireEvent.click(await screen.findByRole("button", { name: "Edit Test member" })); fireEvent.click(screen.getByLabelText(/Active access/));
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    expect(screen.getByRole("button", { name: "Confirm removal" })).toBeTruthy();
    expect(fetcher.mock.calls.every(call => !(call[1] as RequestInit | undefined)?.method)).toBe(true);
  });
  it("renders audit strings inertly and supports bounded pagination", async () => {
    navigation.path = "/organization/audit";
    const events = Array.from({ length: 100 }, (_, i) => ({ id: String(i), actorId: "actor", actorKind: "Human", action: "Membership.Changed", targetType: "Membership", targetId: "member", outcome: "Succeeded", reasonCode: "ok", requestId: "request", beforeState: "<script>alert(1)</script>", afterState: "role:Viewer", recordedAtUtc: "2026-09-11T10:00:00Z" }));
    const fetcher = vi.fn(async (url: string) => json(url.endsWith("/session") ? session : url.endsWith("offset=0") ? events : [])); vi.stubGlobal("fetch", fetcher);
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><AuditPage/></EnterpriseProvider>);
    await screen.findByText("100 events on this page"); expect(document.querySelector(".audit-detail script")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Older" })); await screen.findByText("No events on this page.");
    expect(fetcher.mock.calls.some(([url]) => url.endsWith("offset=100"))).toBe(true);
  });
});
