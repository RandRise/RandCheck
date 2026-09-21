"use client";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { API, apiFetch } from "../../security/api-fetch";
import { useEnterprise } from "../../security/enterprise-provider";
import "../../security/security.css";
import "./retention.css";
import { DocumentDeletionControls } from "./document-deletion";

type Policy = { version: string; documentDays: number | null; quarantineHours: number | null };
type Hold = { id: string; documentId: string | null; reason: string; createdAtUtc: string; releasedAtUtc: string | null; version: string };
type Preview = { documentDeletionEnabled: boolean; documents: { documentId: string; createdAtUtc: string; disposition: string }[]; nextOffset: number | null };
type Cleanup = { executed: boolean; eligible: number; deleted: number; preserved: number; truncated: boolean };
const base = "/api/organization/retention";
class RetentionUiError extends Error {}
async function read<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await apiFetch(`${API}${path}`, init);
  if (!response.ok) throw new RetentionUiError(response.status === 409 ? "The policy or hold changed, or cleanup is disabled. Refresh before trying again." :
    response.status === 400 ? "Check the retention values and document ID." :
    "The request could not be completed. Refresh and check the audit trail before retrying cleanup.");
  return response.json();
}
const body = (method: string, value: unknown): RequestInit => ({ method, headers: { "Content-Type": "application/json" }, body: JSON.stringify(value) });

