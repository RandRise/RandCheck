import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { InvoiceHome } from "./components/invoice-home";

const access = vi.hoisted(() => ({ allowed: true, push: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: access.push }) }));
vi.mock("../security/api-fetch", () => ({
  API: "http://test",
  apiFetch: (url: string, init?: RequestInit) => fetch(url, init),
}));
vi.mock("../security/enterprise-provider", () => ({
  useEnterprise: () => ({ session: { organizationId: "org", actorId: "actor", role: "Staff" } }),
  can: () => access.allowed,
}));

const saved = {
  id: "00000000-0000-4000-8000-000000000001",
  name: "فحص فاتورة مورد — Invoice review",
  reference: "REC-2026-000028",
  status: "Draft",
};
const closed = { ...saved, id: "00000000-0000-4000-8000-000000000002", reference: "REC-2026-000029", status: "Closed" };
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), {
  status, headers: { "Content-Type": "application/json" },
});

function mockRequests(list: (url: URL) => Response | Promise<Response>) {
  const requests = vi.fn(async (url: string, init?: RequestInit) => {
    if (init?.method) throw new Error("This read-only test must not write.");
    const parsed = new URL(url);
    return parsed.pathname.endsWith("work-list") ? json([]) : list(parsed);
  });
  vi.stubGlobal("fetch", requests);
  return requests;
}

beforeEach(() => {
  access.allowed = true;
  access.push.mockReset();
  window.history.replaceState(null, "", "/");
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe("Home design retains saved-work behaviour", () => {
  it("keeps one destination per row, Arabic names and neutral closed-history wording", async () => {
    const requests = mockRequests(() => json({ cases: [saved, closed], nextOffset: null }));
    render(<InvoiceHome />);
    const region = await screen.findByRole("region", { name: "Continue your work" });
    const links = await within(region).findAllByRole("link");
    expect(links).toHaveLength(2);
    expect(links.map(link => link.getAttribute("href"))).toEqual([
      `/invoices?case=${saved.id}`, `/invoices?case=${closed.id}`,
    ]);
    expect(links[0].textContent).toBe(`Continue ${saved.reference}`);
    expect(links[1].textContent).toBe(`View results ${closed.reference}`);
    expect(screen.getByText("Closed · saved history")).toBeTruthy();
    expect(screen.getAllByText(saved.name).every(node => node.tagName === "BDI")).toBe(true);
    expect(screen.queryByText(/complete match|all checks passed/i)).toBeNull();
    expect(requests.mock.calls.every(([, init]) => !init?.method)).toBe(true);
  });

  it("announces loading, disables refresh, then shows the empty state", async () => {
    let resolve!: (response: Response) => void;
    mockRequests(() => new Promise<Response>(done => { resolve = done; }));
    render(<InvoiceHome />);
    await screen.findByText("Loading saved checks…");
    expect(screen.getByRole("button", { name: "Refresh checks" }).hasAttribute("disabled")).toBe(true);
    resolve(json({ cases: [], nextOffset: null }));
    await screen.findByText(/No saved checks yet/);
    expect(screen.queryByText("Loading saved checks…")).toBeNull();
  });

  it("preserves saved rows during refresh failure and allows a keyboard retry", async () => {
    let attempt = 0;
    mockRequests(() => ++attempt === 2 ? json({ detail: "Saved checks are unavailable." }, 503)
      : json({ cases: [saved], nextOffset: null }));
    render(<InvoiceHome />);
    await screen.findByRole("link", { name: `Continue ${saved.reference}` });
    const refresh = screen.getByRole("button", { name: "Refresh checks" });
    refresh.focus();
    await userEvent.keyboard("{Enter}");
    expect(await screen.findByRole("alert")).toHaveProperty("textContent", "Saved checks are unavailable.");
    expect(screen.getByRole("link", { name: `Continue ${saved.reference}` })).toBeTruthy();
    expect(screen.queryByText(/No saved checks yet/)).toBeNull();
    await userEvent.keyboard("{Enter}");
    await waitFor(() => expect(screen.queryByRole("alert")).toBeNull());
    expect(attempt).toBe(3);
  });

  it("appends the next saved page without duplicates or automatic navigation", async () => {
    const requests = mockRequests(url => json(url.searchParams.get("offset") === "2"
      ? { cases: [saved, closed], nextOffset: null } : { cases: [saved], nextOffset: 2 }));
    render(<InvoiceHome all />);
    await userEvent.click(await screen.findByRole("button", { name: "Load more checks" }));
    await screen.findByRole("link", { name: `View results ${closed.reference}` });
    expect(screen.getAllByRole("link")).toHaveLength(2);
    expect(screen.queryByRole("button", { name: "Load more checks" })).toBeNull();
    expect(requests.mock.calls.some(([url]) => url.includes("offset=2"))).toBe(true);
    expect(access.push).not.toHaveBeenCalled();
  });

  it("does not expose a start action without review permission", async () => {
    access.allowed = false;
    mockRequests(() => json({ cases: [closed], nextOffset: null }));
    render(<InvoiceHome />);
    await screen.findByRole("link", { name: `View results ${closed.reference}` });
    expect(screen.queryByRole("button", { name: "Start with my files" })).toBeNull();
    expect(screen.queryByRole("region", { name: "Start here" })).toBeNull();
  });
});
