"use client";

import Link from "next/link";
import { API, apiFetch } from "../security/api-fetch";
import { FormEvent, useEffect, useState } from "react";

type DocumentSummary = { id: string; originalFilename: string; status: string; documentType?: string };
type ReviewWorkspace = { session?: { id: string; status: string } };
type CaseLink = { id: string; role: string; documentId: string; reviewSessionId: string };
type Resolution = { id: string; sequenceNumber: number; action: string; reason: string; createdAt: string };
type Finding = {
  id: string; ruleCode: string; severity: string; leftRole?: string; rightRole?: string;
  fieldKey?: string; occurrence?: number; leftFieldSnapshotId?: string; rightFieldSnapshotId?: string;
  leftValue?: string; rightValue?: string; difference?: number; tolerance?: number;
  explanation: string; resolutions: Resolution[];
};
type CaseRun = {
  id: string; runNumber: number; status: string; ruleSetVersion: string;
  fields: { id: string; key: string; occurrence: number; value: string; page?: number; quote?: string }[];
  findings: Finding[];
};
type ReconciliationCase = {
  id: string; name: string; status: string; concurrencyToken: string;
  documents: CaseLink[]; runs: CaseRun[];
};


const roles = ["Quotation", "PurchaseOrder", "Invoice"] as const;
const roleLabels: Record<string, string> = {
  Quotation: "Quotation", PurchaseOrder: "Purchase order", Invoice: "Invoice",
};

async function readResponse<T>(response: Response): Promise<{ value: T; etag: string | null }> {
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string } | null;
    throw new Error(problem?.detail ?? `Request failed (${response.status})`);
  }
  return { value: await response.json() as T, etag: response.headers.get("ETag") };
}

