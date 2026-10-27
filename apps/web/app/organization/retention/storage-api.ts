import { API, apiFetch } from "../../security/api-fetch";
import type { StorageHold, StoragePolicy, StoragePreview } from "./storage.types";

export const STORAGE_BASE = "/api/organization/retention";
export const STORAGE_PAGE_SIZE = 100;
export const STORAGE_MAX_OFFSET = 10000;

export class StorageRequestError extends Error {
  constructor(message: string, readonly needsRefresh: boolean) { super(message); }
}

function isPolicy(value: StoragePolicy | null): value is StoragePolicy {
  return !!value && typeof value.version === "string" && value.version.length > 0 &&
    [value.documentDays, value.quarantineHours].every(item => item === null || Number.isInteger(item) && item > 0);
}

function isHold(value: StorageHold | null): value is StorageHold {
  return !!value && [value.id, value.version, value.reason, value.createdAtUtc].every(item => typeof item === "string" && item.length > 0) &&
    (value.documentId === null || typeof value.documentId === "string") &&
    (value.releasedAtUtc === null || typeof value.releasedAtUtc === "string");
}

/** Never replay a write: a lost response can follow a committed change. */
export async function storageRequest<T>(path: string, init?: RequestInit): Promise<T> {
  try {
    const response = await apiFetch(`${API}${path}`, init);
    if (!response.ok) {
      if (response.status >= 500 || response.status === 408) throw new Error("Uncertain response");
      throw new StorageRequestError(response.status === 409
        ? "The saved settings or protection changed. Refresh controls before choosing the change again."
        : response.status === 400 ? "Check the settings and document ID."
        : "The request was not allowed or the record is unavailable. Refresh controls to check access.", response.status !== 400);
    }
    const result = await response.json();
    // A successful HTTP status alone does not prove that the intended write happened.
    if (init?.method === "PUT" && path === STORAGE_BASE) {
      const expected = JSON.parse(String(init.body));
      if (!isPolicy(result) || result.version === expected.expectedVersion ||
        result.documentDays !== expected.documentDays || result.quarantineHours !== expected.quarantineHours) throw new Error("Unconfirmed policy");
    }
    if (init?.method === "POST" && path.startsWith(`${STORAGE_BASE}/holds`)) {
      const expected = JSON.parse(String(init.body));
      if (!isHold(result)) throw new Error("Unconfirmed protection");
      if (path.endsWith("/release")) {
        if (result.id !== path.split("/").at(-2) || !result.releasedAtUtc || result.version === expected.expectedVersion) throw new Error("Unconfirmed release");
      } else if (result.documentId?.toLowerCase() !== expected.documentId?.toLowerCase() || result.reason !== expected.reason || result.releasedAtUtc !== null) {
        throw new Error("Unconfirmed protection target");
      }
    }
    if (path.endsWith("/quarantine/cleanup")) {
      const expected = JSON.parse(String(init?.body)).execute;
      if (!result || result.executed !== expected || typeof result.truncated !== "boolean" ||
        ![result.eligible, result.deleted, result.preserved].every(value => Number.isInteger(value) && value >= 0)) {
        throw new Error("Incomplete cleanup result");
      }
    }
    return result;
  } catch (error) {
    if (error instanceof StorageRequestError) throw error;
    throw new StorageRequestError("The result could not be confirmed. Refresh controls and check Activity history before choosing another change.", true);
  }
}

export const storageBody = (method: string, value: unknown): RequestInit => ({
  method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(value),
});

export async function readStorage(offset: number, holdOffset: number, signal?: AbortSignal) {
  const [policy, holds, preview] = await Promise.all([
    storageRequest<StoragePolicy>(STORAGE_BASE, { signal }),
    storageRequest<StorageHold[]>(`${STORAGE_BASE}/holds?offset=${holdOffset}`, { signal }),
    storageRequest<StoragePreview>(`${STORAGE_BASE}/preview?offset=${offset}`, { signal }),
  ]);
  if (!isPolicy(policy) ||
    !Array.isArray(holds) || holds.length > STORAGE_PAGE_SIZE || holds.some(hold => !isHold(hold)) ||
    !preview || typeof preview.documentDeletionEnabled !== "boolean" || !Array.isArray(preview.documents) ||
    preview.documents.length > STORAGE_PAGE_SIZE || preview.documents.some(item => !item || ![item.documentId, item.createdAtUtc, item.disposition].every(value => typeof value === "string")) ||
    !(preview.nextOffset === null || preview.nextOffset === offset + STORAGE_PAGE_SIZE)) {
    throw new StorageRequestError("Storage controls returned incomplete information. Refresh to try again.", true);
  }
  return { policy, holds, preview };
}
