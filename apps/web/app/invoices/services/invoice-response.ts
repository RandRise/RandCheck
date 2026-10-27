import { isWorkspaceId } from "../../workspace-location";
import { roles } from "../workflow.constants";
import type { Case, Draft } from "../workflow.types";

export async function read<T>(response: Response) {
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new Error(problem?.detail ?? `Request failed (${response.status}).`);
  }

  return { value: await response.json() as T, etag: response.headers.get("ETag") };
}

function validSource(source: Draft) {
  return source && roles.includes(source.role) &&
    typeof source.documentId === "string" && isWorkspaceId(source.documentId);
}

export async function readCase(response: Response, expectedId?: string) {
  const result = await read<Case>(response);
  const value = result.value;

  if (value?.workflowKind === "Legacy") {
    throw new Error("This is an existing comparison case. Open it from Compare documents.");
  }

  if (
    !value || typeof value.id !== "string" || !isWorkspaceId(value.id) ||
    value.workflowKind !== "InvoicePurchaseOrder" ||
    (expectedId && value.id.toLowerCase() !== expectedId.toLowerCase()) ||
    typeof value.name !== "string" || typeof value.reference !== "string" ||
    !["Draft", "Evaluated", "Closed"].includes(value.status) ||
    !Array.isArray(value.draftDocuments) || !value.draftDocuments.every(validSource) ||
    !Array.isArray(value.documents) || !value.documents.every(source =>
      validSource(source) && typeof source.reviewSessionId === "string" &&
      isWorkspaceId(source.reviewSessionId) &&
      (!source.extractionRunId || isWorkspaceId(source.extractionRunId))
    ) ||
    !Array.isArray(value.runs) || !value.runs.every(run =>
      run && typeof run.id === "string" && isWorkspaceId(run.id) &&
      Number.isInteger(run.runNumber) && typeof run.ruleSetVersion === "string"
    )
  ) {
    throw new Error("The invoice check response could not be verified.");
  }

  if (!result.etag || !/^"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"$/i.test(result.etag)) {
    throw new Error("The saved invoice check version could not be verified.");
  }

  return result;
}
