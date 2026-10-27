import * as invoiceService from "./invoice-service";
import type { Role } from "../workflow.types";
import { roles } from "../workflow.constants";

export type PairSelection = Record<Role, string>;
export type Preparation = {
  selections: PairSelection;
  creationKey: string;
  draftKeys: Record<Role, string>;
  caseId?: string;
};

/** Existing APIs save creation and each draft separately. Resume only missing
 * links after a fresh read; never replace another person's saved selection.
 * A lost reply stops the sequence until the user explicitly retries. */
export async function prepareCheck(
  intent: Preparation,
  current: () => boolean,
  remember: (intent: Preparation) => void,
  allowDraftWrites: boolean,
) {
  if (!current()) return null;
  let receipt = intent.caseId
    ? await invoiceService.loadCase(intent.caseId)
    : await invoiceService.createCase("Invoice check", intent.creationKey);
  if (!current()) return null;
  intent = { ...intent, caseId: receipt.value.id };
  remember(intent);
  if (!allowDraftWrites)
    throw new Error("A selected file is unavailable or has a different type. Open the saved check to inspect its documents. No document links were changed.");

  for (const role of roles) {
    if (!current()) return null;
    const item = receipt.value;
    if (item.status !== "Draft" || item.runs.length || item.documents.length)
      throw new Error("This check already has reviewed work. Open the saved check to continue safely.");
    for (const saved of item.draftDocuments ?? []) {
      if (saved.documentId !== intent.selections[saved.role])
        throw new Error("The saved document choices changed. Open the saved check to inspect them.");
    }
    if (item.draftDocuments?.some(saved => saved.role === role && saved.documentId === intent.selections[role])) continue;
    receipt = await invoiceService.saveDraft(item.id, role, intent.selections[role], intent.draftKeys[role], {
      etag: receipt.etag,
      // The caller keeps this intent and stops; the next explicit attempt starts with a GET.
      onUncertain: () => {},
    });
  }
  if (!current()) return null;
  if (!roles.every(role => receipt.value.draftDocuments?.some(saved => saved.role === role && saved.documentId === intent.selections[role])))
    throw new Error("Both saved documents could not be verified. Retry preparation to check the saved progress.");
  return receipt.value;
}
