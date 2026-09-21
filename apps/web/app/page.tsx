"use client";

import Link from "next/link";
import { API, apiFetch } from "./security/api-fetch";
import { can, useEnterprise } from "./security/enterprise-provider";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { readReviewResponse, requireSuccess, sendReviewMutation } from "./review-request.mjs";
import { SourceDownload } from "./source-download";

type Field = { name: string; value: string; confidence: number; page: number };
type Document = {
  id: string; originalFilename: string; sizeBytes: number; status: "Processing" | "Ready" | "Failed" | "Cancelled";
  documentType?: string; language?: string; extractor?: string; processingMs?: number;
  createdAt: string; fields: Field[]; errorMessage?: string;
};
type ProcessingStatus = {
  jobId: string; extractionRunId: string; status: "Queued" | "Processing" | "Ready" | "Partial" | "Failed" | "Cancelled";
  attemptCount: number; retryAt?: string; errorCode?: string; errorMessage?: string;
};
type MachineValue = { id: string; value: string; valueType: string; confidence: number | null; evidence: { page: number; quotedText?: string }[] };
type ReviewDecision = { id: string; action: string; value?: string; valueType?: string; reason?: string; sequenceNumber: number };
type ReviewField = {
  fieldKey: string; occurrenceIndex: number; machine?: MachineValue; latestAction?: ReviewDecision;
  effective: { isPresent: boolean; value?: string; valueType?: string; source: string }; decisionStatus: string; cues: string[];
};
type ReviewWorkspace = {
  documentId: string; selectedExtractionRunId: string; extractionRunId: string;
  session?: { id: string; revisionNumber: number; status: string; concurrencyToken: string };
  fields: ReviewField[];
};


const labels: Record<string, string> = {
  invoice_number: "Invoice number", invoice_date: "Invoice date", supplier_trn: "Supplier TRN",
  currency: "Currency", subtotal: "Subtotal", vat: "VAT", total: "Total", payment_terms: "Payment terms",
  supplier: "Supplier", supplier_ar: "Supplier (Arabic)", trn: "TRN"
};
const cueLabels: Record<string, string> = {
  MissingMachineValue: "Missing extraction",
  LowConfidence: "Low confidence",
  ConfidenceUnavailable: "Confidence unavailable",
};
const fieldIdentity = (field: ReviewField) => `${field.fieldKey}:${field.occurrenceIndex}`;

