"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import { API, apiFetch } from "../../security/api-fetch";
import { useEnterprise } from "../../security/enterprise-provider";
import "../../security/security.css";
type AuditEvent = { id: string; actorId: string | null; actorKind: string; action: string; targetType: string; targetId: string | null;
  outcome: string; reasonCode: string; requestId: string; beforeState: string | null; afterState: string | null; recordedAtUtc: string };
const label = (code: string) => code.replace(/[._-]/g, " ").replace(/([a-z])([A-Z])/g, "$1 $2");
export default function AuditPage() {
  const { session } = useEnterprise(); const [events, setEvents] = useState<AuditEvent[]>([]);
  const [offset, setOffset] = useState(0); const [revision, setRevision] = useState(0); const [loading, setLoading] = useState(true); const [error, setError] = useState("");
  useEffect(() => {
    if (!session.canManageMembers) return;
    const abort = new AbortController();
    void apiFetch(`${API}/api/organization/audit?offset=${offset}`, { signal: abort.signal }).then(async response => {
      if (!response.ok) throw new Error();
      const rows = await response.json(); if (!abort.signal.aborted) setEvents(rows);
    }).catch(() => { if (!abort.signal.aborted) setError("The audit trail could not be loaded. Try again."); })
      .finally(() => { if (!abort.signal.aborted) setLoading(false); });
    return () => abort.abort();
  }, [offset, revision, session.canManageMembers]);
  function changePage(next: number) { setLoading(true); setError(""); setEvents([]); setOffset(next); }
  if (!session.canManageMembers) return null;
  return <main className="organization-page"><nav className="security-tabs" aria-label="Organization"><Link href="/organization">Members & access</Link><Link href="/organization/audit" aria-current="page">Audit trail</Link><Link href="/organization/retention">Retention &amp; holds</Link></nav>
    <div className="security-heading"><div><span className="security-eyebrow">ORGANIZATION / ACCOUNTABILITY</span><h1>A clear record<br/><em>of important changes.</em></h1><p>Membership and workflow events for {session.organizationName}.</p></div><button className="security-button secondary" disabled={loading} onClick={() => { changePage(0); setRevision(v => v + 1); }}>Refresh trail</button></div>
    <div className="audit-intro"><span className="security-badge active">Append-only record</span><p>Newest events first. Open an event to inspect its actor, target, outcome and recorded state.</p></div>
    {error && <p className="security-alert" role="alert">{error}</p>}
    <section className="security-panel"><div className="security-panel-heading"><h2>Organization activity</h2><span>{loading ? "Loading…" : `${events.length} events on this page`}</span></div>
      {loading && <p className="security-note" role="status">Loading audit trail…</p>}
      {!loading && !error && !events.length && <div className="audit-empty"><h2>No events on this page.</h2><p>Recorded organization changes will appear here.</p></div>}
      {events.map(event => <details className="audit-event" key={event.id}><summary><span className={`audit-dot ${event.outcome.toLowerCase()}`}/><span className="audit-description"><strong>{label(event.action)}</strong><small>{event.actorKind} · {label(event.targetType)}</small></span><span className={`security-badge ${event.outcome.toLowerCase()}`}>{event.outcome}</span><time dateTime={event.recordedAtUtc}>{new Date(event.recordedAtUtc).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" })}</time><span aria-hidden="true">＋</span></summary>
        <div className="audit-detail"><dl>{[["Actor ID", event.actorId ?? "Not attributed"], ["Target ID", event.targetId ?? "Not recorded"], ["Request ID", event.requestId], ["Reason", event.reasonCode], ["Before", event.beforeState ?? "—"], ["After", event.afterState ?? "—"], ["Event ID", event.id], ["Recorded (UTC)", event.recordedAtUtc]].map(([name, value]) => <div key={name}><dt>{name}</dt><dd>{value}</dd></div>)}</dl></div>
      </details>)}
      <div className="audit-pagination"><button className="security-button secondary" disabled={loading || offset === 0} onClick={() => changePage(Math.max(0, offset - 100))}>← Newer</button><span>Page {offset / 100 + 1} · up to 100 events</span><button className="security-button secondary" disabled={loading || !!error || events.length < 100 || offset >= 10000} onClick={() => changePage(offset + 100)}>Older →</button></div>
    </section>
    <div className="security-footnote"><span>RECORD SCOPE</span><p>This trail covers supported membership and workflow events, including attributed denials. It is not a complete access log. Document contents and tokens are excluded.</p></div>
  </main>;
}
