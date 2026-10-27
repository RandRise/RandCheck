import { isWorkspaceId } from "../../workspace-location";
import { roles } from "../workflow.constants";
import type { PairSelection, Preparation } from "./prepared-check";

export type StartDraft = { selections: PairSelection; intent?: Preparation };
export const emptyStartDraft = (): StartDraft => ({ selections: { Invoice: "", PurchaseOrder: "" } });
const key = (identity: string) => `invoice-pair-start:${identity}`;

function validPair(value: PairSelection, allowEmpty = false) {
  return value && roles.every(role => typeof value[role] === "string" && (allowEmpty && !value[role] || isWorkspaceId(value[role])));
}

/** Store only account-scoped IDs and retry keys, never document contents. */
export function readStartDraft(identity: string): StartDraft {
  try {
    const value = JSON.parse(sessionStorage.getItem(key(identity)) ?? "null") as StartDraft | null;
    if (!value || !validPair(value.selections, true)) return emptyStartDraft();
    const intent = value.intent;
    if (intent && (!validPair(intent.selections) || intent.selections.Invoice === intent.selections.PurchaseOrder ||
      !isWorkspaceId(intent.creationKey) || !validPair(intent.draftKeys) ||
      intent.caseId !== undefined && !isWorkspaceId(intent.caseId) ||
      roles.some(role => intent.selections[role] !== value.selections[role]))) return emptyStartDraft();
    return value;
  } catch { return emptyStartDraft(); }
}

export function saveStartDraft(identity: string, value: StartDraft | null) {
  try {
    if (value) sessionStorage.setItem(key(identity), JSON.stringify(value));
    else sessionStorage.removeItem(key(identity));
  } catch { /* In-memory recovery still works when session storage is unavailable. */ }
}

/** Adopt a pre-change interrupted Home request without changing its payload. */
export function creationKey(identity: string) {
  try {
    const old = JSON.parse(sessionStorage.getItem(`invoice-check-creation:${identity}`) ?? "null");
    if (old?.name === "Invoice check" && typeof old.key === "string" && isWorkspaceId(old.key)) return old.key as string;
  } catch { /* No usable older request. */ }
  return crypto.randomUUID();
}

export function clearRecoveredCreation(identity: string, requestKey: string) {
  try {
    const storageKey = `invoice-check-creation:${identity}`;
    if (JSON.parse(sessionStorage.getItem(storageKey) ?? "null")?.key === requestKey) sessionStorage.removeItem(storageKey);
  } catch { /* Never clear a different request. */ }
}