export default function Home() {
  const { session } = useEnterprise();
  const canReview = can(session, "Review"); const canOperate = can(session, "Operate");
  const [documents, setDocuments] = useState<Document[]>([]);
  const [selected, setSelected] = useState<Document | null>(null);
  const [review, setReview] = useState<ReviewWorkspace | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [reviewBusy, setReviewBusy] = useState<string | null>(null);
  const [message, setMessage] = useState("");
  const [processing, setProcessing] = useState<ProcessingStatus | null>(null);

  const applyReview = useCallback((workspace: ReviewWorkspace, nextEtag: string | null) => {
    setReview(workspace);
    setEtag(nextEtag);
    setDrafts(Object.fromEntries(workspace.fields.map(field => [fieldIdentity(field),
      field.effective.value ?? field.machine?.value ?? ""])));
  }, []);

  const loadReview = useCallback(async (document: Document) => {
    setReview(null); setEtag(null);
    if (document.status !== "Ready" && document.fields.length === 0) return;
    const response = await apiFetch(`${API}/api/documents/${document.id}/review`);
    try { await requireSuccess(response); }
    catch (error) { setMessage(error instanceof Error ? error.message : "Could not load review"); return; }
    const result = await readReviewResponse<ReviewWorkspace>(response);
    applyReview(result.workspace, result.etag);
  }, [applyReview]);

  const chooseDocument = useCallback(async (document: Document) => {
    setSelected(document); setProcessing(null); setMessage("");
    await loadReview(document);
  }, [loadReview]);

  const loadProcessing = useCallback(async (documentId: string) => {
    const response = await apiFetch(`${API}/api/documents/${documentId}/processing`);
    if (response.status === 404) return null;
    await requireSuccess(response);
    const state: ProcessingStatus = await response.json();
    setProcessing(state);
    return state;
  }, []);

  const loadDocuments = useCallback(async (preferredId?: string) => {
    const response = await apiFetch(`${API}/api/documents`);
    if (!response.ok) throw new Error("API unavailable");
    const data: Document[] = await response.json();
    setDocuments(data);
    const next = data.find(item => item.id === preferredId) ?? data[0] ?? null;
    setSelected(next);
    if (next) await loadReview(next); else { setReview(null); setEtag(null); }
  }, [loadReview]);

  useEffect(() => {
    let cancelled = false;
    apiFetch(`${API}/api/documents`)
      .then(response => response.ok ? response.json() : Promise.reject(new Error("API unavailable")))
      .then(async (data: Document[]) => {
        if (cancelled) return;
        setDocuments(data);
        const first = data[0] ?? null;
        setSelected(first);
        if (!first || first.status !== "Ready") return;
        const response = await apiFetch(`${API}/api/documents/${first.id}/review`);
        const result = await readReviewResponse<ReviewWorkspace>(response);
        if (!cancelled) applyReview(result.workspace, result.etag);
      })
      .catch(() => { if (!cancelled) setMessage("Start the ASP.NET API and Python AI service to load documents."); });
    return () => { cancelled = true; };
  }, [applyReview]);

  useEffect(() => {
    if (!selected || (selected.status !== "Processing" && !processing) ||
        processing && ["Ready", "Partial", "Failed", "Cancelled"].includes(processing.status)) return;
    let cancelled = false;
    const poll = async () => {
      try {
        const state = await loadProcessing(selected.id);
        if (!cancelled && state && ["Ready", "Partial", "Failed", "Cancelled"].includes(state.status))
          await loadDocuments(selected.id);
      } catch { if (!cancelled) setMessage("Processing status is temporarily unavailable. Retrying…"); }
    };
    void poll();
    const timer = window.setInterval(poll, 1500);
    return () => { cancelled = true; window.clearInterval(timer); };
  }, [selected, processing, loadDocuments, loadProcessing]);

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const formElement = event.currentTarget;
    const input = formElement.elements.namedItem("file") as HTMLInputElement;
    if (!input.files?.[0]) return;
    setBusy(true); setMessage("Uploading securely to the processing queue…");
    const body = new FormData(); body.append("file", input.files[0]);
    try {
      const response = await apiFetch(`${API}/api/documents`, { method: "POST", body });
      await requireSuccess(response);
      const result: Document = await response.json();
      await loadDocuments(result.id); setMessage("Document queued. You can leave it processing in the background."); formElement.reset();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Upload failed"); }
    finally { setBusy(false); }
  }

  async function cancelProcessing() {
    if (!processing || !selected) return;
    setBusy(true);
    try {
      const response = await apiFetch(`${API}/api/extraction-jobs/${processing.jobId}/cancel`, { method: "POST" });
      await requireSuccess(response); await loadProcessing(selected.id); await loadDocuments(selected.id);
      setMessage("Processing cancelled.");
    } catch (error) { setMessage(error instanceof Error ? error.message : "Could not cancel processing"); }
    finally { setBusy(false); }
  }

  async function reprocess() {
    if (!selected) return;
    setBusy(true);
    try {
      const response = await apiFetch(`${API}/api/documents/${selected.id}/reprocess`, { method: "POST" });
      await requireSuccess(response); const state: ProcessingStatus = await response.json();
      setProcessing(state); await loadDocuments(selected.id);
      setMessage("Reprocessing queued. Existing reviewable output stays visible until replacement succeeds.");
    } catch (error) { setMessage(error instanceof Error ? error.message : "Could not queue reprocessing"); }
    finally { setBusy(false); }
  }

  async function startReview() {
    if (!selected) return;
    setReviewBusy("start");
    try {
      const response = await apiFetch(`${API}/api/documents/${selected.id}/reviews`, { method: "POST" });
      const result = await readReviewResponse<ReviewWorkspace>(response);
      applyReview(result.workspace, result.etag); setMessage("Review started. Machine values remain unchanged.");
    } catch (error) { setMessage(error instanceof Error ? error.message : "Could not start review"); }
    finally { setReviewBusy(null); }
  }

  async function recordAction(field: ReviewField, action: "Confirmed" | "Corrected" | "Cleared") {
    if (!selected || !review?.session || !etag) return;
    const key = fieldIdentity(field); setReviewBusy(key);
    const body: Record<string, unknown> = { action, machineFieldValueId: field.machine?.id };
    if (!field.machine) Object.assign(body, { fieldKey: field.fieldKey, occurrenceIndex: field.occurrenceIndex });
    if (action === "Corrected") Object.assign(body, { valueType: field.machine?.valueType ?? "String", value: drafts[key], reason: reasons[key] });
    if (action === "Cleared") body.reason = reasons[key];
    try {
      if (action !== "Confirmed" && !reasons[key]?.trim()) throw new Error("Add a reason before changing or clearing a value.");
      const result = await sendReviewMutation<ReviewWorkspace>({
        fetcher: apiFetch,
        url: `${API}/api/documents/${selected.id}/reviews/${review.session.id}/actions`,
        init: { method: "POST", headers: { "Content-Type": "application/json", "If-Match": etag }, body: JSON.stringify(body) },
        onStale: () => loadReview(selected),
      });
      applyReview(result.workspace, result.etag); setMessage(`${labels[field.fieldKey] ?? field.fieldKey} saved as ${action.toLowerCase()}.`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Review action failed");
    } finally { setReviewBusy(null); }
  }

  async function addField(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected || !review?.session || !etag) return;
    const formElement = event.currentTarget;
    const form = new FormData(formElement); setReviewBusy("add");
    try {
      const fieldKey = String(form.get("fieldKey") ?? "").trim();
      const value = String(form.get("value") ?? "").trim();
      const reason = String(form.get("reason") ?? "").trim();
      if (!fieldKey || !value || !reason) throw new Error("Field key, value, and reason are required.");
      const result = await sendReviewMutation<ReviewWorkspace>({
        fetcher: apiFetch,
        url: `${API}/api/documents/${selected.id}/reviews/${review.session.id}/actions`,
        init: {
          method: "POST", headers: { "Content-Type": "application/json", "If-Match": etag },
          body: JSON.stringify({ action: "Added", fieldKey, occurrenceIndex: 0, valueType: "String", value, reason }),
        },
        onStale: () => loadReview(selected),
      });
      applyReview(result.workspace, result.etag); formElement.reset(); setMessage("Reviewer-added field saved.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not add field");
    }
    finally { setReviewBusy(null); }
  }

  async function completeReview() {
    if (!selected || !review?.session || !etag) return;
    setReviewBusy("complete");
    try {
      const result = await sendReviewMutation<ReviewWorkspace>({
        fetcher: apiFetch,
        url: `${API}/api/documents/${selected.id}/reviews/${review.session.id}/complete`,
        init: { method: "POST", headers: { "If-Match": etag } },
        onStale: () => loadReview(selected),
      });
      applyReview(result.workspace, result.etag); setMessage("Review completed and locked.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Could not complete review");
    }
    finally { setReviewBusy(null); }
  }

  async function exportHistory() {
    if (!selected) return;
    try {
      const response = await apiFetch(`${API}/api/documents/${selected.id}/review-history`);
      await requireSuccess(response);
      const url = URL.createObjectURL(await response.blob());
      const link = window.document.createElement("a"); link.href = url; link.download = `review-history-${selected.id}.json`;
      link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch { setMessage("Review history could not be downloaded."); }
  }

  const active = canReview && review?.session?.status === "InProgress";
  const pendingCount = review?.fields.filter(field => field.machine && field.decisionStatus === "Unreviewed").length ?? 0;
  const attentionCount = review?.fields.filter(field => (field.cues?.length ?? 0) > 0).length ?? 0;

  return <main>
    <nav className="phase-nav"><span>Phases 1–3 · Document review</span><Link href="/questions">Search evidence</Link><Link href="/reconciliation">Reconcile documents →</Link></nav>
    <header><div><span className="eyebrow">UAE · DOCUMENT INTELLIGENCE</span><h1>Document intelligence,<br/><em>with evidence.</em></h1></div>
      <p>Review English business documents with traceable machine output and append-only human decisions. Arabic and bilingual support is planned.</p></header>
    <section className="upload-card">
      <div><span className="step">01 / INGEST</span><h2>Add a business document</h2><p>PDF, PNG, JPEG, or TXT · maximum 10 MB</p></div>
      <form onSubmit={upload}><fieldset disabled={!canOperate || busy} style={{border:0,padding:0,display:"contents"}}><input name="file" type="file" accept=".pdf,.png,.jpg,.jpeg,.txt" required/><button disabled={!canOperate || busy}>{busy ? "Processing…" : "Upload & extract"}</button></fieldset></form>
      {message && <p className="message" role="status">{message}</p>}
    </section>
    <div className="workspace">
      <aside><span className="step">02 / QUEUE</span><h2>Documents <b>{documents.length}</b></h2>
        <div className="document-list">{documents.length === 0 && <p className="empty">No documents yet. Upload a Phase 2 PDF from <code>samples/phase-2/</code>.</p>}
          {documents.map(doc => <button key={doc.id} className={selected?.id === doc.id ? "document active" : "document"} onClick={() => chooseDocument(doc)}>
            <span className="file-icon">{doc.documentType?.slice(0, 3).toUpperCase() || "DOC"}</span><span><strong>{doc.originalFilename}</strong><small>{new Date(doc.createdAt).toLocaleString()} · {(doc.sizeBytes / 1024).toFixed(1)} KB</small></span><i className={`status ${doc.status.toLowerCase()}`}>{doc.status}</i>
          </button>)}</div>
      </aside>
      <section className="review"><span className="step">03 / REVIEW</span>
        {!selected ? <div className="placeholder"><div>DI</div><h2>Evidence appears here</h2><p>Select or upload a document to inspect extracted fields and page provenance.</p></div> : <>
          <div className="review-head"><div><h2>{selected.originalFilename}</h2><p>{selected.documentType ?? "Unknown type"} · {selected.language ?? "—"} · {selected.processingMs ?? 0} ms</p></div><span className="provider">{selected.extractor ?? "pending"}</span></div>
          {session.tenantId && can(session, "Read") && <SourceDownload key={selected.id} documentId={selected.id}/>}
          {(selected.status === "Processing" || processing) && <div className={`processing-panel state-${(processing?.status ?? "Queued").toLowerCase()}`} aria-live="polite">
            <div><strong>{processing?.status ?? "Queued"}</strong><small>{processing?.status === "Processing" ? `Extraction attempt ${processing.attemptCount} is running.` : processing?.retryAt ? `Retry scheduled for ${new Date(processing.retryAt).toLocaleTimeString()}.` : "Waiting for a durable worker claim."}</small></div>
            {processing && ["Queued", "Processing"].includes(processing.status) && <button className="secondary" disabled={!canOperate || busy} onClick={cancelProcessing}>Cancel</button>}
          </div>}
          {processing?.status === "Partial" && <p className="warning">Extraction completed with warnings. Review the available fields and evidence carefully.</p>}
          {(selected.status === "Failed" || selected.status === "Cancelled" || processing?.status === "Failed" || processing?.status === "Cancelled") && <div className="terminal-state"><p>{processing?.errorMessage ?? selected.errorMessage ?? (processing?.status === "Cancelled" ? "Processing was cancelled." : "Processing did not complete.")}</p><button disabled={!canOperate || busy} onClick={reprocess}>Try processing again</button></div>}
          {selected.status === "Ready" && <div className="reprocess-row"><button className="secondary" disabled={!canOperate || busy || !!processing && ["Queued", "Processing"].includes(processing.status)} onClick={reprocess}>Reprocess document</button></div>}
          {selected.errorMessage && <p className="error">{selected.errorMessage}</p>}
          {(selected.status === "Ready" || selected.fields.length > 0) && review && <>
            <div className="review-toolbar">
              <div><strong>{review.session ? `Review ${review.session.revisionNumber} · ${review.session.status}` : "Not reviewed"}</strong><small>{review.session ? `${pendingCount} machine fields pending · ${attentionCount} need attention` : `${attentionCount} fields need attention before review.`}</small></div>
              {canReview && !review.session && <button onClick={startReview} disabled={reviewBusy === "start"}>Start review</button>}
              {active && <button onClick={completeReview} disabled={reviewBusy !== null || pendingCount > 0}>Complete review</button>}
              {review.session?.status === "Completed" && <span className="locked">Read only</span>}
              {review.session && selected && <button className="history-link" onClick={exportHistory}>Export history</button>}
            </div>
            <div className="review-fields">{review.fields.map(field => {
              const key = fieldIdentity(field); const isBusy = reviewBusy === key; const cues = field.cues ?? [];
              return <article className={`review-field decision-${field.decisionStatus.toLowerCase()} ${cues.length ? "needs-attention" : ""}`} key={key}>
                <div className="field-title"><span>{labels[field.fieldKey] ?? field.fieldKey}</span><i>{field.decisionStatus}</i></div>
                {cues.length > 0 && <div className="review-cues" role="alert">{cues.map(cue => <span key={cue}>{cueLabels[cue] ?? cue}</span>)}</div>}
                {field.machine ? <div className="machine-value"><small>AI output · {field.machine.confidence === null ? "confidence unavailable" : `${Math.round(field.machine.confidence * 100)}%`} · page {field.machine.evidence[0]?.page ?? 1}</small><p dir={field.fieldKey.endsWith("_ar") ? "rtl" : "auto"}>{field.machine.value}</p></div> : <div className="machine-value"><small>No machine value</small><p>{field.decisionStatus === "Missing" ? "Expected field was not extracted" : "Reviewer-added field"}</p></div>}
                <div className="effective-value"><small>Effective reviewed value</small><p>{field.effective.isPresent ? field.effective.value : field.decisionStatus === "Missing" ? "Not extracted" : "Excluded"}</p>{field.latestAction?.reason && <em>Reason: {field.latestAction.reason}</em>}</div>
                {active && field.machine && <div className="field-controls">
                  <label>Reviewed value<input value={drafts[key] ?? ""} onChange={event => setDrafts(current => ({ ...current, [key]: event.target.value }))}/></label>
                  <label>Reason for change or clear<input value={reasons[key] ?? ""} onChange={event => setReasons(current => ({ ...current, [key]: event.target.value }))}/></label>
                  <div><button disabled={isBusy} onClick={() => recordAction(field, "Confirmed")}>Confirm AI value</button><button disabled={isBusy} onClick={() => recordAction(field, "Corrected")}>Save correction</button><button className="secondary" disabled={isBusy} onClick={() => recordAction(field, "Cleared")}>Clear</button></div>
                </div>}
                {active && !field.machine && field.decisionStatus === "Added" && <div className="field-controls single">
                  <label>Reason to clear<input value={reasons[key] ?? ""} onChange={event => setReasons(current => ({ ...current, [key]: event.target.value }))}/></label>
                  <div><button className="secondary" disabled={isBusy} onClick={() => recordAction(field, "Cleared")}>Clear added field</button></div>
                </div>}
              </article>;
            })}</div>
            {active && <form className="add-field" onSubmit={addField}><div><strong>Add a missing field</strong><small>Reviewer additions have no AI confidence or fabricated evidence.</small></div><input name="fieldKey" placeholder="field_key"/><input name="value" placeholder="Value"/><input name="reason" placeholder="Reason"/><button disabled={reviewBusy !== null}>Add field</button></form>}
          </>}
        </>}
      </section>
    </div>
  </main>;
}
