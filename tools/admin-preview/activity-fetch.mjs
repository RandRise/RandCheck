import { activityExamples } from "./activity-fixtures.mjs";

/** The original sample server returns all events. Adapt only that local read to
 * bounded pages without restarting or resetting its existing in-memory state. */
export function createActivityPreviewFetch(fetcher, origin, examples = activityExamples) {
  return async (input, init) => {
    const url = new URL(input instanceof Request ? input.url : String(input), origin);
    const method = (init?.method ?? (input instanceof Request ? input.method : "GET")).toUpperCase();
    if (url.origin !== origin || url.pathname !== "/api/organization/audit" || method !== "GET") return fetcher(input, init);
    const offset = Number(url.searchParams.get("offset") ?? 0);
    if (!Number.isInteger(offset) || offset < 0 || offset > 10000) return new Response("{}", { status: 400 });
    const response = await fetcher(input, init);
    if (!response.ok) return response;
    const events = await response.clone().json();
    if (!Array.isArray(events)) return response;
    const rows = [...events, ...examples].sort((a, b) => Date.parse(b.recordedAtUtc) - Date.parse(a.recordedAtUtc)
      || b.id.localeCompare(a.id));
    return new Response(JSON.stringify(rows.slice(offset, offset + 100)), {
      status: 200, headers: { "Content-Type": "application/json", "Cache-Control": "no-store" },
    });
  };
}
