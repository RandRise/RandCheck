import { documentRoleLabel } from "../../security/document-roles";
import type { PeopleController } from "./use-people";

export function PeopleList({ people }: { people: PeopleController }) {
  const { members, session, selection, busy, recovery, loading, loadError, edit, reload } = people;

  return <section className="security-panel people-list" aria-labelledby="people-list-title">
    <div className="security-panel-heading">
      <div><h3 id="people-list-title">People in this workspace</h3>
        <p>{loading ? "Loading people…" : loadError ? "List needs attention" : `${members.length} people shown`}</p></div>
      <button className="security-text-button" disabled={busy || loading} onClick={() => void reload()}>Refresh list</button>
    </div>
    {loadError && <p className="security-alert" role="alert">{loadError}</p>}
    {loading && <p className="security-note" role="status">Loading people…</p>}
    <ul className="people-rows" aria-label="People">
      {members.map(member => <li key={member.id} className={selection !== "new" && selection?.id === member.id ? "selected" : ""}>
        <div className="people-name"><strong dir="auto">{member.displayName}</strong>{member.actorId === session.actorId && <small>You</small>}</div>
        <span className="people-role">{documentRoleLabel(member.role)}</span>
        <span className={`security-badge ${member.isActive ? "active" : "revoked"}`}>{member.isActive ? "Active access" : "Access removed"}</span>
        <button className="security-text-button" disabled={busy || people.loading || recovery} aria-label={`Edit ${member.displayName}`} onClick={() => edit(member)}>Edit access</button>
      </li>)}
    </ul>
    {!loading && !loadError && !members.length && <p className="security-note">No people were returned.</p>}
    <p className="security-note people-list-limit">Up to 500 people are shown. People whose access was removed stay in the record.</p>
  </section>;
}
