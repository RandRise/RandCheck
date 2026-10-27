import { MemberSaveError } from "./member-api";

const uncertain = new Set<string>();
const pending = new Map<string, Promise<unknown>>();
const storageKey = (scope: string) => `member-save-recovery:${scope}`;

/** Only an unresolved flag is stored, never credentials or a replayable request. */
export function needsMemberRecovery(scope: string) {
  try { return uncertain.has(scope) || sessionStorage.getItem(storageKey(scope)) === "pending"; }
  catch { return uncertain.has(scope); }
}

export function clearMemberRecovery(scope: string) {
  uncertain.delete(scope);
  try { sessionStorage.removeItem(storageKey(scope)); } catch { /* In-memory fallback remains available. */ }
}

export function waitForMemberSave(scope: string) {
  return pending.get(scope)?.catch(() => undefined);
}

/** Track ownership across SPA navigation; a full reload still requires explicit inspection. */
export async function trackMemberSave<T>(scope: string, write: () => Promise<T>): Promise<T> {
  uncertain.add(scope);
  try { sessionStorage.setItem(storageKey(scope), "pending"); } catch { /* Never block the in-memory guard. */ }
  const request = write();
  pending.set(scope, request);
  try {
    const saved = await request;
    if (pending.get(scope) === request) clearMemberRecovery(scope);
    return saved;
  } catch (error) {
    if (pending.get(scope) === request && error instanceof MemberSaveError && !error.reloadRequired) clearMemberRecovery(scope);
    throw error;
  } finally {
    if (pending.get(scope) === request) pending.delete(scope);
  }
}