export default function ReconciliationPage() {
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [item, setItem] = useState<ReconciliationCase | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [selected, setSelected] = useState<Record<string, string>>({});
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [reasons, setReasons] = useState<Record<string, string>>({});

  useEffect(() => {
    let cancelled = false;
    apiFetch(`${API}/api/documents`).then(response => readResponse<DocumentSummary[]>(response))
      .then(result => { if (!cancelled) setDocuments(result.value.filter(document => document.status === "Ready")); })
      .catch(() => { if (!cancelled) setMessage("Start the ASP.NET API to load reviewed documents."); });
    return () => { cancelled = true; };
  }, []);

  function applyCase(next: ReconciliationCase, nextEtag: string | null) {
    setItem(next); setEtag(nextEtag);
  }

  async function createCase(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const name = String(new FormData(form).get("name") ?? "").trim();
    if (!name) return;
    setBusy("create"); setMessage("");
    try {
      const result = await readResponse<ReconciliationCase>(await apiFetch(`${API}/api/reconciliation-cases`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name }),
      }));
      applyCase(result.value, result.etag); form.reset();
      setMessage("Case created. Link completed review revisions next.");
    } catch (error) { setMessage(error instanceof Error ? error.message : "Could not create case"); }
    finally { setBusy(null); }
  }

  async function linkDocument(role: string) {
    if (!item || !etag || !selected[role]) return;
    setBusy(`link-${role}`); setMessage("");
    try {
      const review = await readResponse<ReviewWorkspace>(await apiFetch(`${API}/api/documents/${selected[role]}/review`));
      if (!review.value.session || review.value.session.status !== "Completed")
        throw new Error(`${roleLabels[role]} requires a completed review.`);
      const result = await readResponse<ReconciliationCase>(await apiFetch(
        `${API}/api/reconciliation-cases/${item.id}/documents/${role}`, {
          method: "PUT", headers: { "Content-Type": "application/json", "If-Match": etag },
          body: JSON.stringify({ documentId: selected[role], reviewSessionId: review.value.session.id }),
        }));
      applyCase(result.value, result.etag); setMessage(`${roleLabels[role]} review pinned to the case.`);
    } catch (error) { setMessage(error instanceof Error ? error.message : "Could not link document"); }
    finally { setBusy(null); }
  }

  async function evaluate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!item || !etag) return;
    const form = new FormData(event.currentTarget);
    const numeric = (key: string) => {
      const raw = String(form.get(key) ?? "").trim(); return raw === "" ? null : Number(raw);
    };
    setBusy("evaluate"); setMessage("");
    try {
      const result = await readResponse<ReconciliationCase>(await apiFetch(
        `${API}/api/reconciliation-cases/${item.id}/evaluations`, {
          method: "POST", headers: { "Content-Type": "application/json", "If-Match": etag },
          body: JSON.stringify({
            quantityTolerance: numeric("quantity"), unitPriceTolerance: numeric("unitPrice"),
            taxTolerance: numeric("tax"), lineTotalTolerance: numeric("lineTotal"),
            documentTotalTolerance: numeric("documentTotal"),
          }),
        }));
      applyCase(result.value, result.etag);
      setMessage(`Evaluation ${result.value.runs[0]?.runNumber ?? ""} completed with ${result.value.runs[0]?.findings.length ?? 0} findings.`);
    } catch (error) { setMessage(error instanceof Error ? error.message : "Evaluation failed"); }
    finally { setBusy(null); }
  }

  async function resolveFinding(runId: string, finding: Finding, action: string) {
    if (!item || !etag) return;
    const reason = reasons[finding.id]?.trim();
    if (!reason) { setMessage("Add a reason before deciding a finding."); return; }
    setBusy(finding.id); setMessage("");
    try {
      const result = await readResponse<ReconciliationCase>(await apiFetch(
        `${API}/api/reconciliation-cases/${item.id}/runs/${runId}/findings/${finding.id}/resolutions`, {
          method: "POST", headers: { "Content-Type": "application/json", "If-Match": etag },
          body: JSON.stringify({ action, reason }),
        }));
      applyCase(result.value, result.etag);
      setReasons(current => ({ ...current, [finding.id]: "" }));
      setMessage(`${finding.ruleCode} recorded as ${action.toLowerCase()}.`);
    } catch (error) { setMessage(error instanceof Error ? error.message : "Could not record decision"); }
    finally { setBusy(null); }
  }

  const latest = item?.runs[0];
  return <main className="reconciliation-page">
    <nav className="phase-nav"><Link href="/">← Document review</Link><Link href="/questions">Search evidence</Link><span>Phase 4 · Reconciliation</span></nav>
    <header className="reconciliation-hero"><div><span className="eyebrow">EXPLAINABLE PROCUREMENT CONTROL</span>
      <h1>Compare the chain,<br/><em>keep the evidence.</em></h1></div>
      <p>Pin completed quotation, purchase-order, and invoice reviews. Every exception preserves its rule, tolerance, values, and source page.</p>
    </header>

    {!item && <section className="case-create"><span className="step">01 / CASE</span><h2>Start a reconciliation case</h2>
      <form onSubmit={createCase}><input name="name" aria-label="Case name" placeholder="August consulting procurement" required/>
        <button disabled={busy === "create"}>Create case</button></form></section>}

    {item && <>
      <Link href={`/questions?case=${item.id}`}>Ask about this case’s documents →</Link>
      <section className="case-summary"><div><span className="step">CASE</span><h2>{item.name}</h2>
        <p>{item.status} · {item.documents.length}/3 reviewed documents pinned</p></div><code>{item.id}</code></section>
      <section className="link-grid"><div className="section-heading"><span className="step">02 / SOURCES</span><h2>Pin completed reviews</h2></div>
        {roles.map(role => {
          const linked = item.documents.find(document => document.role === role);
          const source = documents.find(document => document.id === linked?.documentId);
          return <article key={role} className={linked ? "source-card linked" : "source-card"}>
            <span>{roleLabels[role]}</span>{linked ? <><strong>{source?.originalFilename ?? linked.documentId}</strong>
              <small>Review {linked.reviewSessionId.slice(0, 8)}… pinned</small></> : <>
              <select aria-label={`${roleLabels[role]} document`} value={selected[role] ?? ""}
                onChange={event => setSelected(current => ({ ...current, [role]: event.target.value }))}>
                <option value="">Select a ready document</option>{documents.map(document =>
                  <option key={document.id} value={document.id}>{document.originalFilename}</option>)}</select>
              <button disabled={!selected[role] || busy !== null} onClick={() => linkDocument(role)}>Pin review</button></>}
          </article>;
        })}
      </section>
      <section className="tolerance-panel"><div><span className="step">03 / EVALUATE</span><h2>Absolute tolerances</h2>
        <p>A difference equal to the tolerance passes.</p></div><form onSubmit={evaluate}>{[
          ["quantity", "Quantity", "0"], ["unitPrice", "Unit price", "0.01"], ["tax", "Tax / VAT", "0.01"],
          ["lineTotal", "Line total", "0.01"], ["documentTotal", "Document total", "0.01"],
        ].map(([name, label, initial]) => <label key={name}>{label}<input name={name} type="number" min="0" step="0.01" defaultValue={initial}/></label>)}
          <button disabled={busy !== null || item.documents.length === 0}>Run comparison</button></form></section>
    </>}

    {message && <p className="reconciliation-message" role="status">{message}</p>}

    {latest && <section className="findings"><div className="section-heading"><span className="step">04 / FINDINGS</span>
      <h2>Evaluation {latest.runNumber} <b>{latest.findings.length}</b></h2><p>Rule set {latest.ruleSetVersion} · immutable run</p></div>
      {latest.findings.length === 0 ? <div className="match-state"><strong>Exact match</strong><p>No configured discrepancy rule triggered.</p></div> :
        <div className="finding-list">{latest.findings.map(finding => {
          const left = latest.fields.find(field => field.id === finding.leftFieldSnapshotId);
          const right = latest.fields.find(field => field.id === finding.rightFieldSnapshotId);
          const resolution = finding.resolutions.at(-1);
          return <article key={finding.id} className={`finding finding-${finding.severity.toLowerCase()}`}>
            <div className="finding-head"><span>{finding.ruleCode}</span><i>{finding.severity}</i>{resolution && <b>{resolution.action}</b>}</div>
            <h3>{finding.explanation}</h3><div className="comparison-values"><div><small>{roleLabels[finding.leftRole ?? ""] ?? finding.leftRole ?? "Source"}</small>
              <strong>{finding.leftValue ?? "Unavailable"}</strong><em>Page {left?.page ?? "—"}</em></div><div><small>{roleLabels[finding.rightRole ?? ""] ?? finding.rightRole ?? "Target"}</small>
              <strong>{finding.rightValue ?? "Unavailable"}</strong><em>Page {right?.page ?? "—"}</em></div>{finding.difference !== undefined && <div className="delta"><small>Difference / tolerance</small>
              <strong>{finding.difference} / {finding.tolerance}</strong></div>}</div>
            {resolution && <p className="resolution-history">Latest decision: {resolution.action} — {resolution.reason}</p>}
            <div className="resolution-controls"><input aria-label={`Reason for ${finding.ruleCode}`} placeholder="Required decision reason"
              value={reasons[finding.id] ?? ""} onChange={event => setReasons(current => ({ ...current, [finding.id]: event.target.value }))}/>
              {(["Resolved", "Accepted", "Dismissed"] as const).map(action => <button key={action} disabled={busy !== null}
                className={action === "Dismissed" ? "secondary" : ""} onClick={() => resolveFinding(latest.id, finding, action)}>{action}</button>)}</div>
          </article>;
        })}</div>}
    </section>}
  </main>;
}
