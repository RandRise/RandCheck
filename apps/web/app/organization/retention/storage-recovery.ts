const unresolved = new Set<string>();
const pending = new Map<string, Promise<unknown>>();
const key = (scope: string) => `storage-recovery:${scope}`;

/** Persist only an unresolved flag. Never store or replay a destructive request. */
export function needsStorageRecovery(scope: string) {
  try { return unresolved.has(scope) || sessionStorage.getItem(key(scope)) === "pending"; }
  catch { return unresolved.has(scope); }
}

export function clearStorageRecovery(scope: string) {
  unresolved.delete(scope);
  try { sessionStorage.removeItem(key(scope)); } catch { /* In-memory fallback. */ }
}

export function waitForStorageWrite(scope: string) {
  return pending.get(scope)?.catch(() => undefined);
}

export async function trackStorageWrite<T>(scope: string, write: () => Promise<T>): Promise<T> {
  unresolved.add(scope);
  try { sessionStorage.setItem(key(scope), "pending"); } catch { /* The in-memory guard remains active. */ }
  const request = write();
  pending.set(scope, request);
  try {
    const result = await request;
    if (pending.get(scope) === request) clearStorageRecovery(scope);
    return result;
  } finally {
    if (pending.get(scope) === request) pending.delete(scope);
  }
}
