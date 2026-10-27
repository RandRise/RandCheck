import { clearMemberRecovery } from "./people/member-recovery";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import OrganizationPage, { type Member } from "./page";

vi.mock("../security/enterprise-provider", () => ({ useEnterprise: () => ({
  session: { actorId: "administrator", organizationName: "Synthetic organization", tenantId: "tenant", role: "Admin" },
  refresh: vi.fn(async () => {}),
}) }));
const members: Member[] = ["Alice", "Bob"].map((displayName, index) => ({
  id: `member-${index}`, actorId: `actor-${index}`, tenantId: "tenant", objectId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
  displayName, role: "Staff", isActive: true, version: "original-version",
}));
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status });
afterEach(() => { clearMemberRecovery("undefined:administrator"); vi.unstubAllGlobals(); });

describe("membership save ownership", () => {
  it.each(["success", "conflict", "unavailable"])("locks the target and draft through a pending %s response", async outcome => {
    let finish!: (response: Response) => void;
    const fetcher = vi.fn((_url: string, init?: RequestInit) => init?.method
      ? new Promise<Response>(resolve => { finish = resolve; }) : Promise.resolve(json(members)));
    vi.stubGlobal("fetch", fetcher);
    render(<OrganizationPage/>);
    fireEvent.click(await screen.findByRole("button", { name: "Edit Alice" }));
    fireEvent.change(screen.getByLabelText("Role"), { target: { value: "Admin" } });
    const form = screen.getByRole("button", { name: "Grant Admin access" }).closest("form")!;
    // Two synchronous submits must still create exactly one owned write.
    act(() => { fireEvent.submit(form); fireEvent.submit(form); });
    expect(fetcher.mock.calls.filter(([, init]) => init?.method)).toHaveLength(1);
    for (const label of ["Edit Alice", "Edit Bob", "Add person", "Close", "Refresh list"])
      expect((screen.getByRole("button", { name: label }) as HTMLButtonElement).disabled).toBe(true);
    for (const label of ["Role", /Active access/])
      expect(screen.getByLabelText(label).matches(":disabled")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Edit Bob" }));
    fireEvent.click(screen.getByRole("button", { name: "Add person" }));
    expect(form.textContent).toContain("Alice");
    expect(screen.queryByLabelText("Display name")).toBeNull();
    const write = fetcher.mock.calls.find(([, init]) => init?.method)!;
    expect(write[0]).toMatch(/\/member-0$/);
    expect(JSON.parse(write[1]!.body as string)).toMatchObject({ expectedVersion: "original-version", role: "Admin" });

    await act(async () => finish(outcome === "success" ? json({ ...members[0], role: "Admin" })
      : outcome === "conflict" ? json({ code: "membership_version_conflict" }, 409) : json({}, 503)));
    if (outcome === "success") {
      expect(screen.getByRole("status").textContent).toContain("Alice: access saved");
      expect(screen.queryByRole("button", { name: "Grant Admin access" })).toBeNull();
    } else {
      expect(screen.getByRole("alert").textContent).toContain(outcome === "conflict" ? "another administrator" : "could not be confirmed");
      expect(form.textContent).toContain("Alice");
      expect((screen.getByLabelText("Role") as HTMLSelectElement).value).toBe("Admin");
      expect((screen.getByRole("button", { name: "Grant Admin access" }) as HTMLButtonElement).disabled).toBe(true);
    }
    if (outcome !== "success") {
      expect((screen.getByRole("button", { name: "Edit Bob" }) as HTMLButtonElement).disabled).toBe(true);
      await act(async () => fireEvent.click(screen.getByRole("button", { name: "Reload members" })));
    }
    expect((screen.getByRole("button", { name: "Edit Bob" }) as HTMLButtonElement).disabled).toBe(false);
    fireEvent.click(screen.getByRole("button", { name: "Edit Bob" }));
    expect(screen.getByRole("button", { name: "Save changes" }).closest("form")!.textContent).toContain("Bob");
    expect((screen.getByLabelText("Role") as HTMLSelectElement).value).toBe("Staff");
  });

  it("locks creation identity fields until the grant response finishes", async () => {
    let finish!: (response: Response) => void;
    const fetcher = vi.fn((_url: string, init?: RequestInit) => init?.method
      ? new Promise<Response>(resolve => { finish = resolve; }) : Promise.resolve(json(members)));
    vi.stubGlobal("fetch", fetcher);
    render(<OrganizationPage/>);
    await screen.findByRole("button", { name: "Edit Alice" });
    fireEvent.click(screen.getByRole("button", { name: "Add person" }));
    fireEvent.change(screen.getByLabelText("Display name"), { target: { value: "Synthetic new member" } });
    fireEvent.change(screen.getByLabelText("Microsoft Entra object ID"), { target: { value: members[0].objectId } });
    fireEvent.change(screen.getByLabelText("Role"), { target: { value: "Admin" } });
    fireEvent.submit(screen.getByRole("button", { name: "Grant access" }).closest("form")!);
    expect(screen.getByLabelText("Display name").matches(":disabled")).toBe(true);
    expect(screen.getByLabelText("Microsoft Entra object ID").matches(":disabled")).toBe(true);
    expect(fetcher.mock.calls.filter(([, init]) => init?.method)).toHaveLength(1);
    await act(async () => finish(json({}, 503)));
    expect((screen.getByLabelText("Display name") as HTMLInputElement).value).toBe("Synthetic new member");
    expect(screen.getByLabelText("Display name").matches(":disabled")).toBe(false);
  });
});
