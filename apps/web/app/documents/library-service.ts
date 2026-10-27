import { API, apiFetch } from "../security/api-fetch";
import { LIBRARY_PAGE_SIZE, type LibraryPage, type LibraryQuery } from "./library.types";

export async function searchLibrary(query: LibraryQuery, signal: AbortSignal): Promise<LibraryPage> {
  const params = new URLSearchParams({ q: query.search, type: query.type, includeQuotations: String(query.includeQuotations), offset: String(query.offset) });
  const response = await apiFetch(`${API}/api/documents/library?${params}`, { signal, cache: "no-store" });
  if (!response.ok) throw new Error("The document search could not be loaded. Try again.");
  const page = await response.json() as LibraryPage;
  if (!Array.isArray(page.documents) || page.pageSize !== LIBRARY_PAGE_SIZE || page.documents.length > LIBRARY_PAGE_SIZE ||
      !Number.isInteger(page.total) || page.total < 0 || !Number.isInteger(page.offset) || page.offset < 0)
    throw new Error("The document search returned an invalid page. Try again.");
  return page;
}
