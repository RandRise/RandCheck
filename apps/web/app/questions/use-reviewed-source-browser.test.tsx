import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { useReviewedSourceBrowser } from "./use-reviewed-source-browser";

vi.mock("../security/api-fetch", () => ({ API: "http://test", apiFetch: (url: string, init?: RequestInit) => fetch(url, init) }));
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
const page = (filename: string) => ({ sources: [{ filename, documentId: filename, reviewSessionId: filename, extractionRunId: filename, revisionNumber: 1 }], nextOffset: 10 });
const json = (value: unknown) => new Response(JSON.stringify(value));

it("ignores a late old search after clearing, even when the transport ignores abort", async () => {
  let finish!: (response: Response) => void;
  const fetcher = vi.fn((raw: string) => new URL(raw).searchParams.has("q")
    ? new Promise<Response>(resolve => { finish = resolve; }) : Promise.resolve(json(page("Current page"))));
  vi.stubGlobal("fetch", fetcher);
  const { result } = renderHook(() => useReviewedSourceBrowser(true));
  await waitFor(() => expect(result.current.sourceLoading).toBe(false));
  act(() => result.current.setSearchDraft("Old search"));
  act(() => result.current.searchSources());
  await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2));
  act(() => result.current.searchSources(true));
  await waitFor(() => expect(result.current.sourceLoading).toBe(false));
  await act(async () => { finish(json(page("Stale search"))); });
  expect(result.current.sources[0].filename).toBe("Current page");
  expect(result.current.searchDraft).toBe("");
});

it("ignores an abandoned next page when the case-only view closes browsing", async () => {
  let finish!: (response: Response) => void;
  const fetcher = vi.fn((raw: string) => new URL(raw).searchParams.has("offset")
    ? new Promise<Response>(resolve => { finish = resolve; }) : Promise.resolve(json(page("First page"))));
  vi.stubGlobal("fetch", fetcher);
  const { result, rerender } = renderHook(({ enabled }) => useReviewedSourceBrowser(enabled), { initialProps: { enabled: false } });
  expect(fetcher).not.toHaveBeenCalled();
  rerender({ enabled: true });
  await waitFor(() => expect(result.current.sourceLoading).toBe(false));
  act(() => result.current.sourcePage(true));
  await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(2));
  rerender({ enabled: false });
  await act(async () => { finish(json(page("Abandoned page"))); });
  expect(result.current.sources.some(source => source.filename === "Abandoned page")).toBe(false);
});
