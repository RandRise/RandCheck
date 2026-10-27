import { afterEach, expect, it, vi } from "vitest";
import { readStorage, storageBody, storageRequest, STORAGE_BASE as base } from "./storage-api";

afterEach(() => vi.unstubAllGlobals());

const hold = { id: "hold-1", documentId: null, reason: "LegalHold", version: "v2", createdAtUtc: "2026-10-27T00:00:00Z", releasedAtUtc: null };

it.each([null, {}, { version: "v1", documentDays: null, quarantineHours: null }, { version: "v2", documentDays: 99, quarantineHours: null }])("rejects an unproven policy save: %j", async result => {
  vi.stubGlobal("fetch", vi.fn(async () => Response.json(result)));
  await expect(storageRequest(base, storageBody("PUT", { expectedVersion: "v1", documentDays: null, quarantineHours: null }))).rejects.toThrow("could not be confirmed");
});

it.each([null, {}, { ...hold, documentId: "wrong-document" }, { ...hold, reason: "Investigation" }, { ...hold, releasedAtUtc: "2026-10-27" }])("rejects an unproven or mismatched protection: %j", async result => {
  vi.stubGlobal("fetch", vi.fn(async () => Response.json(result)));
  await expect(storageRequest(`${base}/holds`, storageBody("POST", { documentId: null, reason: "LegalHold" }))).rejects.toThrow("could not be confirmed");
});

it.each([hold, { ...hold, id: "wrong-hold", releasedAtUtc: "2026-10-27" }, { ...hold, version: "v1", releasedAtUtc: "2026-10-27" }])("rejects an unproven release: %j", async result => {
  vi.stubGlobal("fetch", vi.fn(async () => Response.json(result)));
  await expect(storageRequest(`${base}/holds/hold-1/release`, storageBody("POST", { expectedVersion: "v1" }))).rejects.toThrow("could not be confirmed");
});

it("accepts the backend continuation past the last permitted request page", async () => {
  const fetcher = vi.fn(async (url: string) => Response.json(url.includes("/preview?")
    ? { documentDeletionEnabled: false, documents: [], nextOffset: 10100 }
    : url.includes("/holds?") ? [] : { version: "v1", documentDays: null, quarantineHours: null }));
  vi.stubGlobal("fetch", fetcher);
  expect((await readStorage(10000, 0)).preview.nextOffset).toBe(10100);
  expect(fetcher.mock.calls.some(([url]) => url.includes("offset=10100"))).toBe(false);
});
