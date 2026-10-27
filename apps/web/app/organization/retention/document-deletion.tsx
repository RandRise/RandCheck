"use client";

import { activityTime } from "../audit/activity-labels";
import { deletionReasons as reasons } from "./deletion-api";
import { useDocumentDeletion, type DeletionProps } from "./use-document-deletion";

export function DocumentDeletionControls(props: DeletionProps) {
  const { enabled, locked = false } = props;
  const controls = useDocumentDeletion(props);
  const { id, plan, resume, target, records, offset, confirmed, busy, loading,
    needsRefresh, historyError, error, message, setConfirmed } = controls;
  return <section className="security-panel retention-section"><div className="security-panel-heading"><h3>Remove a stored document</h3><span>{enabled ? "Inspect first, then confirm" : "Removal is disabled"}</span></div>
    <div className="retention-form"><p>Deletion removes an expired document’s original file and unreferenced extraction content. Active processing, holds, reviewed or cited history, and accounting records block removal. Audit and released-hold history remain.</p>
      {!enabled && <p>Deployment configuration and an independent deletion journal are required before removal is available.</p>}
      {enabled && <>
        <form className="deletion-inspect" onSubmit={event => { event.preventDefault(); void controls.inspect(); }}>
          <label>Document ID to inspect<input required pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" value={id} disabled={busy || locked || needsRefresh} onChange={event => controls.changeTarget(event.target.value)}/></label>
          <button className="security-button secondary" disabled={busy || locked || needsRefresh || loading}>Inspect deletion</button>
        </form>
        {plan && <div className="deletion-plan"><strong>{plan.blockers.length ? "Deletion blocked" : "Eligible for deletion"}</strong><p>{plan.extractionRuns} extraction runs · {plan.extractionJobs} processing jobs</p>
          {!!plan.blockers.length && <ul>{plan.blockers.map(code => <li key={code}>{reasons[code] ?? "A preservation rule prevents deletion."}</li>)}</ul>}
        </div>}
        {resume && <p><strong>Resume file removal</strong><br/>{resume.documentId}<br/>Database content was already removed. This retries verification and removal of the original file.</p>}
        {target && (resume || plan?.enabled && !plan.blockers.length) && <><label className="security-check"><input type="checkbox" checked={confirmed} disabled={busy || locked || needsRefresh} onChange={e => setConfirmed(e.target.checked)}/><span>I understand this permanently removes the selected document content.</span></label>
          <button className="security-button" disabled={busy || locked || needsRefresh || !confirmed || loading} onClick={() => void controls.execute()}>{resume ? "Resume file removal" : "Delete expired document"}</button></>}
        {error && <p className="security-alert" role="alert">{error}</p>}{message && <p role="status">{message}</p>}
      </>}
    </div>
    {enabled && <><div className="security-panel-heading"><h4>Removal history</h4><button className="security-button secondary" disabled={busy || locked || loading} onClick={controls.refreshHistory}>Refresh deletion history</button></div>
      {loading && <p className="retention-note" role="status">Loading deletion history…</p>}
      {!loading && !historyError && !records.length && <p className="retention-note">No deletion records on this page.</p>}
      {records.map(r => <div className="retention-row" key={r.documentId}><div><strong>{r.state === "Completed" ? "Completed" : "File removal pending"}</strong><small>{r.documentId}</small><small>Requested {activityTime(r.requestedAtUtc) ?? "Time unavailable"}</small></div>{r.state === "PendingFileRemoval" && <button className="security-button secondary" disabled={busy || locked || needsRefresh || loading} onClick={() => controls.reviewPending(r)}>Review pending removal</button>}</div>)}
      <div className="audit-pagination"><button className="security-button secondary" disabled={busy || locked || loading || needsRefresh || !offset} onClick={() => controls.changePage(offset - 100)}>Previous deletions</button><span>Page {offset / 100 + 1}</span><button className="security-button secondary" disabled={busy || locked || loading || needsRefresh || historyError || records.length < 100 || offset >= 10000} onClick={() => controls.changePage(offset + 100)}>Next deletions</button></div>
    </>}
  </section>;
}
