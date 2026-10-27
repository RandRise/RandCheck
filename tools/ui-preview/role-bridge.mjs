// Preview-only bridge for an already-running, in-memory fixture server. Never
// imported by production. It neither rewrites memberships nor restarts the server.
export function createPreviewRoleFetch(original, origin, onLegacy = () => {}) {
  let legacy = true; // Block membership requests until a current session proves compatibility.
  return async (input, init) => {
    const url = new URL(input instanceof Request ? input.url : String(input), origin);
    if (url.origin !== origin) return original(input, init);
    if (legacy && /^\/api\/organization\/memberships(?:\/|$)/.test(url.pathname)) {
      return Response.json({ detail: "Existing preview data is preserved. User management requires an updated preview server." }, { status: 503 });
    }
    const response = await original(input, init);
    if (url.pathname !== "/api/session" || !response.ok) return response;
    const session = await response.clone().json();
    const current = ["Staff", "Admin"].includes(session.role) && !("canManageMembers" in session);
    legacy = !current;
    onLegacy(legacy);
    if (current) return response;
    // These two old Operator combinations already had exactly the same powers.
    // Viewer/Reviewer/None must never be promoted by a display adapter.
    if (session.role !== "Operator" || typeof session.canManageMembers !== "boolean" || session.isActive === false)
      return Response.json({ detail: "Select the Staff or Admin sample account." }, { status: 403 });
    const { canManageMembers, ...projected } = session;
    return Response.json({ ...projected, role: canManageMembers ? "Admin" : "Staff" }, {
      status: response.status, headers: { "Cache-Control": "no-store" },
    });
  };
}

export function installPreviewRoleBridge(target) {
  // HMR must not stack wrappers, retain old adapters, or replace the live server.
  const key = Symbol.for("uaedi.preview.originalFetch");
  target[key] ??= target.fetch.bind(target);
  target.fetch = createPreviewRoleFetch(target[key], target.location.origin, legacy => {
    const notice = target.document.getElementById("preview-role-compatibility");
    if (notice) notice.hidden = !legacy;
  });
}
