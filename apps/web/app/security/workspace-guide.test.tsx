import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { EnterpriseProvider, useEnterprise, type Session } from "./enterprise-provider";

const navigation = vi.hoisted(() => ({ path: "/", replace: vi.fn() }));
vi.mock("next/navigation", () => ({ usePathname: () => navigation.path,
  useRouter: () => ({ replace: navigation.replace }) }));
const config = { mode: "Entra", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", clientId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  scope: "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/DocumentIntelligence.Access", api: "http://localhost:8080" };
const base: Session = { organizationId: "org-a", organizationName: "Example organization", actorId: "actor-a", displayName: "Example user",
  tenantId: config.tenantId, role: "Staff" };
function identity(signedIn = true) { return { initialize: vi.fn(async () => signedIn), token: vi.fn(async () => "test-token"),
  signIn: vi.fn(async () => {}), signOut: vi.fn(async () => {}) }; }
function json(value: unknown) { return new Response(JSON.stringify(value), { headers: { "Content-Type": "application/json" } }); }
function Content() {
  const { refresh } = useEnterprise(); const [draft, setDraft] = useState("");
  return <section><h2>Review workspace</h2><label>Correction reason<input value={draft} onChange={event => setDraft(event.target.value)}/></label>
    <button onClick={() => void refresh()}>Refresh access for test</button></section>;
}
function mount(session: Session = base) {
  let latest = session;
  vi.stubGlobal("fetch", vi.fn(async () => json(latest)));
  const client = identity(); const factory = async () => client;
  const element = () => <EnterpriseProvider config={config} identityFactory={factory}><Content/></EnterpriseProvider>;
  const view = render(element());
  return { changeSession: (next: Session) => { latest = next; }, changeRoute: (path: string) => { navigation.path = path; view.rerender(element()); } };
}
async function openGuide() {
  fireEvent.click(await screen.findByRole("button", { name: "Help with this page" }));
  return screen.getByRole("region", { name: "Role guide" });
}

describe("workspace role guidance", () => {
  beforeEach(() => { navigation.path = "/documents"; navigation.replace.mockClear(); });
  afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it.each(["Close guide", "Escape"])("preserves the working form and restores focus when help closes using %s", async closeMethod => {
    mount({ ...base, role: "Staff" });
    const input = await screen.findByLabelText("Correction reason") as HTMLInputElement;
    expect(screen.queryByRole("region", { name: "Role guide" })).toBeNull();
    fireEvent.change(input, { target: { value: "القيمة checked against source" } });
    const trigger = screen.getByRole("button", { name: "Help with this page" });
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    const guide = await openGuide();
    expect(within(guide).getByRole("heading", { name: "Find a saved file" })).toBeTruthy();
    expect(trigger.getAttribute("aria-expanded")).toBe("true");
    expect(trigger.getAttribute("aria-controls")).toBe(guide.id);
    const closeButton = within(guide).getByRole("button", { name: "Close guide" });
    expect(document.activeElement).toBe(closeButton);
    expect(screen.getByLabelText("Correction reason")).toBe(input);
    if (closeMethod === "Escape") fireEvent.keyDown(closeButton, { key: "Escape" });
    else fireEvent.click(closeButton);
    expect(screen.queryByRole("region", { name: "Role guide" })).toBeNull();
    expect(document.activeElement).toBe(trigger);
    expect(input.value).toBe("القيمة checked against source");
    await openGuide();
    expect(screen.getByLabelText("Correction reason")).toBe(input);
    expect(input.value).toBe("القيمة checked against source");
  });

  it.each([
    { name: "document role", patch: { role: "Admin" as const } },
    { name: "organization", patch: { organizationId: "org-b", organizationName: "Second organization" } },
    { name: "actor", patch: { actorId: "actor-b", displayName: "Another user" } },
    { name: "administration grant", patch: { role: "Admin" as const } },
  ])("dismisses old guidance when the $name changes", async ({ patch }) => {
    const workspace = mount();
    const guide = await openGuide();
    expect(within(guide).getByRole("heading", { name: "Find a saved file" })).toBeTruthy();
    workspace.changeSession({ ...base, ...patch });
    fireEvent.click(screen.getByRole("button", { name: "Refresh access for test" }));
    await waitFor(() => expect(screen.queryByRole("region", { name: "Role guide" })).toBeNull());
    const updated = await openGuide();
    expect(within(updated).getByRole("heading", { name: "Find a saved file" })).toBeTruthy();
  });

  it("updates the open guide for each route without unmounting the working form", async () => {
    const workspace = mount({ ...base, role: "Admin" });
    const guide = await openGuide();
    const input = screen.getByLabelText("Correction reason") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Work stays here" } });
    for (const [path, title] of [
      ["/", "Check an invoice in 3 steps"], ["/invoices", "Check an invoice in 3 steps"], ["/documents", "Find a saved file"],
      ["/reconciliation", "Compare reviewed documents"], ["/questions", "Ask with source evidence"],
      ["/organization", "Manage members and access"], ["/organization/audit", "Read the audit history"],
      ["/organization/retention", "Manage retention and holds"],
    ]) {
      workspace.changeRoute(path);
      expect(screen.getByRole("region", { name: "Role guide" })).toBe(guide);
      expect(within(guide).getByRole("heading", { name: title })).toBeTruthy();
      expect(screen.getByLabelText("Correction reason")).toBe(input);
      expect(input.value).toBe("Work stays here");
    }
  });

  it("explains administrator-only access without offering document workflow instructions", async () => {
    navigation.path = "/organization";
    mount({ ...base, role: "Admin" });
    const guide = await openGuide();
    expect(within(guide).getByRole("heading", { name: "Manage members and access" })).toBeTruthy();
    expect(within(guide).getByText(/You also manage users, audit history/)).toBeTruthy();
    expect(within(guide).queryByText(/Choose a document and start its review/)).toBeNull();
    expect(within(guide).queryByText(/Use Add a document to upload/)).toBeNull();
  });

  it("acknowledges both reviewer and administrator capabilities for combined access", async () => {
    mount({ ...base, role: "Admin" });
    const guide = await openGuide();
    expect(within(guide).getByRole("heading", { name: "Find a saved file" })).toBeTruthy();
    expect(within(guide).getByText(/Admin guide/)).toBeTruthy();
    expect(within(guide).getByText(/You also manage users, audit history/)).toBeTruthy();
  });

  it("offers Staff guidance for preparing sources and asking questions", async () => {
    navigation.path = "/questions"; mount({ ...base, role: "Staff" });
    const guide = await openGuide();
    expect(within(guide).getByRole("heading", { name: "Ask with source evidence" })).toBeTruthy();
    expect(within(guide).getByRole("figure", { name: "Workflow at a glance" })).toBeTruthy();
  });

  it("does not offer page guidance for a denied deep link", async () => {
    navigation.path = "/organization"; mount({ ...base, role: "Staff" });
    await screen.findByRole("heading", { name: "Administration access required." });
    expect(screen.queryByRole("button", { name: "Help with this page" })).toBeNull();
    expect(screen.queryByRole("region", { name: "Role guide" })).toBeNull();
  });

  it("does not offer workspace guidance when signed out", async () => {
    const client = identity(false);
    render(<EnterpriseProvider config={config} identityFactory={async () => client}><Content/></EnterpriseProvider>);
    await screen.findByRole("button", { name: "Continue with Microsoft" });
    expect(screen.queryByRole("button", { name: "Help with this page" })).toBeNull();
  });

  it("leaves the authentication callback free of workspace guidance", () => {
    navigation.path = "/auth/callback"; const factory = vi.fn();
    render(<EnterpriseProvider config={config} identityFactory={factory}><p>Callback bridge</p></EnterpriseProvider>);
    expect(screen.getByText("Callback bridge")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Help with this page" })).toBeNull();
    expect(factory).not.toHaveBeenCalled();
  });

  it("switches guide language without closing help or resetting an unfinished correction", async () => {
    mount({ ...base, role: "Staff" });
    const input = await screen.findByLabelText("Correction reason") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "Reviewed value مطابق للمصدر" } });
    const guide = await openGuide();
    expect(within(guide).getByRole("button", { name: "English" }).getAttribute("aria-pressed")).toBe("true");
    fireEvent.click(within(guide).getByRole("button", { name: "العربية" }));
    expect(screen.getByRole("region", { name: "Role guide" })).toBe(guide);
    expect(within(guide).getByRole("heading", { name: "العثور على ملف محفوظ" })).toBeTruthy();
    expect(within(guide).getByRole("button", { name: "العربية" }).getAttribute("aria-pressed")).toBe("true");
    expect(screen.getByLabelText("Correction reason")).toBe(input);
    expect(input.value).toBe("Reviewed value مطابق للمصدر");
    fireEvent.click(within(guide).getByRole("button", { name: "English" }));
    const content = guide.querySelector('[lang="en"][dir="ltr"]');
    expect(content).not.toBeNull();
    expect(within(content as HTMLElement).getByRole("heading", { name: "Find a saved file" })).toBeTruthy();
    expect(within(guide).getByRole("button", { name: "العربية" }).getAttribute("aria-pressed")).toBe("false");
    fireEvent.click(within(guide).getByRole("button", { name: "العربية" }));
    fireEvent.click(within(guide).getByRole("button", { name: "إغلاق الدليل" }));
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Help with this page" }));
    expect(screen.queryByRole("region", { name: "Role guide" })).toBeNull();
    expect(input.value).toBe("Reviewed value مطابق للمصدر");
    const reopened = await openGuide();
    expect(within(reopened).getByRole("heading", { name: "العثور على ملف محفوظ" })).toBeTruthy();
  });

  it.each([
    { role: "Staff" as const, admin: false, path: "/documents", title: "العثور على ملف محفوظ", nodes: 3 },
    { role: "Staff" as const, admin: false, path: "/documents", title: "العثور على ملف محفوظ", nodes: 3 },
    { role: "Admin" as const, admin: false, path: "/documents", title: "العثور على ملف محفوظ", nodes: 3 },
    { role: "Admin" as const, admin: true, path: "/organization", title: "إدارة الأعضاء والصلاحيات", nodes: 3 },
  ])("provides RTL responsibilities, limits and a translated workflow diagram for $role", async ({ role, admin, path, title, nodes }) => {
    navigation.path = path; mount({ ...base, role: admin ? "Admin" : role });
    const guide = await openGuide();
    const englishFigure = within(guide).getByRole("figure", { name: "Workflow at a glance" });
    expect(within(englishFigure).getAllByRole("listitem")).toHaveLength(nodes);
    fireEvent.click(within(guide).getByRole("button", { name: "العربية" }));
    const content = guide.querySelector('[lang="ar"][dir="rtl"]') as HTMLElement;
    expect(content).not.toBeNull();
    expect(within(content).getByRole("heading", { name: title })).toBeTruthy();
    for (const name of ["مسؤولياتك", "ما الذي تفعله في هذه الصفحة", "الصلاحيات والحدود"]) {
      expect(within(content).getByRole("heading", { name })).toBeTruthy();
    }
    const figure = within(content).getByRole("figure", { name: "خطوات العمل باختصار" });
    expect(within(figure).getAllByRole("listitem")).toHaveLength(nodes);
    expect(within(content).queryByRole("figure", { name: "Workflow at a glance" })).toBeNull();
    const arrows = [...figure.querySelectorAll(".workspace-guide-arrow")];
    expect(arrows).toHaveLength(nodes - 1);
    expect(arrows.every(arrow => arrow.textContent === "←" && arrow.getAttribute("aria-hidden") === "true")).toBe(true);
    if (path === "/documents") expect(within(figure).getByText("ابحث عن ملفك")).toBeTruthy();
    else expect(within(figure).queryByText("ابحث عن ملفك")).toBeNull();
  });

  it("keeps Arabic selected while the open guide changes between document and administration routes", async () => {
    const workspace = mount({ ...base, role: "Admin" });
    const guide = await openGuide();
    fireEvent.click(within(guide).getByRole("button", { name: "العربية" }));
    expect(within(guide).getByText(/دليل المسؤول/)).toBeTruthy();
    expect(within(guide).getByText(/تدير أيضاً المستخدمين وسجل التدقيق/)).toBeTruthy();
    for (const [path, title] of [
      ["/reconciliation", "مطابقة المستندات المراجعة"], ["/questions", "طرح أسئلة تستند إلى المصادر"],
      ["/organization", "إدارة الأعضاء والصلاحيات"], ["/organization/audit", "قراءة سجل التدقيق"],
      ["/organization/retention", "إدارة الاحتفاظ بالمستندات وتعليق حذفها"],
    ]) {
      workspace.changeRoute(path);
      expect(screen.getByRole("region", { name: "Role guide" })).toBe(guide);
      expect(within(guide).getByRole("heading", { name: title })).toBeTruthy();
      expect(guide.querySelector('[lang="ar"][dir="rtl"]')).not.toBeNull();
      expect(within(guide).getByRole("figure", { name: "خطوات العمل باختصار" })).toBeTruthy();
    }
  });

  it("explains Staff source selection in Arabic", async () => {
    navigation.path = "/questions"; mount({ ...base, role: "Staff" });
    const guide = await openGuide();
    fireEvent.click(within(guide).getByRole("button", { name: "العربية" }));
    expect(within(guide).getByRole("heading", { name: "طرح أسئلة تستند إلى المصادر" })).toBeTruthy();
    expect(within(guide).getByText(/ترفع المستندات وتتحقق من قيمها/)).toBeTruthy();
    const figure = within(guide).getByRole("figure", { name: "خطوات العمل باختصار" });
    expect(within(figure).getAllByRole("listitem").length).toBeGreaterThan(0);
    expect(within(figure).getByText("اختر المصادر المراجعة")).toBeTruthy();
  });
});
