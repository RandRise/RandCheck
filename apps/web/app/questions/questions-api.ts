import { API, apiFetch } from "../security/api-fetch";

export async function request<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const response = await apiFetch(`${API}${path}`, { method: body === undefined ? "GET" : "POST",
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body), cache: "no-store", signal });
  if (!response.ok) {
    const messages: Record<number, string> = { 400: "Check the question and source selection.",
      404: "This source or saved result is unavailable to this user.",
      409: "The source or request is no longer eligible, or the request is still running. Reload its status before retrying.",
      413: "This source page is too large for the evidence viewer.",
      422: "Semantic search is not available for this question and selection yet. Use keyword search or reviewed fields.",
      503: "The processing service is unavailable. Your selection has been kept.",
      504: "The request timed out. Retry with the same request key to check for a saved result." };
    throw new Error(messages[response.status] ?? "The request could not be completed. Please try again.");
  }
  return await response.json() as T;
}

