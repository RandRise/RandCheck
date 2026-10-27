import { API, apiFetch } from "../../security/api-fetch";

export type Plan = { documentId: string; documentVersion: string; policyVersion: string; enabled: boolean; blockers: string[]; extractionRuns: number; extractionJobs: number };
export type Deletion = { documentId: string; documentVersion: string; policyVersion: string; state: string; requestedAtUtc: string; completedAtUtc: string | null };
export const deletionPath = (id: string) => `/api/organization/retention/documents/${encodeURIComponent(id)}/deletion`;
export const deletionReasons: Record<string, string> = { deployment_disabled: "Deletion is disabled for this deployment.", retention_not_configured: "No document retention window is configured.", retention_window_active: "The document is still within its retention window.", retention_hold_active: "An active hold prevents removal.", processing_active: "Processing must finish or be cancelled first.", historical_dependencies: "Review, reconciliation or cited evidence depends on this document.", accounting_dependencies: "Extraction accounting or a non-local provider record must be retained." };
export class DeletionRequestError extends Error {}
export async function deletionRequest<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await apiFetch(`${API}${url}`, options);
  if (!response.ok) throw new DeletionRequestError(response.status === 409 ? "The document, policy or hold state changed. Inspect it again before continuing." :
    response.status === 404 ? "The document is unavailable in this organization. Refresh deletion history if a previous request may have completed." :
    "The request could not be completed. Refresh deletion history and check the audit trail before retrying.");
  const result: unknown = await response.json();
  const validRecord = (value: unknown): value is Deletion => {
    if (!value || typeof value !== "object") return false;
    const record = value as Deletion;
    return [record.documentId, record.documentVersion, record.policyVersion, record.requestedAtUtc].every(item => typeof item === "string") &&
      ["Completed", "PendingFileRemoval"].includes(record.state) && (record.completedAtUtc === null || typeof record.completedAtUtc === "string");
  };
  if (url.includes("/deletions?")) {
    if (!Array.isArray(result) || result.length > 100 || !result.every(validRecord)) throw new DeletionRequestError("Removal history returned incomplete information. Refresh before continuing.");
  } else if (options?.method === "POST") {
    if (!validRecord(result) || result.documentId.toLowerCase() !== decodeURIComponent(url.split("/").at(-2)!).toLowerCase()) {
      throw new DeletionRequestError("The removal result could not be confirmed. Refresh deletion history before choosing another action.");
    }
  } else {
    const plan = result as Plan | null;
    if (!plan || ![plan.documentId, plan.documentVersion, plan.policyVersion].every(value => typeof value === "string") ||
      typeof plan.enabled !== "boolean" || !Array.isArray(plan.blockers) || !plan.blockers.every(code => typeof code === "string") ||
      ![plan.extractionRuns, plan.extractionJobs].every(value => Number.isInteger(value) && value >= 0)) {
      throw new DeletionRequestError("The inspection returned incomplete information. Refresh and inspect again.");
    }
  }
  return result as T;
}
