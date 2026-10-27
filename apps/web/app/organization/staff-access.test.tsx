import { clearMemberRecovery } from "./people/member-recovery";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import OrganizationPage, { type Member } from "./page";

vi.mock("../security/enterprise-provider", () => ({ useEnterprise: () => ({
  session: { actorId: "administrator", organizationName: "Test", tenantId: "tenant", role: "Admin" },
  refresh: vi.fn(async () => {}),
}) }));
const member: Member = { id: "member", actorId: "person", tenantId: "tenant",
  objectId: "dddddddd-dddd-dddd-dddd-dddddddddddd", displayName: "Test member",
  role: "Staff", isActive: true, version: "saved-version" };
function setup(role = "Staff", active = true) {
  const fetcher = vi.fn(async (_url: string, init?: RequestInit) => new Response(JSON.stringify(
    init?.method ? { code: "unavailable" } : [{ ...member, role, isActive: active }]
  ), { status: init?.method ? 503 : 200 }));
  vi.stubGlobal("fetch", fetcher); render(<OrganizationPage/>); return fetcher;
}
const writes = (fetcher: ReturnType<typeof setup>) => fetcher.mock.calls.filter(([, init]) => init?.method);
afterEach(() => { clearMemberRecovery("undefined:administrator"); vi.unstubAllGlobals(); });

describe("Staff and Admin account setup", () => {
  it.each(["Staff", "Admin"])("creates %s with exactly one role and no administration flag", async role => {
    const fetcher = setup();
    fireEvent.click(await screen.findByRole("button", { name: "Add person" }));
    const select = screen.getByRole("combobox", { name: "Role" });
    expect(within(select).getAllByRole("option").map(option => option.textContent)).toEqual(["Choose a role", "Staff", "Admin"]);
    expect(screen.queryByLabelText(/Organization administrator/)).toBeNull();
    fireEvent.submit(screen.getByRole("button", { name: "Grant access" }).closest("form")!);
    await screen.findByText("Choose a role before saving.");
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.change(select, { target: { value: role } });
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.change(screen.getByLabelText("Display name"), { target: { value: "New person" } });
    fireEvent.change(screen.getByLabelText("Microsoft Entra object ID"), { target: { value: member.objectId } });
    fireEvent.click(screen.getByRole("button", { name: "Grant access" }));
    await waitFor(() => expect(writes(fetcher)).toHaveLength(1));
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body))).toEqual({ tenantId: "tenant", objectId: member.objectId, displayName: "New person", role });
  });

  it.each(["Staff", "Admin"])("preserves %s when disabling an account, with confirmation and expected version", async role => {
    const fetcher = setup(role);
    fireEvent.click(await screen.findByRole("button", { name: "Edit Test member" }));
    expect((screen.getByLabelText("Role") as HTMLSelectElement).value).toBe(role);
    fireEvent.click(screen.getByLabelText(/Active access/));
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "Confirm removal" }));
    await waitFor(() => expect(writes(fetcher)).toHaveLength(1));
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body))).toEqual({ expectedVersion: "saved-version", role, isActive: false });
  });

  it("explains the extra rights before granting Admin and saves only after an explicit action", async () => {
    const fetcher = setup(); fireEvent.click(await screen.findByRole("button", { name: "Edit Test member" }));
    fireEvent.change(screen.getByLabelText("Role"), { target: { value: "Admin" } });
    expect(screen.getByText(/including managing people and document deletion settings/)).toBeTruthy();
    expect(writes(fetcher)).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "Grant Admin access" }));
    await waitFor(() => expect(writes(fetcher)).toHaveLength(1));
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body))).toEqual({ expectedVersion: "saved-version", role: "Admin", isActive: true });
  });

  it("keeps a disabled account disabled when its role changes", async () => {
    const fetcher = setup("Staff", false); fireEvent.click(await screen.findByRole("button", { name: "Edit Test member" }));
    fireEvent.change(screen.getByLabelText("Role"), { target: { value: "Admin" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(writes(fetcher)).toHaveLength(1));
    expect(JSON.parse(String(writes(fetcher)[0][1]?.body))).toEqual({ expectedVersion: "saved-version", role: "Admin", isActive: false });
  });
});
