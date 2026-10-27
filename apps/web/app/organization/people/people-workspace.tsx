import { usePeople } from "./use-people";
import { PeopleList } from "./people-list";
import { MemberEditor } from "./member-editor";

export function PeopleWorkspace() {
  const people = usePeople();
  const { selection, busy, recovery, message, edit } = people;

  return <main className="organization-page people-page">
    <div className="security-heading">
      <div><h2>People &amp; access</h2><p>Choose who can work here and what they can do.</p></div>
      <button className="security-button" disabled={busy || people.loading || recovery} onClick={() => edit("new")}>Add person</button>
    </div>
    {message && <p className="security-success" role="status">{message}</p>}
    {recovery && !selection && <div className="security-alert" role="alert"><p>{people.error}</p>
      <button className="security-button secondary" disabled={people.loading} onClick={() => void people.reload()}>Reload members</button>
    </div>}
    <div className={`people-layout ${selection ? "editing" : ""}`}>
      <PeopleList people={people}/>
      {selection && <MemberEditor key={selection === "new" ? "new" : selection.id} people={people}/>}
    </div>
    <p className="people-guidance">Staff does the document work. Admin also manages people and system settings. Keep at least one active Admin.</p>
  </main>;
}
