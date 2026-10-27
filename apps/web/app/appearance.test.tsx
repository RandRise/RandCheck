import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { hydrateRoot } from "react-dom/client";
import { renderToString } from "react-dom/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AppearanceProvider } from "./appearance";

const navigation = vi.hoisted(() => ({ path: "/" }));
vi.mock("next/navigation", () => ({ usePathname: () => navigation.path }));
const preferenceKey = "document-intelligence:appearance";

function choose(value: string) {
  fireEvent.change(screen.getByRole("combobox", { name: "Appearance" }), { target: { value } });
}
function expectAppearance(value: string) {
  const control = screen.getByRole("combobox", { name: "Appearance" }) as HTMLSelectElement;
  expect(control.value).toBe(value);
  expect(control.closest(".app-appearance")?.getAttribute("data-theme")).toBe(value);
}

describe("shared application appearance", () => {
  beforeEach(() => {
    vi.restoreAllMocks(); navigation.path = "/";
    // Reset the module's memory fallback through the same public control users use.
    localStorage.setItem(preferenceKey, "dark");
    render(<AppearanceProvider><p>Reset preferences</p></AppearanceProvider>);
    choose("dark"); cleanup(); localStorage.clear();
  });
  afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear(); });

  it("defaults to the approved dark appearance without an existing preference", () => {
    render(<AppearanceProvider><p>Document workspace</p></AppearanceProvider>);
    expectAppearance("dark");
    expect(screen.getByText("Document workspace")).toBeTruthy();
  });

  it("persists an explicit choice across provider remounts", () => {
    const first = render(<AppearanceProvider><p>Document workspace</p></AppearanceProvider>);
    choose("light"); expectAppearance("light");
    expect(localStorage.getItem(preferenceKey)).toBe("light");
    first.unmount();
    render(<AppearanceProvider><p>Reopened workspace</p></AppearanceProvider>);
    expectAppearance("light");
    choose("dark");
    expect(localStorage.getItem(preferenceKey)).toBe("dark");
  });

  it("ignores an invalid stored preference", () => {
    localStorage.setItem(preferenceKey, "unexpected-theme");
    render(<AppearanceProvider><p>Document workspace</p></AppearanceProvider>);
    expectAppearance("dark");
    expect(screen.getByRole("combobox", { name: "Appearance" }).querySelectorAll("option")).toHaveLength(2);
  });

  it("updates from another tab and restores the default when preferences are cleared", () => {
    render(<AppearanceProvider><p>Document workspace</p></AppearanceProvider>);
    act(() => {
      localStorage.setItem(preferenceKey, "light");
      window.dispatchEvent(new StorageEvent("storage", { key: preferenceKey, newValue: "light", storageArea: localStorage }));
    });
    expectAppearance("light");
    act(() => {
      window.dispatchEvent(new StorageEvent("storage", { key: "unrelated-preference", newValue: "dark", storageArea: localStorage }));
    });
    expectAppearance("light");
    act(() => {
      localStorage.clear();
      window.dispatchEvent(new StorageEvent("storage", { key: null, storageArea: localStorage }));
    });
    expectAppearance("dark");
  });

  it("continues to switch appearance when browser storage is unavailable", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new DOMException("Blocked", "SecurityError"); });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new DOMException("Blocked", "SecurityError"); });
    const first = render(<AppearanceProvider><p>Document workspace</p></AppearanceProvider>);
    expectAppearance("dark"); choose("light"); expectAppearance("light");
    first.unmount();
    render(<AppearanceProvider><p>Reopened workspace</p></AppearanceProvider>);
    expectAppearance("light");
  });

  it("honors a new in-memory choice when reading storage works but saving fails", () => {
    localStorage.setItem(preferenceKey, "dark");
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new DOMException("Full", "QuotaExceededError"); });
    const first = render(<AppearanceProvider><p>Document workspace</p></AppearanceProvider>);
    choose("light"); expectAppearance("light");
    expect(localStorage.getItem(preferenceKey)).toBe("dark");
    first.unmount();
    render(<AppearanceProvider><p>Reopened workspace</p></AppearanceProvider>);
    expectAppearance("light");
  });

  it("does not discard an unsaved preference for an unrelated session-storage event", () => {
    localStorage.setItem(preferenceKey, "dark");
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new DOMException("Full", "QuotaExceededError"); });
    render(<AppearanceProvider><p>Document workspace</p></AppearanceProvider>);
    choose("light");
    act(() => {
      window.dispatchEvent(new StorageEvent("storage", { key: preferenceKey, newValue: "dark", storageArea: sessionStorage }));
    });
    expectAppearance("light");
    expect(localStorage.getItem(preferenceKey)).toBe("dark");
  });

  it("preserves unsaved child state while switching appearance and route context", () => {
    function Draft() {
      const [value, setValue] = useState("");
      return <label>Review reason<input value={value} onChange={event => setValue(event.target.value)}/></label>;
    }
    const view = render(<AppearanceProvider><Draft/></AppearanceProvider>);
    fireEvent.change(screen.getByLabelText("Review reason"), { target: { value: "Verified against المصدر" } });
    choose("light"); choose("dark");
    navigation.path = "/organization";
    view.rerender(<AppearanceProvider><Draft/></AppearanceProvider>);
    expect((screen.getByLabelText("Review reason") as HTMLInputElement).value).toBe("Verified against المصدر");
    expectAppearance("dark");
  });

  it("leaves the authentication callback unwrapped without reading browser preferences", () => {
    navigation.path = "/auth/callback";
    const read = vi.spyOn(Storage.prototype, "getItem");
    const write = vi.spyOn(Storage.prototype, "setItem");
    const view = render(<AppearanceProvider><p>Authentication bridge</p></AppearanceProvider>);
    expect(screen.getByText("Authentication bridge")).toBeTruthy();
    expect(screen.queryByRole("combobox", { name: "Appearance" })).toBeNull();
    expect(view.container.querySelector(".app-appearance")).toBeNull();
    expect(read).not.toHaveBeenCalled(); expect(write).not.toHaveBeenCalled();
  });

  it("hydrates the default server appearance before applying a saved client preference", async () => {
    localStorage.setItem(preferenceKey, "light");
    const read = vi.spyOn(Storage.prototype, "getItem");
    const element = <AppearanceProvider><p>Hydrated workspace</p></AppearanceProvider>;
    const html = renderToString(element);
    expect(html).toContain('data-theme="dark"');
    expect(read).not.toHaveBeenCalled();
    const container = document.createElement("div");
    container.innerHTML = html; document.body.appendChild(container);
    const recoverable = vi.fn();
    let root!: ReturnType<typeof hydrateRoot>;
    try {
      await act(async () => { root = hydrateRoot(container, element, { onRecoverableError: recoverable }); });
      expectAppearance("light");
      expect(screen.getByText("Hydrated workspace")).toBeTruthy();
      expect(recoverable).not.toHaveBeenCalled();
    } finally {
      if (root) await act(async () => { root.unmount(); });
      container.remove();
    }
  });
});
