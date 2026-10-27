import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import OrganizationPage from "../page";
import { clearMemberRecovery } from "./member-recovery";

const identity = vi.hoisted(() => ({ organizationId: "recovery-org", actorId: "admin", tenantId: "tenant", role: "Admin" }));
vi.mock("../../security/enterprise-provider", () => ({ useEnterprise: () => ({ session: identity, refresh: vi.fn(async () => {}) }) }));
const member = { id: "alice", actorId: "alice", displayName: "Alice", objectId: "dddddddd-dddd-dddd-dddd-dddddddddddd", role: "Staff", isActive: true, version: "v1" };
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status });
const saveButton = () => screen.getByRole("button", { name: "Grant Admin access" });
async function startEdit() {
  fireEvent.click(await screen.findByRole("button", { name: "Edit Alice" }));
  fireEvent.change(screen.getByLabelText("Role"), { target: { value: "Admin" } });
}
beforeEach(() => { identity.organizationId = "recovery-org"; identity.role = "Admin"; });
afterEach(() => { cleanup(); clearMemberRecovery("recovery-org:admin"); clearMemberRecovery("other-org:admin"); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe("people refresh and uncertain saves", () => {
  it.each([200, 503])("locks edits during refresh and keeps the draft after HTTP %s", async status => {
    let finish!: (response: Response) => void;
    let reads = 0;
    const fetcher = vi.fn((_url: string, init?: RequestInit) => init?.method ? Promise.resolve(json(member))
      : ++reads === 1 ? Promise.resolve(json([member])) : new Promise<Response>(resolve => { finish = resolve; }));
    vi.stubGlobal("fetch", fetcher);
    render(<OrganizationPage/>);
    await startEdit();
    const form = saveButton().closest("form")!;
    act(() => { fireEvent.click(screen.getByRole("button", { name: "Refresh list" })); fireEvent.submit(form); });
    for (const name of ["Edit Alice", "Add person", "Close", "Grant Admin access"]) expect(screen.getByRole("button", { name }).matches(":disabled")).toBe(true);
    await waitFor(() => expect(finish).toBeDefined());
    await act(async () => finish(status === 200 ? json([{ ...member, version: "v2" }]) : json({}, 503)));
    expect((screen.getByLabelText("Role") as HTMLSelectElement).value).toBe("Admin");
    expect(fetcher.mock.calls.filter(([, init]) => init?.method)).toHaveLength(0);
    if (status === 503) expect(screen.getByRole("alert").textContent).toContain("could not be loaded");
    else {
      expect(screen.queryByRole("alert")).toBeNull();
      // The draft retains its version; a later conflict is resolved by the backend.
      fireEvent.submit(form);
      await waitFor(() => expect(fetcher.mock.calls.filter(([, init]) => init?.method)).toHaveLength(1));
      const write = fetcher.mock.calls.find(([, init]) => init?.method)!;
      expect(JSON.parse(write[1]!.body as string).expectedVersion).toBe("v1");
      await waitFor(() => expect(reads).toBe(3));
      await act(async () => finish(json([member])));
    }
  });

  it.each(["network", "503", "invalid-success", "incomplete-success"])("retains a draft and blocks replay after %s, including a failed recovery read", async failure => {
    let failRead = false;
    const fetcher = vi.fn(async (_url: string, init?: RequestInit) => {
      if (!init?.method) return failRead ? json({}, 503) : json([member]);
      if (failure === "network") throw new TypeError("Connection lost");
      if (failure === "incomplete-success") return json({});
      return failure === "503" ? json({}, 503) : new Response("not json", { status: 200 });
    });
    vi.stubGlobal("fetch", fetcher);
    render(<OrganizationPage/>);
    await startEdit();
    fireEvent.submit(saveButton().closest("form")!);
    await screen.findByText(/save result could not be confirmed/);
    failRead = true;
    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Reload members" })));
    expect(saveButton().matches(":disabled")).toBe(true);
    expect((screen.getByLabelText("Role") as HTMLSelectElement).value).toBe("Admin");
    fireEvent.submit(saveButton().closest("form")!);
    expect(fetcher.mock.calls.filter(([, init]) => init?.method)).toHaveLength(1);
    failRead = false;
    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Reload members" })));
    expect(screen.queryByLabelText("Role")).toBeNull();
    expect(screen.getByRole("button", { name: "Edit Alice" }).matches(":disabled")).toBe(false);
  });

  it("waits for a pending write across remount, then requires inspection with no replay", async () => {
    let finish!: (response: Response) => void;
    const fetcher = vi.fn((_url: string, init?: RequestInit) => init?.method
      ? new Promise<Response>(resolve => { finish = resolve; }) : Promise.resolve(json([member])));
    vi.stubGlobal("fetch", fetcher);
    const first = render(<OrganizationPage/>);
    await startEdit();
    fireEvent.submit(saveButton().closest("form")!);
    first.unmount();
    render(<OrganizationPage/>);
    await act(async () => {});
    expect(fetcher.mock.calls.filter(([, init]) => !init?.method)).toHaveLength(1);
    expect(screen.getByRole("alert").textContent).toContain("earlier save needs checking");
    await act(async () => finish(json({}, 503)));
    await screen.findByRole("button", { name: "Edit Alice" });
    expect(screen.getByRole("button", { name: "Edit Alice" }).matches(":disabled")).toBe(true);
    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Reload members" })));
    expect(screen.getByRole("button", { name: "Edit Alice" }).matches(":disabled")).toBe(false);
    expect(fetcher.mock.calls.filter(([, init]) => init?.method)).toHaveLength(1);
  });

  it("keeps recovery separate for another organization and denies Staff without a read", async () => {
    sessionStorage.setItem("member-save-recovery:recovery-org:admin", "pending");
    identity.organizationId = "other-org";
    const fetcher = vi.fn(async () => json([member]));
    vi.stubGlobal("fetch", fetcher);
    const view = render(<OrganizationPage/>);
    expect((await screen.findByRole("button", { name: "Edit Alice" })).matches(":disabled")).toBe(false);
    identity.role = "Staff";
    view.rerender(<OrganizationPage/>);
    expect(screen.queryByRole("button", { name: "Add person" })).toBeNull();
    expect(fetcher).toHaveBeenCalledTimes(1);
  });

  it("shows load failure without claiming the organization has no people", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => json({}, 503)));
    render(<OrganizationPage/>);
    await screen.findByRole("alert");
    expect(screen.queryByText("No people were returned.")).toBeNull();
    expect(screen.queryByText("0 people shown")).toBeNull();
  });
});
