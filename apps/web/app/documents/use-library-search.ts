import { useEffect, useState } from "react";
import { searchLibrary } from "./library-service";
import { EMPTY_LIBRARY_QUERY, type LibraryPage, type LibraryQuery } from "./library.types";

/** A changed search never displays a previous response as its results. Abort and
 * disposal checks also protect against transports that finish after cancellation. */
export function useLibrarySearch(refreshVersion: number) {
  const [query, setQuery] = useState(EMPTY_LIBRARY_QUERY);
  const [retry, setRetry] = useState(0);
  const key = JSON.stringify([query, retry, refreshVersion]);
  const [result, setResult] = useState<{ key: string; page?: LibraryPage; error?: string }>();

  useEffect(() => {
    const controller = new AbortController();
    let disposed = false;
    void searchLibrary(query, controller.signal).then(page => {
      if (!disposed) setResult({ key, page });
    }).catch(error => {
      if (!disposed) setResult({ key, error: error instanceof Error ? error.message : "Document search failed." });
    });
    return () => { disposed = true; controller.abort(); };
  }, [key, query]);

  const current = result?.key === key ? result : undefined;
  function search(next: Partial<LibraryQuery>) {
    setQuery(value => ({ ...value, ...next, offset: 0 }));
    setRetry(value => value + 1);
  }

  return { query, page: current?.page, error: current?.error, loading: !current,
    search, move: (offset: number) => setQuery(value => ({ ...value, offset })), retry: () => setRetry(value => value + 1) };
}
