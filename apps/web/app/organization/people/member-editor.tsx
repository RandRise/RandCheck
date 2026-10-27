import { useEffect, useRef } from "react";
import { documentRoles, documentRoleDescriptions, documentRoleLabel, isDocumentRole } from "../../security/document-roles";
import type { PeopleController } from "./use-people";

export function MemberEditor({ people }: { people: PeopleController }) {
  const { selection, role, active, busy, recovery, error, confirm, close, save, reload, changeRole, changeActive } = people;
  const title = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    title.current?.focus({ preventScroll: true });
    if (window.innerWidth < 1000) title.current?.scrollIntoView?.({ block: "start" });
  }, []);
  if (!selection) return null;
  const member = selection === "new" ? null : selection;
  const grantingAdmin = member && member.role !== "Admin" && role === "Admin" && active;

  return <section className="security-panel member-editor people-editor" aria-labelledby="editor-title">
    <div className="security-panel-heading">
      <h3 id="editor-title" ref={title} tabIndex={-1}>{member ? "Edit access" : "Add a person"}</h3>
      <button className="security-text-button" onClick={close} disabled={busy || people.loading || recovery}>Close</button>
    </div>
    <form onSubmit={save}>
      {member ? <div className="people-editor-person"><strong dir="auto">{member.displayName}</strong>
        <details><summary>Account ID</summary><code className="member-object-id">{member.objectId}</code></details>
      </div> : <>
        <label>Display name<input name="displayName" disabled={busy || people.loading} required maxLength={200} autoComplete="off"/></label>
        <label>Microsoft Entra object ID<input name="objectId" disabled={busy || people.loading} required pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" placeholder="00000000-0000-0000-0000-000000000000"/></label>
        <p className="security-note">Ask your IT contact for this person&apos;s account ID in your company&apos;s Microsoft directory. This grants app access; it does not send an invitation.</p>
      </>}
      <label>Role<select disabled={busy || people.loading} value={role} required onChange={event => changeRole(event.target.value)}>
        <option value="">Choose a role</option>
        {documentRoles.map(value => <option key={value} value={value}>{documentRoleLabel(value)}</option>)}
      </select></label>
      <p className="security-note">{isDocumentRole(role) ? documentRoleDescriptions[role] : "Select Staff or Admin."}</p>
      {grantingAdmin && <p className="security-note">Saving gives {member.displayName} Admin access, including managing people and document deletion settings. This change is recorded in the audit history.</p>}
      {member && <label className="security-check"><input type="checkbox" disabled={busy || people.loading} checked={active} onChange={event => changeActive(event.target.checked)}/><span><strong>Active access</strong><small>Uncheck to remove access. Earlier work and history stay saved.</small></span></label>}
      {confirm && <p className="security-alert">Remove {member?.displayName}&apos;s access? Their next API request will be denied. Their earlier work stays in the record.</p>}
      {error && <p className="security-alert" role="alert">{error}</p>}
      {recovery && <p className="security-note">Check the current access before choosing a new change. If browser storage is blocked, keep this page open until you have checked.</p>}
      <div className="people-editor-actions">
        <button className="security-button" disabled={busy || people.loading || recovery}>{busy ? "Saving…" : confirm ? "Confirm removal" : selection === "new" ? "Grant access" : grantingAdmin ? "Grant Admin access" : "Save changes"}</button>
        {recovery && <button type="button" className="security-button secondary" disabled={people.loading} onClick={() => void reload()}>Reload members</button>}
      </div>
      {busy && <p className="security-note" role="status">Saving access. Please stay on this page.</p>}
    </form>
  </section>;
}
