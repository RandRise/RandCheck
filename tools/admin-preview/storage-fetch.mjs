// Browser-only storage simulation. Never forwards a storage write to a real API.
const key = "fictional-admin-storage-v1";
const base = "/api/organization/retention";
const documentId = "dddddddd-dddd-dddd-dddd-dddddddddddd";
const seed = () => ({ policy: { version: crypto.randomUUID(), documentDays: null, quarantineHours: null }, holds: [], events: [] });

export function createStoragePreviewFetch(fetcher, origin, storage) {
  let state;
  try { state = JSON.parse(storage.getItem(key)); } catch { /* First visit or blocked browser storage. */ }
  state ??= seed();
  const save = () => storage.setItem(key, JSON.stringify(state));
  const reply = (value, status = 200) => Response.json(value, { status });
  function record(action, targetId) {
    state.events.unshift({ id: crypto.randomUUID(), actorId: "sample-preview-admin", actorKind: "User", action,
      targetType: "Sample storage", targetId, outcome: "Succeeded", reasonCode: "Fictional preview only", requestId: crypto.randomUUID(),
      beforeState: null, afterState: null, recordedAtUtc: new Date().toISOString() });
    save();
  }
  return async (input, init) => {
    const url = new URL(input instanceof Request ? input.url : String(input), origin);
    if (url.origin !== origin) return fetcher(input, init);
    const path = url.pathname;
    const method = (init?.method ?? "GET").toUpperCase();
    if (path === "/api/organization/audit" && method === "GET") {
      const response = await fetcher(input, init);
      if (!response.ok) return response;
      return reply([...(await response.json()), ...state.events]);
    }
    if (!path.startsWith(base) && path !== "/api/organization/quarantine/cleanup") return fetcher(input, init);
    const offset = Number(url.searchParams.get("offset") ?? 0);
    if (!Number.isInteger(offset) || offset < 0 || offset > 10000) return reply({}, 400);
    const body = init?.body ? JSON.parse(String(init.body)) : {};
    if (path === base && method === "GET") return reply(state.policy);
    if (path === base && method === "PUT") {
      if (body.expectedVersion !== state.policy.version) return reply({}, 409);
      if (![body.documentDays, body.quarantineHours].every(v => v === null || Number.isInteger(v) && v > 0)) return reply({}, 400);
      state.policy = { version: crypto.randomUUID(), documentDays: body.documentDays, quarantineHours: body.quarantineHours };
      record("retention.policy-changed", "sample-company"); return reply(state.policy);
    }
    if (path === `${base}/holds` && method === "GET") return reply(state.holds.slice(offset, offset + 100));
    if (path === `${base}/holds` && method === "POST") {
      if (body.documentId !== null && body.documentId !== documentId) return reply({}, 404);
      if (!["AdministrativeReview", "Investigation", "LegalHold"].includes(body.reason)) return reply({}, 400);
      const hold = { id: crypto.randomUUID(), documentId: body.documentId, reason: body.reason, createdAtUtc: new Date().toISOString(), releasedAtUtc: null, version: crypto.randomUUID() };
      state.holds.unshift(hold); record("retention.hold-placed", hold.id); return reply(hold);
    }
    if (path.startsWith(`${base}/holds/`) && path.endsWith("/release") && method === "POST") {
      const hold = state.holds.find(row => row.id === path.split("/").at(-2));
      if (!hold) return reply({}, 404);
      if (hold.version !== body.expectedVersion || hold.releasedAtUtc) return reply({}, 409);
      hold.version = crypto.randomUUID(); hold.releasedAtUtc = new Date().toISOString();
      record("retention.hold-released", hold.id); return reply(hold);
    }
    if (path === `${base}/preview` && method === "GET") return reply({ documentDeletionEnabled: false, nextOffset: null, documents: offset ? [] : [
      { documentId, createdAtUtc: "2026-10-01T12:00:00Z", disposition: state.holds.some(h => !h.releasedAtUtc) ? "Held" : "Retained" },
    ] });
    if (path === "/api/organization/quarantine/cleanup" && method === "POST") {
      if (body.expectedPolicyVersion !== state.policy.version || !state.policy.quarantineHours) return reply({}, 409);
      const held = state.holds.some(hold => hold.documentId === null && !hold.releasedAtUtc);
      const eligible = state.cleaned || held ? 0 : 2;
      if (body.execute) { if (eligible) state.cleaned = true; record("retention.quarantine-cleaned", "sample-company"); }
      return reply({ executed: !!body.execute, eligible, deleted: body.execute ? eligible : 0, preserved: held && !state.cleaned ? 3 : 1, truncated: false });
    }
    return reply({}, 404);
  };
}
