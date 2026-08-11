"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { readReviewResponse, requireSuccess, sendReviewMutation } from "./review-request.mjs";

type Field = { name: string; value: string; confidence: number; page: number };
type Document = {
  id: string; originalFilename: string; sizeBytes: number; status: "Processing" | "Ready" | "Failed";
  documentType?: string; language?: string; extractor?: string; processingMs?: number;
  createdAt: string; fields: Field[]; errorMessage?: string;
};
type MachineValue = { id: string; value: string; valueType: string; confidence: number; evidence: { page: number; quotedText?: string }[] };
type ReviewDecision = { id: string; action: string; value?: string; valueType?: string; reason?: string; sequenceNumber: number };
type ReviewField = {
  fieldKey: string; occurrenceIndex: number; machine?: MachineValue; latestAction?: ReviewDecision;
  effective: { isPresent: boolean; value?: string; valueType?: string; source: string }; decisionStatus: string;
};
type ReviewWorkspace = {
  documentId: string; selectedExtractionRunId: string; extractionRunId: string;
  session?: { id: string; revisionNumber: number; status: string; concurrencyToken: string };
  fields: ReviewField[];
};

const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";
const labels: Record<string, string> = {
  invoice_number: "Invoice number", supplier: "Supplier", supplier_ar: "Supplier (Arabic)",
  invoice_date: "Invoice date", currency: "Currency", subtotal: "Subtotal", vat: "VAT", total: "Total", trn: "TRN"
};
const fieldIdentity = (field: ReviewField) => `${field.fieldKey}:${field.occurrenceIndex}`;