export default function RetentionPage() {
  const { session } = useEnterprise();
  const [policy, setPolicy] = useState<Policy | null>(null); const [holds, setHolds] = useState<Hold[]>([]);
  const [preview, setPreview] = useState<Preview | null>(null); const [offset, setOffset] = useState(0); const [holdOffset, setHoldOffset] = useState(0);
  const [days, setDays] = useState(""); const [hours, setHours] = useState(""); const [document, setDocument] = useState("");
  const [reason, setReason] = useState("AdministrativeReview"); const [release, setRelease] = useState<Hold | null>(null);
  const [cleanup, setCleanup] = useState<Cleanup | null>(null); const [cleanupVersion, setCleanupVersion] = useState("");
  const [confirmCleanup, setConfirmCleanup] = useState(false); const [busy, setBusy] = useState(true);
  const [error, setError] = useState(""); const [message, setMessage] = useState("");
  const load = useCallback(async (signal?: AbortSignal) => {
    const [p, h, v] = await Promise.all([read<Policy>(base, { signal }), read<Hold[]>(`${base}/holds?offset=${holdOffset}`, { signal }), read<Preview>(`${base}/preview?offset=${offset}`, { signal })]);
    if (signal?.aborted) return;
    setPolicy(p); setDays(p.documentDays?.toString() ?? ""); setHours(p.quarantineHours?.toString() ?? ""); setHolds(h); setPreview(v);
    setCleanup(null); setCleanupVersion(""); setConfirmCleanup(false); setRelease(null);
  }, [offset, holdOffset]);
  useEffect(() => {
    if (!session.canManageMembers) return;
    // Keep controls blocked during a refetch, including permission and page changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    const abort = new AbortController(); setBusy(true); setError("");
    void load(abort.signal).catch(() => { if (!abort.signal.aborted) { setPolicy(null); setError("Retention controls could not be loaded. Refresh to try again."); } })
      .finally(() => { if (!abort.signal.aborted) setBusy(false); });
    return () => abort.abort();
  }, [load, session.canManageMembers]);
  async function act(action: () => Promise<void>) {
    setBusy(true); setError(""); setMessage("");
    try { await action(); } catch (e) { setError(e instanceof RetentionUiError ? e.message : "Request could not be completed. Refresh and check the audit trail before retrying cleanup."); setCleanup(null); setConfirmCleanup(false); }
    finally { setBusy(false); }
  }
  if (!session.canManageMembers) return null;
  return <main className="organization-page"><nav className="security-tabs" aria-label="Organization"><Link href="/organization">Members & access</Link><Link href="/organization/audit">Audit trail</Link><Link href="/organization/retention" aria-current="page">Retention & holds</Link></nav>
    <div className="security-heading"><div><span className="security-eyebrow">ORGANIZATION / RETENTION</span><h1>Keep what matters.<br/><em>Control what expires.</em></h1><p>Retention settings and preservation holds for {session.organizationName}.</p></div><button className="security-button secondary" disabled={busy} onClick={() => act(() => load())}>Refresh controls</button></div>
    <div className="audit-intro"><span className="security-badge active">{preview?.documentDeletionEnabled ? "Document deletion requires inspection" : "Document deletion disabled"}</span><p>Retention expiry flags documents for review. Removal always requires a separate action.</p></div>
    {error && <p className="security-alert" role="alert">{error}</p>}{message && <p className="security-alert success" role="status">{message}</p>}
    {busy && !policy && <p role="status">Loading retention controls…</p>}
    {policy && <>
      <div className="retention-grid">
        <section className="security-panel"><div className="security-panel-heading"><h2>Retention policy</h2><span>Organization settings</span></div>
          <form className="retention-form" onSubmit={e => { e.preventDefault(); void act(async () => {
            await read(base, body("PUT", { expectedVersion: policy.version, documentDays: days === "" ? null : Number(days), quarantineHours: hours === "" ? null : Number(hours) }));
            await load(); setMessage("Retention policy saved and audited.");
          }); }}>
            <label>Document retention (days)<input type="number" min="1" max="36500" value={days} disabled={busy} onChange={e => setDays(e.target.value)} placeholder="Keep indefinitely"/></label>
            <p>Leave blank to retain indefinitely. A configured window flags documents for review; it does not automatically delete them.</p>
            <label>Abandoned quarantine uploads (hours)<input type="number" min="1" max="87600" value={hours} disabled={busy} onChange={e => setHours(e.target.value)} placeholder="Cleanup disabled"/></label>
            <p>Leave blank to disable cleanup. At least one hour is required. Cleanup runs only when an administrator requests it.</p>
            <button className="security-button" disabled={busy}>Save policy</button>
          </form>
        </section>
        <section className="security-panel"><div className="security-panel-heading"><h2>Place a hold</h2><span>Preservation takes priority</span></div>
          <form className="retention-form" onSubmit={e => { e.preventDefault(); void act(async () => {
            await read(`${base}/holds`, body("POST", { documentId: document.trim() || null, reason }));
            setDocument(""); await load(); setMessage("Hold placed and audited. It remains active until explicitly released.");
          }); }}>
            <label>Scope<select value={document ? "document" : "organization"} disabled={busy} onChange={e => setDocument(e.target.value === "document" ? " " : "")}><option value="organization">Entire organization</option><option value="document">One document</option></select></label>
            {!!document && <label>Document ID<input required pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" value={document.trim()} disabled={busy} onChange={e => setDocument(e.target.value || " ")}/></label>}
            <label>Reason<select value={reason} disabled={busy} onChange={e => setReason(e.target.value)}><option value="AdministrativeReview">Administrative review</option><option value="Investigation">Investigation</option><option value="LegalHold">Legal hold</option></select></label>
            <p>Organization holds also block abandoned-upload cleanup. Holds do not expire automatically; every hold and release is audited.</p>
            <button className="security-button" disabled={busy}>Place hold</button>
          </form>
        </section>
      </div>
      <section className="security-panel retention-section"><div className="security-panel-heading"><h2>Preservation holds</h2><span>Active and released</span></div>
        {!holds.length && <p className="retention-note">No holds on this page.</p>}
        {holds.map(h => <div className="retention-row" key={h.id}><div><strong>{h.documentId ? "Document hold" : "Organization hold"}</strong><small>{h.documentId ?? "All documents and quarantine uploads"}</small><small>{h.reason.replace(/([a-z])([A-Z])/g, "$1 $2")} · {new Date(h.createdAtUtc).toLocaleDateString()}</small></div><span className={`security-badge ${h.releasedAtUtc ? "" : "active"}`}>{h.releasedAtUtc ? "Released" : "Active"}</span>{!h.releasedAtUtc && <button className="security-button secondary" disabled={busy} onClick={() => setRelease(h)}>Release hold</button>}</div>)}
        {release && <div className="retention-confirm" role="group" aria-label="Confirm hold release"><p>Release this {release.documentId ? "document" : "organization"} hold? Other active holds will remain in force.</p><button className="security-button" disabled={busy} onClick={() => act(async () => { await read(`${base}/holds/${release.id}/release`, body("POST", { expectedVersion: release.version })); await load(); setMessage("Hold released and audited."); })}>Confirm release</button> <button className="security-button secondary" disabled={busy} onClick={() => setRelease(null)}>Cancel</button></div>}
        <div className="audit-pagination"><button className="security-button secondary" disabled={busy || !holdOffset} onClick={() => setHoldOffset(v => v - 100)}>Previous holds</button><span>Page {holdOffset / 100 + 1}</span><button className="security-button secondary" disabled={busy || holds.length < 100 || holdOffset >= 10000} onClick={() => setHoldOffset(v => v + 100)}>Next holds</button></div>
      </section>
      <section className="security-panel retention-section"><div className="security-panel-heading"><h2>Document retention preview</h2><span>Files remain available</span></div>
        {preview?.documents.map(d => <div className="retention-row" key={d.documentId}><div><strong>Document</strong><small>{d.documentId}</small><small>Uploaded {new Date(d.createdAtUtc).toLocaleDateString()}</small></div><span className={`security-badge ${d.disposition === "Held" ? "active" : ""}`}>{d.disposition === "PastRetentionWindow" ? "Past retention window" : d.disposition}</span></div>)}
        {!preview?.documents.length && <p className="retention-note">No documents on this page.</p>}
        <div className="audit-pagination"><button className="security-button secondary" disabled={busy || !offset} onClick={() => setOffset(v => v - 100)}>Previous documents</button><span>Page {offset / 100 + 1}</span><button className="security-button secondary" disabled={busy || preview?.nextOffset == null || offset >= 10000} onClick={() => setOffset(preview!.nextOffset!)}>Next documents</button></div>
      </section>
      <section className="security-panel retention-section"><div className="security-panel-heading"><h2>Quarantine cleanup</h2><span>{policy.quarantineHours == null ? "Disabled" : `After ${policy.quarantineHours} hours`}</span></div><div className="retention-form">
        <p>Preview abandoned uploads before removal. Active uploads, held items and files associated with stored documents are preserved. Eligibility is checked again when cleanup runs.</p>
        <button className="security-button secondary" disabled={busy || policy.quarantineHours == null} onClick={() => act(async () => { const result = await read<Cleanup>("/api/organization/quarantine/cleanup", body("POST", { expectedPolicyVersion: policy.version, execute: false })); setCleanup(result); setCleanupVersion(policy.version); setConfirmCleanup(false); })}>Preview cleanup</button>
        {cleanup && <div role="status"><p><strong>{cleanup.eligible} eligible · {cleanup.preserved} preserved · {cleanup.deleted} removed</strong></p>{cleanup.truncated && <p>This batch is limited. Additional uploads may remain.</p>}</div>}
        {cleanup && !cleanup.executed && cleanup.eligible > 0 && <><label className="security-check"><input type="checkbox" checked={confirmCleanup} disabled={busy} onChange={e => setConfirmCleanup(e.target.checked)}/><span>Remove eligible abandoned uploads. This cannot be undone.</span></label><button className="security-button" disabled={busy || !confirmCleanup} onClick={() => act(async () => { const result = await read<Cleanup>("/api/organization/quarantine/cleanup", body("POST", { expectedPolicyVersion: cleanupVersion, execute: true })); setCleanup(result); setConfirmCleanup(false); setMessage("Quarantine cleanup completed. Review the audit trail for individual actions."); })}>Run quarantine cleanup</button></>}
      </div></section>
      <DocumentDeletionControls enabled={preview?.documentDeletionEnabled ?? false} onRemoved={id => setPreview(current => current && ({ ...current, documents: current.documents.filter(d => d.documentId !== id) }))}/>
      <div className="security-footnote"><span>POLICY SCOPE</span><p>Deleted content must stay excluded after restore. Backup expiry and recovery require the independent deletion journal, current retention controls and deployment policy.</p><Link href="/organization/audit">View audit trail →</Link></div>
    </>}
  </main>;
}
