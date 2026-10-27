import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import Layout from "./layout";

const state = vi.hoisted(() => ({ path: "/organization", role: "Admin" }));
vi.mock("next/navigation", () => ({ usePathname: () => state.path }));
vi.mock("../security/enterprise-provider", () => ({ useEnterprise: () => ({ session: { role: state.role, organizationName: "Example" } }) }));
afterEach(() => { cleanup(); state.role = "Admin"; });

it.each(["/organization", "/organization/audit", "/organization/retention"])("marks exactly one section on %s", path => {
  state.path = path;
  render(<Layout><p>Content</p></Layout>);
  const links = within(screen.getByRole("navigation", { name: "Administration sections" })).getAllByRole("link");
  expect(links).toHaveLength(3);
  expect(links.filter(link => link.getAttribute("aria-current") === "page").map(link => link.getAttribute("href"))).toEqual([path]);
});

it("never exposes admin children to Staff", () => {
  state.role = "Staff";
  render(<Layout><p>Private content</p></Layout>);
  expect(screen.queryByText("Private content")).toBeNull();
  expect(screen.queryByRole("navigation")).toBeNull();
});
