import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { LibraryList } from "./library-list";

const files = Array.from({ length: 23 }, (_, index) => ({ id: `file-${index}`, originalFilename: `Invoice ${index}.pdf`,
  status: "Ready", documentType: "invoice", sizeBytes: 100, createdAt: "2026-10-23T00:00:00Z" }));
const json = (documents: typeof files, total = documents.length, offset = 0) => new Response(JSON.stringify({ documents, total, offset, pageSize: 10 }));
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

describe("server library search", () => {
  it("requests ten-file pages, searches across pages, resets filters and keeps the selection", async () => {
    const select = vi.fn();
    const fetcher = vi.fn(async (input: string | URL | Request) => {
      const url = new URL(String(input));
      const offset = Number(url.searchParams.get("offset"));
      if (url.searchParams.get("q")) return json([files[22]]);
      return json(files.slice(offset, offset + 10), 23, offset);
    });
    vi.stubGlobal("fetch", fetcher);
    render(<LibraryList selectedId="file-0" onSelect={select} refreshVersion={0} />);
    await screen.findByText("Showing 1–10 of 23");
    expect(screen.getByRole("button", { name: /Invoice 0.pdf/ }).getAttribute("aria-pressed")).toBe("true");
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await screen.findByText("Showing 11–20 of 23");
    expect(screen.queryByRole("button", { name: /Invoice 0.pdf/ })).toBeNull();
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "Sample supplier" } });
    expect(fetcher).toHaveBeenCalledTimes(2);
    fireEvent.click(screen.getByRole("button", { name: "Search" }));
    await screen.findByRole("button", { name: /Invoice 22.pdf/ });
    expect(new URL(String(fetcher.mock.lastCall![0])).searchParams.get("offset")).toBe("0");
    fireEvent.change(screen.getByLabelText("Document type"), { target: { value: "PurchaseOrder" } });
    await waitFor(() => expect(new URL(String(fetcher.mock.lastCall![0])).searchParams.get("type")).toBe("PurchaseOrder"));
    await screen.findByText("Showing 1–1 of 1");
    expect(select).not.toHaveBeenCalled();
  });

  it("ignores an obsolete response after a new search and permits retry after failure", async () => {
    let finish!: (response: Response) => void;
    let attempts = 0;
    vi.stubGlobal("fetch", vi.fn((input: string) => {
      const term = new URL(input).searchParams.get("q");
      if (!term) return new Promise<Response>(resolve => { finish = resolve; });
      if (++attempts === 1) return Promise.resolve(new Response("{}", { status: 503 }));
      return Promise.resolve(json([files[22]]));
    }));
    render(<LibraryList onSelect={vi.fn()} refreshVersion={0} />);
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "INV-22" } });
    fireEvent.click(screen.getByRole("button", { name: "Search" }));
    fireEvent.click(await screen.findByRole("button", { name: "Try search again" }));
    await screen.findByRole("button", { name: /Invoice 22.pdf/ });
    await act(async () => finish(json([files[0]])));
    expect(screen.queryByRole("button", { name: /Invoice 0.pdf/ })).toBeNull();
    expect(screen.getByRole("button", { name: /Invoice 22.pdf/ })).toBeTruthy();
  });

  it("clears results on refresh and ignores responses after unmount", async () => {
    let finish!: (response: Response) => void;
    const fetcher = vi.fn().mockResolvedValueOnce(json([files[0]])).mockImplementation(() => new Promise<Response>(resolve => { finish = resolve; }));
    vi.stubGlobal("fetch", fetcher);
    const select = vi.fn();
    const view = render(<LibraryList onSelect={select} refreshVersion={0} />);
    await screen.findByRole("button", { name: /Invoice 0.pdf/ });
    view.rerender(<LibraryList onSelect={select} refreshVersion={1} />);
    expect(screen.queryByRole("button", { name: /Invoice 0.pdf/ })).toBeNull();
    view.unmount();
    await act(async () => finish(json([files[22]])));
    expect(select).not.toHaveBeenCalled();
  });
});
