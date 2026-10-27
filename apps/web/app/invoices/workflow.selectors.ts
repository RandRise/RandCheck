import type { Case } from "./workflow.types";
import { roles } from "./workflow.constants";

export function savedSelections(item: Case) {
  return Object.fromEntries(
    (item.draftDocuments ?? []).map(source => [source.role, source.documentId]),
  );
}

export function isPairReady(item: Case | null) {
  return !!item && roles.every(role => {
    const draft = item.draftDocuments?.find(source => source.role === role);
    return draft && item.documents.some(pin =>
      pin.role === role && pin.documentId === draft.documentId,
    );
  });
}

export function hasUnsavedSelections(item: Case | null, selections: Record<string, string>) {
  return !!item && roles.some(role =>
    (selections[role] || "") !== (item.draftDocuments?.find(source => source.role === role)?.documentId ?? ""),
  );
}
