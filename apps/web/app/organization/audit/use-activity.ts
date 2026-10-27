import { useEffect, useState } from "react";
import { ACTIVITY_MAX_OFFSET, ACTIVITY_PAGE_SIZE, readActivity } from "./activity-api";
import type { ActivityEvent } from "./activity.types";

type Query = { offset: number; revision: number };
type Result = { query: Query | null; events: ActivityEvent[]; error: string };

export function useActivity() {
  const [query, setQuery] = useState<Query>({ offset: 0, revision: 0 });
  const [result, setResult] = useState<Result>({ query: null, events: [], error: "" });
  const loading = result.query !== query;

  useEffect(() => {
    const request = new AbortController();
    void readActivity(query.offset, request.signal).then(events => {
      if (!request.signal.aborted) setResult({ query, events, error: "" });
    }).catch(() => {
      if (!request.signal.aborted) setResult({ query, events: [], error: "Activity could not be loaded. Try this page again." });
    });
    return () => request.abort();
  }, [query]);

  function changePage(offset: number) {
    if (loading || offset < 0 || offset > ACTIVITY_MAX_OFFSET) return;
    setQuery({ offset, revision: query.revision + 1 });
  }

  return {
    loading, error: loading ? "" : result.error, events: loading ? [] : result.events,
    offset: query.offset, page: query.offset / ACTIVITY_PAGE_SIZE + 1,
    newer: () => changePage(query.offset - ACTIVITY_PAGE_SIZE),
    older: () => changePage(query.offset + ACTIVITY_PAGE_SIZE),
    refresh: () => changePage(0), retry: () => changePage(query.offset),
  };
}