export default function Home() {
  const [documents, setDocuments] = useState<Document[]>([]);
  const [selected, setSelected] = useState<Document | null>(null);
  const [review, setReview] = useState<ReviewWorkspace | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [reviewBusy, setReviewBusy] = useState<string | null>(null);
  const [message, setMessage] = useState("");

  const applyReview = useCallback((workspace: ReviewWorkspace, nextEtag: string | null) => {
    setReview(workspace);
    setEtag(nextEtag);
    setDrafts(Object.fromEntries(workspace.fields.map(field => [fieldIdentity(field),
      field.effective.value ?? field.machine?.value ?? ""])));
  }, []);

  const loadReview = useCallback(async (document: Document) => {
    setReview(null); setEtag(null);
    if (document.status !== "Ready") return;
    const response = await fetch(`${API}/api/documents/${document.id}/review`);
    try { await requireSuccess(response); }
    catch (error) { setMessage(error instanceof Error ? error.message : "Could not load review"); return; }
    const result = await readReviewResponse<ReviewWorkspace>(response);
    applyReview(result.workspace, result.etag);
  }, [applyReview]);

  const chooseDocument = useCallback(async (document: Document) => {
    setSelected(document); setMessage("");
    await loadReview(document);
  }, [loadReview]);

  const loadDocuments = useCallback(async (preferredId?: string) => {
    const response = await fetch(`${API}/api/documents`);
    if (!response.ok) throw new Error("API unavailable");
    const data: Document[] = await response.json();
    setDocuments(data);
    const next = data.find(item => item.id === preferredId) ?? data[0] ?? null;
    setSelected(next);
    if (next) await loadReview(next); else { setReview(null); setEtag(null); }
  }, [loadReview]);

  useEffect(() => {
    let cancelled = false;
    fetch(`${API}/api/documents`)
      .then(response => response.ok ? response.json() : Promise.reject(new Error("API unavailable")))
      .then(async (data: Document[]) => {
        if (cancelled) return;
        setDocuments(data);
        const first = data[0] ?? null;
        setSelected(first);
        if (!first || first.status !== "Ready") return;
        const response = await fetch(`${API}/api/documents/${first.id}/review`);
        const result = await readReviewResponse<ReviewWorkspace>(response);
        if (!cancelled) applyReview(result.workspace, result.etag);
      })
      .catch(() => { if (!cancelled) setMessage("Start the ASP.NET API and Python AI service to load documents."); });
    return () => { cancelled = true; };
  }, [applyReview]);

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const formElement = event.currentTarget;
    const input = formElement.elements.namedItem("file") as HTMLInputElement;
    if (!input.files?.[0]) return;
    setBusy(true); setMessage("Processing securely on your local services…");
    const body = new FormData(); body.append("file", input.files[0]);
    try {
      const response = await fetch(`${API}/api/documents`, { method: "POST", body });
      await requireSuccess(response);
      const result: Document = await response.json();
      await loadDocuments(result.id); setMessage("Document ready for review."); formElement.reset();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Upload failed"); }
    finally { setBusy(false); }
  }

  async function startReview() {
    if (!selected) return;
    setReviewBusy("start");
    try {
      const response = await fetch(`${API}/api/documents/${selected.id}/reviews`, { method: "POST" });
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

  const active = review?.session?.status === "InProgress";
  const pendingCount = review?.fields.filter(field => field.machine && field.decisionStatus === "Unreviewed").length ?? 0;

  return <main>
    <header><div><span className="eyebrow">UAE · DOCUMENT INTELLIGENCE</span><h1>Document intelligence,<br/><em>with evidence.</em></h1></div>
      <p>Review Arabic and English business documents with traceable machine output and append-only human decisions.</p></header>
    <section className="upload-card">
      <div><span className="step">01 / INGEST</span><h2>Add a business document</h2><p>PDF, PNG, JPEG, or TXT · maximum 10 MB</p></div>
      <form onSubmit={upload}><input name="file" type="file" accept=".pdf,.png,.jpg,.jpeg,.txt" required/><button disabled={busy}>{busy ? "Processing…" : "Upload & extract"}</button></form>
      {message && <p className="message" role="status">{message}</p>}
    </section>
    <div className="workspace">
      <aside><span className="step">02 / QUEUE</span><h2>Documents <b>{documents.length}</b></h2>
        <div className="document-list">{documents.length === 0 && <p className="empty">No documents yet. Upload the synthetic sample in <code>samples/</code>.</p>}
          {documents.map(doc => <button key={doc.id} className={selected?.id === doc.id ? "document active" : "document"} onClick={() => chooseDocument(doc)}>
            <span className="file-icon">{doc.documentType?.slice(0, 3).toUpperCase() || "DOC"}</span><span><strong>{doc.originalFilename}</strong><small>{new Date(doc.createdAt).toLocaleString()} · {(doc.sizeBytes / 1024).toFixed(1)} KB</small></span><i className={`status ${doc.status.toLowerCase()}`}>{doc.status}</i>
          </button>)}</div>
      </aside>
      <section className="review"><span className="step">03 / REVIEW</span>
        {!selected ? <div className="placeholder"><div>DI</div><h2>Evidence appears here</h2><p>Select or upload a document to inspect extracted fields and page provenance.</p></div> : <>
          <div className="review-head"><div><h2>{selected.originalFilename}</h2><p>{selected.documentType ?? "Unknown type"} · {selected.language ?? "—"} · {selected.processingMs ?? 0} ms</p></div><span className="provider">{selected.extractor ?? "pending"}</span></div>
          {selected.errorMessage && <p className="error">{selected.errorMessage}</p>}
          {selected.status === "Ready" && review && <>
            <div className="review-toolbar">
              <div><strong>{review.session ? `Review ${review.session.revisionNumber} · ${review.session.status}` : "Not reviewed"}</strong><small>{review.session ? `${pendingCount} machine fields pending` : "Start a review to confirm or correct extracted values."}</small></div>
              {!review.session && <button onClick={startReview} disabled={reviewBusy === "start"}>Start review</button>}
              {active && <button onClick={completeReview} disabled={reviewBusy !== null || pendingCount > 0}>Complete review</button>}
              {review.session?.status === "Completed" && <span className="locked">Read only</span>}
            </div>
            <div className="review-fields">{review.fields.map(field => {
              const key = fieldIdentity(field); const isBusy = reviewBusy === key;
              return <article className={`review-field decision-${field.decisionStatus.toLowerCase()}`} key={key}>
                <div className="field-title"><span>{labels[field.fieldKey] ?? field.fieldKey}</span><i>{field.decisionStatus}</i></div>
                {field.machine ? <div className="machine-value"><small>AI output · {Math.round(field.machine.confidence * 100)}% · page {field.machine.evidence[0]?.page ?? 1}</small><p dir={field.fieldKey.endsWith("_ar") ? "rtl" : "auto"}>{field.machine.value}</p></div> : <div className="machine-value"><small>No machine value</small><p>Reviewer-added field</p></div>}
                <div className="effective-value"><small>Effective reviewed value</small><p>{field.effective.isPresent ? field.effective.value : "Excluded"}</p>{field.latestAction?.reason && <em>Reason: {field.latestAction.reason}</em>}</div>
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
