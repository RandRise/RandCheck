"use client";
import Link from "next/link";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { API, apiFetch } from "../security/api-fetch";
import { useEnterprise } from "../security/enterprise-provider";
import "../security/security.css";

export type Member = { id: string; actorId: string; tenantId: string; objectId: string; displayName: string;
  role: string; canManageMembers: boolean; isActive: boolean; version: string };
const descriptions: Record<string, string> = {
  None: "No document access. Administration can be granted separately.",
  Viewer: "View documents and evidence, and ask cited questions.",
  Reviewer: "Viewer access, plus document reviews and reconciliation.",
  Operator: "Reviewer access, plus uploads, reprocessing and cancellation."
};
export default function OrganizationPage() {
  const { session, refresh } = useEnterprise();
  const [members, setMembers] = useState<Member[]>([]); const [loading, setLoading] = useState(true);
  const [selection, setSelection] = useState<Member | "new" | null>(null);
  const [role, setRole] = useState("Viewer"); const [manage, setManage] = useState(false); const [active, setActive] = useState(true);
  const [busy, setBusy] = useState(false); const [message, setMessage] = useState(""); const [error, setError] = useState("");
  const [conflict, setConflict] = useState(false); const [confirm, setConfirm] = useState(false);
  const load = useCallback(() => apiFetch(`${API}/api/organization/memberships`)
    .then(response => { if (!response.ok) throw new Error(); return response.json(); })
    .then(setMembers)
    .catch(() => setError("Members could not be loaded. Try refreshing the list."))
    .finally(() => setLoading(false)), []);
  useEffect(() => { if (session.canManageMembers) void load(); }, [load, session.canManageMembers]);
  function edit(member: Member | "new") {
    setSelection(member); setRole(member === "new" ? "Viewer" : member.role);
    setManage(member === "new" ? false : member.canManageMembers); setActive(member === "new" ? true : member.isActive);
    setError(""); setMessage(""); setConflict(false); setConfirm(false);
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selection || conflict) return;
    if (selection !== "new" && selection.isActive && !active && !confirm) { setConfirm(true); return; }
    const form = new FormData(event.currentTarget); setBusy(true); setError("");
    const body = selection === "new" ? { tenantId: session.tenantId, objectId: form.get("objectId"), displayName: form.get("displayName"), role, canManageMembers: manage }
      : { expectedVersion: selection.version, role, canManageMembers: manage, isActive: active };
    try {
      const response = await apiFetch(`${API}/api/organization/memberships${selection === "new" ? "" : `/${selection.id}`}`, {
        method: selection === "new" ? "POST" : "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body)
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        if (problem.code === "membership_version_conflict") { setConflict(true); throw new Error("This member was changed by another administrator. Reload the member before making a new change."); }
        const messages: Record<string, string> = { last_administrator_required: "Keep at least one active administrator. Add another administrator before removing this access.",
          membership_already_exists: "This identity already has a membership. Edit its existing membership, or ask the provisioning operator to check its organization.",
          identity_has_active_organization: "This identity already has access to another organization.", membership_unchanged: "There are no changes to save." };
        throw new Error(messages[problem.code] ?? "Access could not be saved. Check the details and try again.");
      }
      const saved = await response.json() as Member;
      setSelection(null); setMessage(`${saved.displayName}: ${saved.isActive ? "access saved" : "access revoked"}. The change is recorded in the audit trail.`);
      await load(); if (saved.actorId === session.actorId) await refresh();
    } catch (e) { setError(e instanceof Error ? e.message : "Access could not be saved."); }
    finally { setBusy(false); }
  }
  if (!session.canManageMembers) return null;
  const selectedMember = selection && selection !== "new" ? selection : null;
  return <main className="organization-page">
    <nav className="security-tabs" aria-label="Organization"><Link href="/organization" aria-current="page">Members & access</Link><Link href="/organization/audit">Audit trail</Link><Link href="/organization/retention">Retention &amp; holds</Link></nav>
    <div className="security-heading"><div><span className="security-eyebrow">ORGANIZATION / ACCESS CONTROL</span><h1>The right people.<br/><em>The right access.</em></h1><p>Manage who can work in {session.organizationName}.</p></div><button className="security-button" onClick={() => edit("new")}>+ Add member</button></div>
    <div className="security-overview"><div><strong>{members.filter(m => m.isActive).length}</strong><span>Active members</span></div><div><strong>{members.filter(m => m.isActive && m.canManageMembers).length}</strong><span>Administrators</span></div><p>One organization per identity.<br/>Changes apply on the next API request.</p></div>
    {message && <p className="security-success" role="status">{message}</p>}
    <div className={`members-layout ${selection ? "editing" : ""}`}><section className="security-panel"><div className="security-panel-heading"><h2>Organization members</h2><button className="security-text-button" onClick={() => { setLoading(true); setError(""); void load(); }} disabled={loading || busy}>Refresh list</button></div>
      <p className="security-table-hint">Scroll the table sideways to see status and edit access.</p><div className="security-table-scroll"><table className="security-table"><thead><tr><th>Member</th><th>Document role</th><th>Administration</th><th>Status</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>
        {members.map(member => <tr key={member.id}><td><strong>{member.displayName}{member.actorId === session.actorId && <small> (you)</small>}</strong><code title={member.objectId}>{member.objectId.slice(0, 8)}…</code></td><td>{member.role === "None" ? "No document access" : member.role}</td><td>{member.canManageMembers ? "Administrator" : "—"}</td><td><span className={`security-badge ${member.isActive ? "active" : "revoked"}`}>{member.isActive ? "Active" : "Revoked"}</span></td><td><button className="security-text-button" aria-label={`Edit ${member.displayName}`} onClick={() => edit(member)}>Edit access ↗</button></td></tr>)}
      </tbody></table></div>{loading && <p className="security-note" role="status">Loading members…</p>}{!loading && !members.length && <p className="security-note">No memberships were returned.</p>}
      <p className="security-note">Showing up to 500 memberships. Revoked members remain in the record.</p>
    </section>
    {selection && <section className="security-panel member-editor" aria-labelledby="editor-title"><div className="security-panel-heading"><h2 id="editor-title">{selection === "new" ? "Add a member" : "Edit access"}</h2><button className="security-text-button" onClick={() => setSelection(null)} disabled={busy}>Close</button></div>
      <form onSubmit={save} key={selection === "new" ? "new" : selection.id}>
        {selection === "new" ? <><label>Display name<input name="displayName" required maxLength={200} autoComplete="off"/></label><label>Microsoft Entra object ID<input name="objectId" required pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" placeholder="00000000-0000-0000-0000-000000000000"/></label><p className="security-note">Use the object ID from this organization’s Entra tenant. This grants app access; it does not send an invitation.</p></> : <><strong>{selectedMember!.displayName}</strong><code className="member-object-id">{selectedMember!.objectId}</code></>}
        <label>Document role<select value={role} onChange={e => { setRole(e.target.value); setConfirm(false); }}>{Object.keys(descriptions).map(r => <option key={r} value={r}>{r === "None" ? "None — no document access" : r}</option>)}</select></label><p className="security-note">{descriptions[role]}</p>
        <label className="security-check"><input type="checkbox" checked={manage} onChange={e => { setManage(e.target.checked); setConfirm(false); }}/><span><strong>Organization administrator</strong><small>Manage memberships, retention and holds, and read the audit trail.</small></span></label>
        {selection !== "new" && <label className="security-check"><input type="checkbox" checked={active} onChange={e => { setActive(e.target.checked); setConfirm(false); }}/><span><strong>Active membership</strong><small>Uncheck to revoke access while preserving history.</small></span></label>}
        {confirm && <p className="security-alert">Revoke {selectedMember?.displayName}’s access? Their next API request will be denied. Their earlier work remains in the record.</p>}
        <button className="security-button" disabled={busy || conflict}>{busy ? "Saving…" : confirm ? "Confirm revocation" : selection === "new" ? "Grant access" : "Save changes"}</button>
        {conflict && <button type="button" className="security-button secondary" onClick={async () => { setSelection(null); setError(""); await load(); }}>Reload members</button>}
      </form>
    </section>}</div>
    {error && <p className="security-alert" role="alert">{error}</p>}
    <div className="security-footnote"><span>IDENTITY & ACCOUNTABILITY</span><p>Document permissions and administration are separate. Keep at least one active administrator.</p><Link href="/organization/audit">View the audit trail →</Link></div>
  </main>;
}
