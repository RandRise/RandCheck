// Keep selection in the URL so refresh, bookmarks and browser Back recover work.
export const isWorkspaceId = (value: string) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
export function selectionFromUrl(key: "document" | "case") {
  return new URLSearchParams(window.location.search).get(key);
}
export function writeSelection(key: "document" | "case", id: string | null, replace = false) {
  const url = new URL(window.location.href);
  if (id) url.searchParams.set(key, id); else url.searchParams.delete(key);
  if (url.href === window.location.href) return;
  // Next integrates native history changes with its router; preserve unrelated query values.
  window.history[replace ? "replaceState" : "pushState"](null, "", `${url.pathname}${url.search}${url.hash}`);
}
