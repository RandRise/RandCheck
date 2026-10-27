import { API, apiFetch } from "../../security/api-fetch";
import type { ActivityEvent } from "./activity.types";

// EnterpriseMembershipService.AuditAsync returns 100 rows, with offset <= 10,000.
export const ACTIVITY_PAGE_SIZE = 100;
export const ACTIVITY_MAX_OFFSET = 10000;

export async function readActivity(offset: number, signal: AbortSignal): Promise<ActivityEvent[]> {
  if (!Number.isInteger(offset) || offset < 0 || offset > ACTIVITY_MAX_OFFSET || offset % ACTIVITY_PAGE_SIZE) {
    throw new Error("Invalid activity page");
  }
  const response = await apiFetch(`${API}/api/organization/audit?offset=${offset}`, { signal });
  if (!response.ok) throw new Error("Activity could not be loaded.");
  const rows: unknown = await response.json();
  if (!Array.isArray(rows) || rows.length > ACTIVITY_PAGE_SIZE || rows.some(row =>
    !row || ["id", "actorKind", "action", "targetType", "outcome", "reasonCode", "requestId", "recordedAtUtc"].some(key => typeof row[key] !== "string") ||
    ["actorId", "targetId", "beforeState", "afterState"].some(key => row[key] !== null && typeof row[key] !== "string"))) {
    throw new Error("Activity response was incomplete.");
  }
  return rows as ActivityEvent[];
}
