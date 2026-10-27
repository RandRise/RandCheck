import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { EnterpriseProvider, useEnterprise, type Session } from "./enterprise-provider";
import { AppearanceProvider } from "../appearance";

const navigation = vi.hoisted(() => ({ path: "/", replace: vi.fn() }));
vi.mock("next/navigation", () => ({ usePathname: () => navigation.path,
  useRouter: () => ({ replace: navigation.replace }) }));
const config = { mode: "Entra", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", clientId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  scope: "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/DocumentIntelligence.Access", api: "http://localhost:8080" };
const base: Session = { organizationId: "org-a", organizationName: "Example organization", actorId: "actor-a", displayName: "Example user",
  tenantId: config.tenantId, role: "Staff" };
const routes = [
  ["Home", "/"], ["Document library", "/documents"],
  ["Administration", "/organization"],
] as const;
function identity() { return { initialize: vi.fn(async () => true), token: vi.fn(async () => "test-token"),
  signIn: vi.fn(async () => {}), signOut: vi.fn(async () => {}) }; }
function json(value: unknown, status = 200) { return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json" } }); }
function Content() {
  const { refresh } = useEnterprise();
  const [draft, setDraft] = useState("");
  return <section><h2>Private screen</h2><label>Private draft<input value={draft} onChange={event => setDraft(event.target.value)}/></label>
    <button onClick={() => void refresh()}>Refresh access for test</button></section>;
}
function expectNoWorkspaceLinks() {
  for (const [name] of routes) expect(screen.queryByRole("link", { name })).toBeNull();
}

describe("workspace navigation through the enterprise session", () => {
  beforeEach(() => { navigation.path = "/"; navigation.replace.mockClear(); });
  afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it.each([
    { role: "Staff", visible: ["Home", "Document library"] },
    { role: "Admin", visible: routes.map(([name]) => name) },
  ] as const)("offers only permitted destinations for $role", async ({ role, visible }) => {
    vi.stubGlobal("fetch", vi.fn(async () => json({ ...base, role })));
    const client = identity();
    render(<EnterpriseProvider config={config} identityFactory={async () => client}><Content/></EnterpriseProvider>);
    await screen.findByRole("heading", { name: "Private screen" });
    for (const [name, href] of routes) {
      const link = screen.queryByRole("link", { name });
      if ((visible as readonly string[]).includes(name)) {
        expect(link?.getAttribute("href")).toBe(href);
        expect(link?.querySelector('svg[aria-hidden="true"]')).toBeTruthy();
        expect(link?.querySelector("svg")?.getAttribute("focusable")).toBe("false");
      }
      else expect(link).toBeNull();
    }
  });

  it.each([...routes, ["Administration", "/organization/audit"] as const, ["Administration", "/organization/retention"] as const])("marks only %s as the active destination on %s", async (name, path) => {
    navigation.path = path;
    vi.stubGlobal("fetch", vi.fn(async () => json({ ...base, role: "Admin" })));
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><Content/></EnterpriseProvider>);
    await screen.findByRole("heading", { name: "Private screen" });
    for (const [label] of routes) expect(screen.getByRole("link", { name: label }).getAttribute("aria-current")).toBe(label === name ? "page" : null);
  });

  it("has one Home destination and a non-navigating brand", async () => {
    navigation.path = "/organization";
    vi.stubGlobal("fetch", vi.fn(async () => json({ ...base, role: "Admin" })));
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><Content/></EnterpriseProvider>);
    await screen.findByRole("heading", { name: "Private screen" });
    expect(screen.queryByRole("link", { name: /Document (intelligence|review)/i })).toBeNull();
    expect(screen.getAllByRole("link").filter(link => link.getAttribute("href") === "/")).toHaveLength(1);
  });

  it("keeps Home active for the workflow and exposes historical tools in one expandable group", async () => {
    navigation.path = "/invoices";
    vi.stubGlobal("fetch", vi.fn(async () => json(base)));
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><Content/></EnterpriseProvider>);
    await screen.findByRole("heading", { name: "Private screen" });
    expect(screen.getByRole("link", { name: "Home" }).getAttribute("aria-current")).toBe("page");
    expect(screen.queryByRole("link", { name: "All checks" })).toBeNull();
    const group = screen.getByText("Other tools").closest("details")!;
    expect(group.open).toBe(false);
    fireEvent.click(screen.getByText("Other tools"));
    expect(screen.getByRole("link", { name: "Earlier cases and history" }).getAttribute("href")).toBe("/reconciliation");
    expect(screen.getByRole("link", { name: "Ask about documents" }).getAttribute("href")).toBe("/questions");
  });

  it.each([
    { role: "Staff", admin: false, path: "/organization/audit", denied: "Administration access required." },
    { role: "None", admin: true, path: "/reconciliation", denied: "Access needs attention." },
    { role: "None", admin: true, path: "/questions", denied: "Access needs attention." },
  ])("blocks unauthorized deep links to $path for $role", async ({ role, admin, path, denied }) => {
    navigation.path = path;
    vi.stubGlobal("fetch", vi.fn(async () => json({ ...base, role })));
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><Content/></EnterpriseProvider>);
    await screen.findByRole("heading", { name: denied });
    expect(screen.queryByRole("heading", { name: "Private screen" })).toBeNull();
    expect(screen.queryByLabelText("Private draft")).toBeNull();
  });

  it("does not expose the workspace while the initial session is pending", async () => {
    let resolve!: (value: Response) => void;
    vi.stubGlobal("fetch", vi.fn(() => new Promise<Response>(done => { resolve = done; })));
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><Content/></EnterpriseProvider>);
    await screen.findByRole("heading", { name: "Opening your workspace…" });
    expectNoWorkspaceLinks();
    expect(screen.queryByLabelText("Private draft")).toBeNull();
    await waitFor(() => expect(resolve).toBeDefined());
    resolve(json(base));
    await screen.findByRole("link", { name: "Document library" });
  });

  it("removes navigation and private state before a slow sign-out finishes", async () => {
    const client = identity(); client.signOut = vi.fn(() => new Promise<void>(() => {}));
    vi.stubGlobal("fetch", vi.fn(async () => json(base)));
    render(<EnterpriseProvider config={config} identityFactory={async () => client}><Content/></EnterpriseProvider>);
    await screen.findByRole("link", { name: "Document library" });
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));
    await screen.findByRole("button", { name: "Continue with Microsoft" });
    expectNoWorkspaceLinks();
    expect(screen.queryByLabelText("Private draft")).toBeNull();
    expect(client.signOut).toHaveBeenCalledOnce();
  });

  it("keeps the redirect callback independent of the workspace frame", () => {
    navigation.path = "/auth/callback";
    const factory = vi.fn(); const fetcher = vi.fn(); vi.stubGlobal("fetch", fetcher);
    render(<EnterpriseProvider config={config} identityFactory={factory}><p>Identity bridge</p></EnterpriseProvider>);
    expect(screen.getByText("Identity bridge")).toBeTruthy();
    expectNoWorkspaceLinks();
    expect(screen.queryByRole("button", { name: "Sign out" })).toBeNull();
    expect(factory).not.toHaveBeenCalled(); expect(fetcher).not.toHaveBeenCalled();
  });

  it("refreshes navigation and clears private drafts when memberships change", async () => {
    let latest = { ...base, role: "Admin" };
    vi.stubGlobal("fetch", vi.fn(async () => json(latest)));
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><Content/></EnterpriseProvider>);
    await screen.findByRole("link", { name: "Administration" });
    fireEvent.change(screen.getByLabelText("Private draft"), { target: { value: "Sensitive unfinished work" } });
    latest = { ...base, role: "Staff" };
    fireEvent.click(screen.getByRole("button", { name: "Refresh access for test" }));
    await waitFor(() => expect(screen.queryByRole("link", { name: "Administration" })).toBeNull());
    expect(screen.getByRole("link", { name: "Home" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Document library" })).toBeTruthy();
    expect((screen.getByLabelText("Private draft") as HTMLInputElement).value).toBe("");
  });

  it("removes all workspace links after access is revoked at the API", async () => {
    let revoked = false;
    vi.stubGlobal("fetch", vi.fn(async () => revoked ? json({}, 403) : json(base)));
    render(<EnterpriseProvider config={config} identityFactory={async () => identity()}><Content/></EnterpriseProvider>);
    await screen.findByRole("link", { name: "Document library" });
    revoked = true;
    fireEvent.click(screen.getByRole("button", { name: "Refresh access for test" }));
    await screen.findByRole("heading", { name: "Access needs attention." });
    expectNoWorkspaceLinks();
    expect(screen.queryByLabelText("Private draft")).toBeNull();
  });

  it("offers one appearance control without losing the draft when the composed workspace changes theme", async () => {
    localStorage.setItem("document-intelligence:appearance", "dark");
    vi.stubGlobal("fetch", vi.fn(async () => json(base)));
    render(<AppearanceProvider showControl={false}>
      <EnterpriseProvider config={config} identityFactory={async () => identity()}><Content/></EnterpriseProvider>
    </AppearanceProvider>);
    await screen.findByRole("heading", { name: "Private screen" });
    const controls = screen.getAllByRole("combobox", { name: "Appearance" });
    expect(controls).toHaveLength(1);
    const input = screen.getByLabelText("Private draft") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Unsaved review explanation" } });
    fireEvent.change(controls[0], { target: { value: "light" } });
    expect(input.closest(".app-appearance")?.getAttribute("data-theme")).toBe("light");
    expect(screen.getByLabelText("Private draft")).toBe(input);
    expect(input.value).toBe("Unsaved review explanation");
    fireEvent.change(controls[0], { target: { value: "dark" } });
    expect(input.closest(".app-appearance")?.getAttribute("data-theme")).toBe("dark");
    expect(input.value).toBe("Unsaved review explanation");
    expect(screen.getAllByRole("combobox", { name: "Appearance" })).toHaveLength(1);
  });

  it("offers exactly one working appearance control before signing in", async () => {
    navigation.path = "/signin";
    localStorage.setItem("document-intelligence:appearance", "dark");
    const client = identity(); client.initialize.mockResolvedValue(false);
    render(<AppearanceProvider showControl={false}>
      <EnterpriseProvider config={config} identityFactory={async () => client}><Content/></EnterpriseProvider>
    </AppearanceProvider>);
    await screen.findByRole("button", { name: "Continue with Microsoft" });
    const controls = screen.getAllByRole("combobox", { name: "Appearance" });
    expect(controls).toHaveLength(1);
    fireEvent.change(controls[0], { target: { value: "light" } });
    expect(controls[0].closest(".app-appearance")?.getAttribute("data-theme")).toBe("light");
    expect(screen.getByRole("button", { name: "Continue with Microsoft" })).toBeTruthy();
    expect(screen.queryByLabelText("Private draft")).toBeNull();
    expectNoWorkspaceLinks();
    fireEvent.change(controls[0], { target: { value: "dark" } });
  });
});
