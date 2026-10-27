import { API, apiFetch } from "../../security/api-fetch";
import { isWorkspaceId } from "../../workspace-location";
import { invoiceRuleSetVersion, labels } from "../workflow.constants";
import type { Case, CasePage, Document, ReviewChoice, Role } from "../workflow.types";
import { read, readCase } from "./invoice-response";

type ReviewResponse = {
  documentId: string;
  session?: { id: string; status: string; revisionNumber?: number };
};

type MutationOptions = {
  etag: string | null;
  onUncertain: () => void;
};

export async function loadDocuments() {
  return read<Document[]>(await apiFetch(`${API}/api/documents/work-list`));
}

export async function loadCases(offset: number) {
  return read<CasePage>(
    await apiFetch(`${API}/api/reconciliation-cases?workflowKind=InvoicePurchaseOrder&offset=${offset}`),
  );
}

export async function loadCase(id: string) {
  return readCase(await apiFetch(`${API}/api/reconciliation-cases/${id}`), id);
}

export async function createCase(name: string, requestKey: string) {
  return readCase(await apiFetch(`${API}/api/reconciliation-cases`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name, requestKey, workflowKind: "InvoicePurchaseOrder" }),
  }));
}

export async function loadReview(documentId: string) {
  return read<ReviewResponse>(await apiFetch(`${API}/api/documents/${documentId}/review`));
}

export function requireCompletedReview(value: ReviewResponse, documentId: string, role: Role) {
  if (value.session?.status !== "Completed") {
    throw new Error(`Complete the ${labels[role].toLowerCase()} review first. Unchecked values stay unchecked.`);
  }

  const review = value.session;

  if (value.documentId !== documentId || typeof review.id !== "string" || !isWorkspaceId(review.id)) {
    throw new Error("The completed reviewed revision could not be verified. Reopen the document review.");
  }

  return review;
}

// An uncertain response requires a read-only reload. Never replay writes here.
async function writeCase(
  caseId: string,
  path: string,
  body: object,
  { etag, onUncertain }: MutationOptions,
  method = "PUT",
) {
  let response: Response;

  try {
    response = await apiFetch(`${API}/api/reconciliation-cases/${caseId}/${path}`, {
      method,
      headers: { "Content-Type": "application/json", "If-Match": etag ?? "" },
      body: JSON.stringify(body),
    });
  } catch {
    onUncertain();
    throw new Error("The save could not be confirmed. Reload saved check before trying again.");
  }

  if (response.status === 412 || response.status >= 500) {
    onUncertain();
    throw new Error("The saved check may have changed. Reload saved check before trying again.");
  }

  if (!response.ok) return read<Case>(response);

  try {
    return await readCase(response, caseId);
  } catch {
    onUncertain();
    throw new Error("The saved response could not be read. Reload saved check before trying again.");
  }
}

export function saveDraft(
  caseId: string,
  role: Role,
  documentId: string | null,
  requestKey: string,
  options: MutationOptions,
) {
  return writeCase(caseId, `draft-documents/${role}`, { documentId, requestKey }, options);
}

export function pinReview(caseId: string, role: Role, choice: ReviewChoice, options: MutationOptions) {
  return writeCase(caseId, `documents/${role}`, {
    documentId: choice.documentId,
    reviewSessionId: choice.reviewSessionId,
  }, options);
}

export function compare(caseId: string, orderScope: string, options: MutationOptions, factual = false) {
  return writeCase(caseId, "evaluations", {
    ruleSetVersion: factual ? "invoice-po-2.0" : invoiceRuleSetVersion,
    ...(factual ? {} : { orderScope }),
  }, options, "POST");
}
