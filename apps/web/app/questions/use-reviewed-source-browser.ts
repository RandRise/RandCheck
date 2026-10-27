"use client";

import { useEffect, useState } from "react";
import { request } from "./questions-api";
import { REVIEW_PAGE_SIZE } from "./questions.constants";
import type { SourceList } from "./questions-sources.types";

/** Searches all accessible completed reviews; each response replaces one bounded page.
 * Browsing never prepares an index or changes the reviews already selected for a question.
 */
export function useReviewedSourceBrowser(enabled: boolean) {
  const [query, setQuery] = useState({ search: "", offset: 0, attempt: 0 });
  const [searchDraft, setSearchDraft] = useState("");
  const [settled, setSettled] = useState<{ query: typeof query; page: SourceList; failed: boolean } | null>(null);
  const page = settled?.page ?? { sources: [], nextOffset: null };
  const sourceLoading = enabled && settled?.query !== query;
  const sourceError = settled?.query === query && settled.failed;

  useEffect(() => {
    if (!enabled) return;
    const controller = new AbortController();
    const parameters = new URLSearchParams();
    if (query.search) parameters.set("q", query.search);
    if (query.offset) parameters.set("offset", String(query.offset));
    const suffix = parameters.size ? `?${parameters}` : "";
    request<SourceList>(`/api/cited-sources${suffix}`, undefined, controller.signal)
      .then(data => { if (!controller.signal.aborted) setSettled({ query, page: data, failed: false }); })
      .catch(() => { if (!controller.signal.aborted) setSettled({ query, page: { sources: [], nextOffset: null }, failed: true }); });
    return () => controller.abort();
  }, [enabled, query]);

  function searchSources(clear = false) {
    if (clear) setSearchDraft("");
    setQuery(current => ({ search: clear ? "" : searchDraft.trim(), offset: 0, attempt: current.attempt + 1 }));
  }

  function sourcePage(next: boolean) {
    const offset = next ? page.nextOffset : Math.max(0, query.offset - REVIEW_PAGE_SIZE);
    if (sourceLoading || offset === null) return;
    setQuery(current => ({ ...current, offset }));
  }

  function retrySources() { setQuery(current => ({ ...current, attempt: current.attempt + 1 })); }

  return { sources: sourceLoading || sourceError ? [] : page.sources,
    nextOffset: page.nextOffset, sourceOffset: query.offset, sourceLoading, sourceError,
    searchDraft, setSearchDraft, searchSources, sourcePage, retrySources };
}
