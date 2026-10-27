import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { can, EnterpriseProvider, SESSION_READ_TIMEOUT_MS, useEnterprise, type Session } from "./enterprise-provider";

const navigation = vi.hoisted(() => ({ path: "/", replace: vi.fn() }));
vi.mock("next/navigation", () => ({ usePathname: () => navigation.path,
  useRouter: () => ({ replace: navigation.replace }) }));
const config = { mode: "Entra", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", clientId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  scope: "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/DocumentIntelligence.Access", api: "http://localhost:8080" };
const operator: Session = { organizationId: "org", organizationName: "Example organization", actorId: "actor", displayName: "Example user",
  tenantId: config.tenantId, role: "Admin" };
const viewer: Session = { ...operator, role: "Staff" };
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json" } });
function deferred() {
  let resolve!: (response: Response) => void; let reject!: (error: Error) => void;
  const promise = new Promise<Response>((done, fail) => { resolve = done; reject = fail; });
  return { promise, resolve, reject };
}
function identity() { return { initialize: vi.fn(async () => true), token: vi.fn(async () => "test-token"),
  signIn: vi.fn(async () => {}), signOut: vi.fn(async () => {}) }; }
function Content() {
  const { session, refresh } = useEnterprise(); const [draft, setDraft] = useState("");
  return <section><h2>Private screen</h2><p>Current role: {session.role}</p>
    <label>Private draft<input value={draft} onChange={event => setDraft(event.target.value)}/></label>
    {can(session, "ManageMembers") && <button>Admin action</button>}
    <button onClick={() => void refresh()}>Refresh access for test</button></section>;
}
function installReads(initial: Session | null = operator) {
  const pending: ReturnType<typeof deferred>[] = [];
  const fetcher = vi.fn((url: string, _init?: RequestInit) => {
    if (!url.endsWith("/api/session")) throw new Error(`Unexpected request ${url}`);
    if (initial && fetcher.mock.calls.length === 1) return Promise.resolve(json(initial));
    // Deliberately ignore abort: response ownership must still reject late completion.
    const read = deferred(); pending.push(read); return read.promise;
  });
  vi.stubGlobal("fetch", fetcher); return { pending, fetcher };
}
function mount(client = identity()) {
  const factory = vi.fn(async () => client);
  return { ...render(<EnterpriseProvider config={config} identityFactory={factory}><Content/></EnterpriseProvider>), client, factory };
}
function expectViewer() {
  expect(screen.getByText("Current role: Staff")).toBeTruthy();
  expect(screen.queryByRole("button", { name: "Admin action" })).toBeNull();
  expect(screen.queryByRole("link", { name: "Administration" })).toBeNull();
  expect(screen.queryByRole("heading", { name: "Access needs attention." })).toBeNull();
}
function expectReadOnly(fetcher: ReturnType<typeof installReads>["fetcher"]) {
  expect(fetcher.mock.calls.every(([url, init]) => url.endsWith("/api/session") && (!init?.method || init.method === "GET"))).toBe(true);
}
beforeEach(() => { navigation.path = "/"; navigation.replace.mockClear(); window.history.replaceState(null, "", "/"); });
afterEach(() => { cleanup(); vi.useRealTimers(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe("session read ownership and bounded recovery", () => {
  it("keeps newer focus-check Staff grants and cleared drafts when the older manual check returns Admin", async () => {
    const { pending, fetcher } = installReads(); mount(); await screen.findByRole("heading", { name: "Private screen" });
    fireEvent.change(screen.getByLabelText("Private draft"), { target: { value: "Prior privileged draft" } });
    fireEvent.click(screen.getByRole("button", { name: "Refresh access for test" }));
    await waitFor(() => expect(pending).toHaveLength(1)); fireEvent(window, new Event("focus"));
    await waitFor(() => expect(pending).toHaveLength(2));
    await act(async () => pending[1].resolve(json(viewer))); expectViewer();
    expect((screen.getByLabelText("Private draft") as HTMLInputElement).value).toBe("");
    await act(async () => pending[0].resolve(json(operator))); expectViewer();
    expect((screen.getByLabelText("Private draft") as HTMLInputElement).value).toBe("");
    expect(fetcher).toHaveBeenCalledTimes(3); expectReadOnly(fetcher);
  });

  it.each(["network", "malformed", "unauthorized", "forbidden"] as const)(
    "ignores an older %s failure after the newer read succeeds", async failure => {
      const { pending, fetcher } = installReads(); mount(); await screen.findByRole("heading", { name: "Private screen" });
      fireEvent.click(screen.getByRole("button", { name: "Refresh access for test" }));
      await waitFor(() => expect(pending).toHaveLength(1)); fireEvent(window, new Event("focus"));
      await waitFor(() => expect(pending).toHaveLength(2));
      await act(async () => pending[1].resolve(json(viewer))); expectViewer();
      await act(async () => {
        if (failure === "network") pending[0].reject(new Error("Old connection failed"));
        else pending[0].resolve(failure === "malformed" ? json({ ...operator, tenantId: "wrong-tenant" })
          : json({}, failure === "unauthorized" ? 401 : 403));
      });
      expectViewer(); expect(fetcher).toHaveBeenCalledTimes(3); expectReadOnly(fetcher);
    });

  it("keeps the newer timer-check role after an older focus response", async () => {
    vi.useFakeTimers(); const { pending, fetcher } = installReads();
    await act(async () => { mount(); }); expect(screen.getByRole("heading", { name: "Private screen" })).toBeTruthy();
    // Start just before the existing one-minute focus interval, before the read budget expires.
    await act(async () => { await vi.advanceTimersByTimeAsync(59000); });
    await act(async () => { fireEvent(window, new Event("focus")); }); expect(pending).toHaveLength(1);
    await act(async () => { await vi.advanceTimersByTimeAsync(1000); }); expect(pending).toHaveLength(2);
    await act(async () => pending[1].resolve(json(viewer))); expectViewer();
    await act(async () => pending[0].resolve(json(operator))); expectViewer(); expectReadOnly(fetcher);
  });

  it("gates a stalled initial read, offers retry and sign-out, and refuses late success after timeout", async () => {
    vi.useFakeTimers(); const { pending, fetcher } = installReads(null);
    await act(async () => { mount(); }); expect(pending).toHaveLength(1);
    expect(screen.getByRole("heading", { name: "Opening your workspace…" })).toBeTruthy();
    await act(async () => { await vi.advanceTimersByTimeAsync(SESSION_READ_TIMEOUT_MS); });
    expect(screen.getByRole("heading", { name: "Access needs attention." })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Check access" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Sign out" })).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Private screen" })).toBeNull();
    await act(async () => pending[0].resolve(json(operator)));
    expect(screen.getByRole("heading", { name: "Access needs attention." })).toBeTruthy();
    expect(screen.queryByRole("link", { name: "Document library" })).toBeNull(); expectReadOnly(fetcher);
  });

  it.each(["pending", "timed-out"] as const)("clears a %s background read on sign-out and cannot restore grants afterward", async state => {
    vi.useFakeTimers(); const { pending, fetcher } = installReads(); const client = identity();
    await act(async () => { mount(client); });
    await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Refresh access for test" })); });
    expect(pending).toHaveLength(1);
    if (state === "timed-out") await act(async () => { await vi.advanceTimersByTimeAsync(SESSION_READ_TIMEOUT_MS); });
    await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Sign out" })); });
    expect(client.signOut).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "Continue with Microsoft" })).toBeTruthy();
    await act(async () => pending[0].resolve(json(operator)));
    await act(async () => { await vi.advanceTimersByTimeAsync(60000); });
    expect(screen.queryByRole("heading", { name: "Private screen" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Administration" })).toBeNull();
    expect(fetcher).toHaveBeenCalledTimes(2); expectReadOnly(fetcher);
  });

  it.each([401, 403])("immediately removes current grants on session HTTP%s without reading the denied body", async status => {
    vi.useFakeTimers(); const { pending, fetcher } = installReads();
    await act(async () => { mount(); });
    await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Refresh access for test" })); });
    const denied = json({}, status); const decode = vi.spyOn(denied, "json");
    // No clock advance: a current authorization denial cannot wait for its deadline.
    await act(async () => pending[0].resolve(denied));
    expect(screen.getByRole("heading", { name: "Access needs attention." })).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Private screen" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Administration" })).toBeNull();
    expect(decode).not.toHaveBeenCalled(); expect(fetcher).toHaveBeenCalledTimes(2); expectReadOnly(fetcher);
  });

  it("rechecks access through a fresh provider after the offered page-reload retry, without replaying writes", async () => {
    vi.useFakeTimers(); const { pending, fetcher } = installReads(null); let first!: ReturnType<typeof mount>;
    await act(async () => { first = mount(); });
    await act(async () => { await vi.advanceTimersByTimeAsync(SESSION_READ_TIMEOUT_MS); });
    expect(screen.getByRole("button", { name: "Check access" })).toBeTruthy();
    // jsdom cannot execute location.reload; model its fresh provider lifecycle.
    first.unmount(); await act(async () => { mount(); }); expect(pending).toHaveLength(2);
    await act(async () => pending[1].resolve(json(viewer))); expectViewer();
    await act(async () => pending[0].resolve(json(operator))); expectViewer();
    expect(fetcher).toHaveBeenCalledTimes(2); expectReadOnly(fetcher);
  });

  it("bounds a stalled session response body and ignores its late decoded grants", async () => {
    vi.useFakeTimers(); let finishBody!: (value: Session) => void;
    const response = json(operator);
    vi.spyOn(response, "json").mockImplementation(() => new Promise<Session>(done => { finishBody = done; }));
    const fetcher = vi.fn(async () => response); vi.stubGlobal("fetch", fetcher);
    await act(async () => { mount(); }); expect(response.json).toHaveBeenCalledOnce();
    await act(async () => { await vi.advanceTimersByTimeAsync(SESSION_READ_TIMEOUT_MS); });
    expect(screen.getByRole("heading", { name: "Access needs attention." })).toBeTruthy();
    await act(async () => finishBody(operator));
    expect(screen.queryByRole("heading", { name: "Private screen" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Administration" })).toBeNull(); expect(fetcher).toHaveBeenCalledOnce();
  });

  it("keeps newer Staff access when the older successful response body decodes Admin afterward", async () => {
    const { pending, fetcher } = installReads(); mount(); await screen.findByRole("heading", { name: "Private screen" });
    fireEvent.click(screen.getByRole("button", { name: "Refresh access for test" }));
    await waitFor(() => expect(pending).toHaveLength(1));
    let finishBody!: (value: Session) => void; const older = json(operator);
    const decode = vi.spyOn(older, "json").mockImplementation(() => new Promise<Session>(done => { finishBody = done; }));
    await act(async () => pending[0].resolve(older)); expect(decode).toHaveBeenCalledOnce();
    fireEvent(window, new Event("focus")); await waitFor(() => expect(pending).toHaveLength(2));
    await act(async () => pending[1].resolve(json(viewer))); expectViewer();
    await act(async () => finishBody(operator)); expectViewer();
    expect(fetcher).toHaveBeenCalledTimes(3); expectReadOnly(fetcher);
  });

  it("bounds stalled session token acquisition and clears the gate safely on sign-out", async () => {
    vi.useFakeTimers(); const client = identity();
    client.token.mockImplementation(() => new Promise<string>(() => {}));
    const { fetcher } = installReads(null); await act(async () => { mount(client); });
    await act(async () => { await vi.advanceTimersByTimeAsync(SESSION_READ_TIMEOUT_MS); });
    expect(screen.getByRole("heading", { name: "Access needs attention." })).toBeTruthy();
    await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Sign out" })); });
    expect(screen.getByRole("button", { name: "Continue with Microsoft" })).toBeTruthy();
    expect(client.signOut).toHaveBeenCalledOnce(); expect(fetcher).not.toHaveBeenCalled();
  });

  it("aborts a pending session read on unmount and cannot affect a fresh provider", async () => {
    vi.useFakeTimers(); const { pending, fetcher } = installReads(null); let first!: ReturnType<typeof mount>;
    await act(async () => { first = mount(); }); expect(pending).toHaveLength(1);
    const firstSignal = fetcher.mock.calls[0][1]?.signal;
    first.unmount(); expect(firstSignal?.aborted).toBe(true);
    await act(async () => { mount(); }); expect(pending).toHaveLength(2);
    await act(async () => pending[1].resolve(json(viewer))); expectViewer();
    await act(async () => pending[0].resolve(json(operator))); expectViewer();
    await act(async () => { await vi.advanceTimersByTimeAsync(SESSION_READ_TIMEOUT_MS); }); expectViewer();
    expect(fetcher).toHaveBeenCalledTimes(2); expectReadOnly(fetcher);
  });
});
