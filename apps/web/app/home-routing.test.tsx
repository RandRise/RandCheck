import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import Home from "./page";

vi.mock("./document-workspace", () => ({ default: () => <p>Saved document workspace</p> }));
vi.mock("./invoices/components/invoice-home", () => ({ InvoiceHome: () => <p>Saved invoice checks</p> }));
afterEach(() => cleanup());

it("opens Home normally and retains earlier document deep links through browser history", async () => {
  window.history.replaceState(null, "", "/");
  render(<Home />);
  await screen.findByText("Saved invoice checks");
  await act(async () => {
    window.history.replaceState(null, "", "/?document=earlier-document");
    window.dispatchEvent(new PopStateEvent("popstate"));
  });
  expect(screen.getByText("Saved document workspace")).toBeTruthy();
  await act(async () => {
    window.history.replaceState(null, "", "/");
    window.dispatchEvent(new PopStateEvent("popstate"));
  });
  expect(screen.getByText("Saved invoice checks")).toBeTruthy();
});
