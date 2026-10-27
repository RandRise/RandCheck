import { API, apiFetch } from "./security/api-fetch";
import { ApiRequestError, requireSuccess } from "./review-request.mjs";
import { isWorkspaceId } from "./workspace-location";

export type ProcessingStatus = {
  jobId: string; extractionRunId: string; status: "Queued" | "Processing" | "Ready" | "Partial" | "Failed" | "Cancelled";
  attemptCount?: number; retryAt?: string; errorCode?: string; errorMessage?: string; replayed?: boolean;
};
export type ProcessingIntent = { fingerprint: string; requestKey: string; file?: File };

export function processingIntentStorageKey(kind: "upload" | "reprocess", organizationId: string, actorId: string, documentId?: string) {
  return `document-${kind}:${organizationId}:${actorId}${documentId ? `:${documentId}` : ""}`;
}

export async function uploadFingerprint(file: File) {
  let fingerprint = JSON.stringify([file.name, file.type, file.size]);
  const durable = !!crypto.subtle;
  if (durable) {
    const bytes = await new Promise<ArrayBuffer>((resolve, reject) => {
      const reader = new FileReader(); reader.onload = () => resolve(reader.result as ArrayBuffer);
      reader.onerror = () => reject(new Error("Could not read the selected file.")); reader.readAsArrayBuffer(file);
    });
    const digest = await crypto.subtle.digest("SHA-256", bytes);
    fingerprint += `:${Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("")}`;
  }
  return { fingerprint, durable };
}

function savedIntent(storageKey: string): ProcessingIntent | null {
  try {
    const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? "null");
    return typeof saved?.fingerprint === "string" && isWorkspaceId(saved.requestKey) ? saved : null;
  } catch { return null; } // Mounted-page retries also work without browser storage.
}

function saveIntent(storageKey: string, intent: ProcessingIntent) {
  try { sessionStorage.setItem(storageKey, JSON.stringify({ fingerprint: intent.fingerprint, requestKey: intent.requestKey })); }
  catch { /* Browser storage is optional. */ }
}

export function prepareUploadIntent(current: ProcessingIntent | null, file: File, fingerprint: string, durable: boolean, storageKey: string) {
  let intent = current ?? (durable ? savedIntent(storageKey) : null);
  if (intent?.fingerprint !== fingerprint || !durable && intent?.file !== file)
    intent = { fingerprint, requestKey: crypto.randomUUID(), file };
  if (durable) saveIntent(storageKey, intent!);
  return intent!;
}

export function prepareReprocessIntent(current: ProcessingIntent | null, documentId: string, storageKey: string) {
  let intent = current;
  if (!intent || intent.fingerprint !== documentId) {
    const saved = savedIntent(storageKey);
    intent = saved?.fingerprint === documentId ? saved : { fingerprint: documentId, requestKey: crypto.randomUUID() };
  }
  saveIntent(storageKey, intent);
  return intent;
}

export function clearProcessingIntent(storageKey: string, requestKey: string | undefined) {
  try { if (JSON.parse(sessionStorage.getItem(storageKey) ?? "null")?.requestKey === requestKey) sessionStorage.removeItem(storageKey); }
  catch { /* Browser storage is optional. */ }
}

export function needsSeparateProcessingIntent(error: unknown) {
  return error instanceof ApiRequestError && ["operation_payload_changed", "operation_source_unavailable"].includes(error.code ?? "");
}

export async function uploadToQueue<T extends { id: string }>(file: File, requestKey: string): Promise<T> {
  const body = new FormData(); body.append("file", file);
  const response = await apiFetch(`${API}/api/documents`, { method: "POST", headers: { "X-Operation-Key": requestKey }, body });
  await requireSuccess(response);
  const result = await response.json() as T;
  if (typeof result.id !== "string" || !result.id) throw new Error("The upload response did not identify the saved document.");
  return result;
}

export async function queueReprocessing(documentId: string, requestKey: string): Promise<ProcessingStatus> {
  const response = await apiFetch(`${API}/api/documents/${documentId}/reprocess`, { method: "POST", headers: { "X-Operation-Key": requestKey } });
  await requireSuccess(response);
  const state = await response.json() as ProcessingStatus;
  if (typeof state.jobId !== "string" || !state.jobId || typeof state.extractionRunId !== "string" || !state.extractionRunId)
    throw new Error("The processing response did not identify the queued run.");
  return state;
}

export async function cancelQueuedProcessing(jobId: string) {
  await requireSuccess(await apiFetch(`${API}/api/extraction-jobs/${jobId}/cancel`, { method: "POST" }));
}
