"use client";
import { useEffect, useState } from "react";
import { API, apiFetch } from "../../security/api-fetch";

type Plan = { documentId: string; documentVersion: string; policyVersion: string; enabled: boolean; blockers: string[]; extractionRuns: number; extractionJobs: number };
type Deletion = { documentId: string; documentVersion: string; policyVersion: string; state: string; requestedAtUtc: string; completedAtUtc: string | null };
const path = (id: string) => `/api/organization/retention/documents/${encodeURIComponent(id)}/deletion`;
const reasons: Record<string, string> = { deployment_disabled: "Deletion is disabled for this deployment.", retention_not_configured: "No document retention window is configured.", retention_window_active: "The document is still within its retention window.", retention_hold_active: "An active hold prevents removal.", processing_active: "Processing must finish or be cancelled first.", historical_dependencies: "Review, reconciliation or cited evidence depends on this document.", accounting_dependencies: "Extraction accounting or a non-local provider record must be retained." };
class DeletionRequestError extends Error {}
async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await apiFetch(`${API}${url}`, options);
  if (!response.ok) throw new DeletionRequestError(response.status === 409 ? "The document, policy or hold state changed. Inspect it again before continuing." :
    response.status === 404 ? "The document is unavailable in this organization. Refresh deletion history if a previous request may have completed." :
    "The request could not be completed. Refresh deletion history and check the audit trail before retrying.");
  return response.json();
}
export function DocumentDeletionControls({ enabled, onRemoved }: { enabled: boolean; onRemoved?: (documentId: string) => void }) {
  const [id, setId] = useState(""); const [plan, setPlan] = useState<Plan | null>(null); const [resume, setResume] = useState<Deletion | null>(null);
  const [records, setRecords] = useState<Deletion[]>([]); const [offset, setOffset] = useState(0); const [revision, setRevision] = useState(0);
  const [confirmed, setConfirmed] = useState(false); const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(false);
  const [error, setError] = useState(""); const [message, setMessage] = useState("");
  useEffect(() => {
    if (!enabled) return;
    // Clear stale deletion records and block controls before the replacement request.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    const abort = new AbortController(); setLoading(true); setError(""); setRecords([]);
    void request<Deletion[]>(`/api/organization/retention/deletions?offset=${offset}`, { signal: abort.signal })
      .then(rows => { if (!abort.signal.aborted) setRecords(rows); })
      .catch(() => { if (!abort.signal.aborted) setError("Deletion history could not be loaded. Refresh before retrying a deletion."); })
      .finally(() => { if (!abort.signal.aborted) setLoading(false); });
    return () => abort.abort();
  }, [enabled, offset, revision]);
  async function act(action: () => Promise<void>) {
    setBusy(true); setError(""); setMessage("");
    try { await action(); } catch (e) { setError(e instanceof DeletionRequestError ? e.message : "Deletion could not be completed. Refresh history and review the audit trail."); }
    finally { setBusy(false); setConfirmed(false); }
  }
  const target = resume ?? plan;
  return <section className="security-panel retention-section"><div className="security-panel-heading"><h2>Stored-document deletion</h2><span>{enabled ? "Explicit, verified removal" : "Deployment disabled"}</span></div>
    <div className="retention-form"><p>Deletion removes an expired document’s original file and unreferenced extraction content. Active processing, holds, reviewed or cited history, and accounting records block removal. Audit and released-hold history remain.</p>
      {!enabled && <p>Deployment configuration and an independent deletion journal are required before removal is available.</p>}
      {enabled && <>
        <form className="deletion-inspect" onSubmit={e => { e.preventDefault(); void act(async () => { setPlan(null); setResume(null); setPlan(await request<Plan>(path(id))); }); }}>
          <label>Document ID to inspect<input required pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" value={id} disabled={busy} onChange={e => { setId(e.target.value); setPlan(null); setResume(null); setConfirmed(false); }}/></label>
          <button className="security-button secondary" disabled={busy || loading}>Inspect deletion</button>
        </form>
        {plan && <div className="deletion-plan"><strong>{plan.blockers.length ? "Deletion blocked" : "Eligible for deletion"}</strong><p>{plan.extractionRuns} extraction runs · {plan.extractionJobs} processing jobs</p>
          {!!plan.blockers.length && <ul>{plan.blockers.map(code => <li key={code}>{reasons[code] ?? "A preservation rule prevents deletion."}</li>)}</ul>}
        </div>}
        {resume && <p><strong>Resume file removal</strong><br/>{resume.documentId}<br/>Database content was already removed. This retries verification and removal of the original file.</p>}
        {target && (resume || plan?.enabled && !plan.blockers.length) && <><label className="security-check"><input type="checkbox" checked={confirmed} disabled={busy} onChange={e => setConfirmed(e.target.checked)}/><span>I understand this permanently removes the selected document content.</span></label>
          <button className="security-button" disabled={busy || !confirmed || loading} onClick={() => act(async () => {
            const result = await request<Deletion>(path(target.documentId), { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ expectedDocumentVersion: target.documentVersion, expectedPolicyVersion: target.policyVersion }) });
            setMessage(result.state === "Completed" ? "Document deletion completed and audited." : "Database content removed. File removal is pending.");
            onRemoved?.(result.documentId);
            setPlan(null); setResume(null); setRevision(v => v + 1);
          })}>{resume ? "Resume file removal" : "Delete expired document"}</button></>}
        {error && <p className="security-alert" role="alert">{error}</p>}{message && <p role="status">{message}</p>}
      </>}
    </div>
    {enabled && <><div className="security-panel-heading"><h2>Deletion history</h2><button className="security-button secondary" disabled={busy || loading} onClick={() => { setPlan(null); setResume(null); setConfirmed(false); setRevision(v => v + 1); }}>Refresh deletion history</button></div>
      {loading && <p className="retention-note" role="status">Loading deletion history…</p>}
      {!loading && !records.length && <p className="retention-note">No deletion records on this page.</p>}
      {records.map(r => <div className="retention-row" key={r.documentId}><div><strong>{r.state === "Completed" ? "Completed" : "File removal pending"}</strong><small>{r.documentId}</small><small>Requested {new Date(r.requestedAtUtc).toLocaleString()}</small></div>{r.state === "PendingFileRemoval" && <button className="security-button secondary" disabled={busy || loading} onClick={() => { setPlan(null); setResume(r); setConfirmed(false); setError(""); setMessage(""); }}>Review pending removal</button>}</div>)}
      <div className="audit-pagination"><button className="security-button secondary" disabled={busy || loading || !offset} onClick={() => setOffset(v => v - 100)}>Previous deletions</button><span>Page {offset / 100 + 1}</span><button className="security-button secondary" disabled={busy || loading || records.length < 100 || offset >= 10000} onClick={() => setOffset(v => v + 100)}>Next deletions</button></div>
    </>}
  </section>;
}
