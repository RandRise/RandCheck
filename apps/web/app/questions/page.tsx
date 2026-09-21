"use client";

import Link from "next/link";
import { API, apiFetch } from "../security/api-fetch";
import { can, useEnterprise } from "../security/enterprise-provider";
import { FormEvent, useEffect, useRef, useState } from "react";
import "./questions.css";

type Source = { documentId: string; filename: string; reviewSessionId: string; extractionRunId: string; revisionNumber: number };
type PinnedSource = Source & { indexId: string };
type SourceList = { sources: Source[]; nextOffset: number | null };
type Hit = { hitId: string; indexId: string; chunkId: string; documentId: string; filename: string;
  reviewSessionId: string; extractionRunId: string; rank: number; kind: string; content: string;
  reviewActionId: string | null; spans: { id: string; pageNumber: number; quote: string }[] };
type QuestionResult = { stage: string; answerGenerationEnabled: boolean; question: string; indexIds: string[];
  retrieval: { id: string; status: string; policyVersion: string; replayed: boolean; hits: Hit[]; failureCode: string | null };
  answer: { status: string; reasonCode: string; message: string } | null;
  reviewedAnswer?: { status: string; reasonCode: string | null; missingParts: string[];
    claims: { text: string; citations: { hitId: string; spanId: string | null }[] }[] } | null };
type Location = { retrievalRunId: string; hitId: string; indexId: string; documentId: string; filename: string;
  extractionRunId: string; reviewSessionId: string; kind: string; reviewedFact: string | null;
  review: { actionId: string; action: string; fieldKey: string; occurrence: number; reason: string | null } | null;
  page: { pageNumber: number; byteStart: number; byteEnd: number; before: string; quote: string; after: string } | null };

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

async function request<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const response = await apiFetch(`${API}${path}`, { method: body === undefined ? "GET" : "POST",
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body), cache: "no-store", signal });
  if (!response.ok) {
    const messages: Record<number, string> = { 400: "Check the question and source selection.",
      404: "This source or saved result is unavailable to this user.",
      409: "The source or request is no longer eligible, or the request is still running. Reload its status before retrying.",
      413: "This source page is too large for the evidence viewer.",
      422: "Semantic search is not available for this question and selection yet. Use keyword search or reviewed fields.",
      503: "The processing service is unavailable. Your selection has been kept.",
      504: "The request timed out. Retry with the same request key to check for a saved result." };
    throw new Error(messages[response.status] ?? "The request could not be completed. Please try again.");
  }
  return await response.json() as T;
}

export default function QuestionsPage() {
  const { session } = useEnterprise(); const canPrepare = can(session, "Review");
  const [sources, setSources] = useState<Source[]>([]);
  const [nextOffset, setNextOffset] = useState<number | null>(null);
  const [sourceLoading, setSourceLoading] = useState(true);
  const [chosen, setChosen] = useState("");
  const [caseId, setCaseId] = useState("");
  const [caseName, setCaseName] = useState("");
  const [pinned, setPinned] = useState<PinnedSource[]>([]);
  const [question, setQuestion] = useState("");
  const [mode, setMode] = useState("evidence");
  const [retrievalMode, setRetrievalMode] = useState("lexical");
  const [result, setResult] = useState<QuestionResult | null>(null);
  const [location, setLocation] = useState<Location | null>(null);
  const [savedId, setSavedId] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [retryAvailable, setRetryAvailable] = useState(false);
  const epoch = useRef(0);
  const mounted = useRef(false);
  const pending = useRef<{ fingerprint: string; requestKey: string } | null>(null);
  const abort = useRef<AbortController | null>(null);
  const evidenceHeading = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    mounted.current = true;
    const linkedCase = new URLSearchParams(window.location.search).get("case");
    const controller = new AbortController();
    request<SourceList>("/api/cited-sources", undefined, controller.signal)
      .then(data => { setSources(data.sources); setNextOffset(data.nextOffset); })
      .catch(() => { if (!controller.signal.aborted) setMessage("Start the ASP.NET API to load completed reviews."); })
      .finally(() => { if (!controller.signal.aborted) {
        setSourceLoading(false);
        if (linkedCase && guid.test(linkedCase)) setCaseId(linkedCase);
      } });
    return () => { mounted.current = false; controller.abort(); abort.current?.abort(); };
  }, []);

  function invalidate() {
    epoch.current++; abort.current?.abort(); setBusy(null);
    setResult(null); setLocation(null); setMessage(""); pending.current = null; setRetryAvailable(false);
  }

  async function perform(label: string, action: (signal: AbortSignal, current: () => boolean) => Promise<void>) {
    abort.current?.abort();
    const controller = new AbortController(); abort.current = controller;
    const version = ++epoch.current;
    const current = () => mounted.current && version === epoch.current;
    setBusy(label); setMessage("");
    const timer = setTimeout(() => controller.abort(), 50000);
    try { await action(controller.signal, current); }
    catch (error) {
      if (current()) {
        setMessage(controller.signal.aborted ? "The request timed out. You can retry the same request." :
          error instanceof Error ? error.message : "The request failed.");
        if (label === "search") setRetryAvailable(true);
      }
    } finally { clearTimeout(timer); if (current()) setBusy(null); }
  }

  async function pinSource() {
    const source = sources.find(x => x.reviewSessionId === chosen);
    if (!source || pinned.some(x => x.documentId === source.documentId) || pinned.length >= 100) return;
    invalidate();
    await perform("pin", async (signal, current) => {
      const index = await request<{ id: string; status: string }>("/api/cited-indexes",
        { documentId: source.documentId, reviewSessionId: source.reviewSessionId }, signal);
      if (index.status !== "Ready" || !guid.test(index.id)) throw new Error("The source index is not ready.");
      if (current()) { setPinned(items => [...items, { ...source, indexId: index.id }]); setChosen(""); setCaseName(""); }
    });
  }

  async function loadCase() {
    if (!guid.test(caseId.trim())) { setMessage("Enter a valid reconciliation case ID."); return; }
    invalidate();
    await perform("case", async (signal, current) => {
      const data = await request<{ name: string; sources: PinnedSource[] }>(`/api/cited-cases/${caseId.trim()}/sources`, {}, signal);
      if (current()) { setPinned(data.sources); setCaseName(data.name); setChosen(""); }
    });
  }

  async function ask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!question.trim() || !pinned.length) return;
    if (mode === "reviewed" && pinned.length > 10) { setMessage("Select up to ten documents for reviewed answers."); return; }
    const indexIds = pinned.map(x => x.indexId).sort();
    const fingerprint = JSON.stringify([mode, retrievalMode, question.trim(), indexIds]);
    if (pending.current?.fingerprint !== fingerprint)
      pending.current = { fingerprint, requestKey: crypto.randomUUID() };
    const body = { question: question.trim(), indexIds, topK: 5, mode: retrievalMode, requestKey: pending.current.requestKey };
    setResult(null); setLocation(null); setRetryAvailable(false);
    await perform("search", async (signal, current) => {
      const path = mode === "reviewed" ? "/api/cited-questions/reviewed" :
        retrievalMode === "lexical" ? "/api/cited-questions" : "/api/cited-questions/semantic";
      const data = await request<QuestionResult>(path, body, signal);
      if (current()) { setResult(data); setSavedId(data.retrieval.id); }
    });
  }

  async function loadSaved(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const id = savedId.trim();
    if (!guid.test(id)) { setMessage("Enter a valid saved retrieval ID."); return; }
    setResult(null); setLocation(null);
    await perform("reload", async (signal, current) => {
      const data = await request<QuestionResult>(`/api/cited-questions/${id}`, undefined, signal);
      if (current()) setResult(data);
    });
  }

  async function openEvidence(hit: Hit, spanId?: string) {
    if (!result || ![result.retrieval.id, hit.hitId, ...(spanId ? [spanId] : [])].every(x => guid.test(x))) return;
    setLocation(null);
    await perform("evidence", async (signal, current) => {
      const data = await request<Location>(`/api/cited-questions/${result.retrieval.id}/hits/${hit.hitId}/evidence${spanId ? `?spanId=${spanId}` : ""}`, undefined, signal);
      if (current()) { setLocation(data); requestAnimationFrame(() => evidenceHeading.current?.focus()); }
    });
  }

  async function moreSources() {
    if (nextOffset === null) return;
    await perform("sources", async (signal, current) => {
      const data = await request<SourceList>(`/api/cited-sources?offset=${nextOffset}`, undefined, signal);
      if (current()) { setSources(items => [...new Map([...items, ...data.sources].map(x => [x.reviewSessionId, x])).values()]); setNextOffset(data.nextOffset); }
    });
  }

  return <main className="questions-page">
    <nav className="phase-nav"><Link href="/">← Document review</Link><Link href="/reconciliation">Reconciliation</Link><span>Phase 5 · Evidence search</span></nav>
    <header className="questions-hero"><div><span className="eyebrow">ASK WITHIN THE EVIDENCE</span>
      <h1>Find the source.<br/><em>Keep the context.</em></h1></div>
      <p>Ask about reviewed fields or search source text. Open the evidence behind each result, with the original review revision preserved.</p></header>
    <div className="questions-notice"><strong>Answers from reviewed fields</strong><span>Ask about identifiers, dates, amounts and comparisons. Missing or unsupported facts remain unanswered. Evidence search is also available.</span></div>

    <div className="questions-grid"><section className="question-composer" aria-labelledby="source-title">
      <span className="step">01 / SELECTED CORPUS</span><h2 id="source-title">Pin your sources</h2>
      <p className="q-muted">One completed review per document. Only these pinned sources will be searched.</p>
      <label htmlFor="question-case">Reconciliation case ID</label>
      {!canPrepare && <p className="q-message">Your Viewer role can open saved results. A reviewer prepares document and case sources.</p>}
      <input id="question-case" value={caseId} disabled={busy !== null} onChange={event => setCaseId(event.target.value)} />
      <button type="button" className="q-primary" disabled={!canPrepare || busy !== null || !caseId.trim()} onClick={loadCase}>Use case sources</button>
      {caseName && <p className="q-muted">Sources loaded from {caseName}. This selection keeps those review revisions.</p>}
      <label htmlFor="question-source">Completed review</label>
      <select id="question-source" value={chosen} disabled={busy !== null || sourceLoading} onChange={event => setChosen(event.target.value)}>
        <option value="">{sourceLoading ? "Loading completed reviews…" : "Select a completed review"}</option>
        {sources.map(source => <option key={source.reviewSessionId} value={source.reviewSessionId}
          disabled={pinned.some(x => x.documentId === source.documentId)}>{source.filename} · review {source.revisionNumber}</option>)}
      </select>
      <button className="q-primary" disabled={!canPrepare || !chosen || busy !== null || pinned.length >= 100} onClick={pinSource}>{busy === "pin" ? "Preparing index…" : "Add source"}</button>
      {!sourceLoading && sources.length === 0 && <p className="q-muted">No completed reviews are available. Complete a review in the document workspace first.</p>}
      {nextOffset !== null && <button className="q-secondary" disabled={busy !== null} onClick={moreSources}>Load more reviews</button>}
      <ul className="q-pinned">{pinned.map(source => <li key={source.indexId}><div><strong>{source.filename}</strong>
        <small>Review {source.revisionNumber} · {source.reviewSessionId.slice(0, 8)}</small></div>
        <button aria-label={`Remove ${source.filename}`} disabled={busy !== null} onClick={() => { invalidate(); setCaseName(""); setPinned(items => items.filter(x => x.indexId !== source.indexId)); }}>×</button></li>)}</ul>
      <form onSubmit={ask}><label htmlFor="question-mode">Question mode</label>
        <select id="question-mode" value={mode} disabled={busy !== null} onChange={event => { invalidate(); setMode(event.target.value); }}>
          <option value="evidence">Search evidence</option><option value="reviewed">Answer from reviewed fields</option>
        </select>
        {mode === "evidence" && <><label htmlFor="retrieval-mode">Search method</label>
          <select id="retrieval-mode" value={retrievalMode} disabled={busy !== null} onChange={event => { invalidate(); setRetrievalMode(event.target.value); }}>
            <option value="lexical">Keyword search</option><option value="vector">Vector search</option>
            <option value="hybrid-nonredundant">Hybrid search</option>
          </select><p className="q-muted">Vector and hybrid search reuse prepared searches for the selected reviews. Unavailable searches do not start paid processing.</p></>}
        {mode === "reviewed" && <p className="q-muted">Try “When is the invoice due?” or “What is the total difference between the order and invoice?” Select one document per role. Training/onboarding line comparisons are supported.</p>}
        <label htmlFor="question-text">Your question</label><textarea id="question-text" rows={4} maxLength={4000}
        placeholder="What total is stated on the invoice?" value={question} onChange={event => { invalidate(); setQuestion(event.target.value); }}/>
        <div className="q-question-meta"><span>{question.length}/4,000</span><span>{pinned.length} source{pinned.length === 1 ? "" : "s"} selected</span></div>
        <button className="q-primary" disabled={busy !== null || !question.trim() || !pinned.length}>{busy === "search" ? "Checking sources…" : retryAvailable ? "Retry same request" : mode === "reviewed" ? "Answer from selected reviews" : "Search selected sources"}</button>
      </form>
      <details className="q-reload"><summary>Reopen a saved result</summary><form onSubmit={loadSaved}><label htmlFor="saved-question">Retrieval ID</label>
        <input id="saved-question" value={savedId} onChange={event => setSavedId(event.target.value)} placeholder="Paste a retrieval ID"/>
        <button className="q-secondary" disabled={busy !== null || !savedId.trim()}>Load saved result</button></form></details>
    </section>

    <section className="question-results" aria-labelledby="results-title" aria-busy={busy === "search" || busy === "reload"}>
      <span className="step">02 / RETRIEVED EVIDENCE</span><h2 id="results-title">Read before you conclude</h2>
      {message && <p className="q-error" role="alert">{message}</p>}
      {!result && <div className="q-empty"><span aria-hidden="true">↗</span><h3>{busy === "search" ? "Searching the pinned corpus" : "Every result starts with a source"}</h3>
        <p>Choose completed reviews and ask a question. Results will keep their document, review, and page provenance.</p></div>}
      {result && <><div className="q-run"><h3>{result.question}</h3><p>{result.indexIds.length} frozen source{result.indexIds.length === 1 ? "" : "s"} · {result.retrieval.policyVersion}</p>
        <code>Retrieval ID: {result.retrieval.id}</code>{result.retrieval.replayed && <small>Loaded from saved history</small>}</div>
        {result.reviewedAnswer ? <div className="q-reviewed-answer" aria-label="Reviewed answer">
          <h3>{result.reviewedAnswer.status === "Answered" ? "Answer from reviewed fields" : result.reviewedAnswer.status === "PartiallyAnswered" ? "Partial answer" : "No supported answer"}</h3>
          {result.reviewedAnswer.reasonCode && <p className="q-muted">{result.reviewedAnswer.reasonCode === "unsupported_scope" ?
            "This question is outside the supported reviewed-field questions. Try a single field or amount comparison, or use evidence search." :
            "The selected reviews do not contain enough unambiguous information to answer fully."}</p>}
          {result.reviewedAnswer.claims.map((claim, n) => <article className="q-hit" key={n}><p>{claim.text}</p>
            <div className="q-evidence-buttons">{claim.citations.map(citation => {
              const hit = result.retrieval.hits.find(h => h.hitId === citation.hitId);
              return hit ? <button className="q-secondary" key={citation.hitId} disabled={busy !== null}
                onClick={() => openEvidence(hit, citation.spanId ?? undefined)}>Source: {hit.filename} · {hit.content.split(":", 1)[0]}</button> : null;
            })}</div></article>)}
          <p className="q-muted">Saved with the selected review revisions. Source buttons open reviewed evidence; the evidence cards below also link to original page text.</p>
        </div> : result.stage === "InsufficientEvidence" && result.answer?.status === "InsufficientEvidence" ?
          <div className="q-refusal" role="status"><strong>Insufficient evidence</strong><p>{result.answer.message}</p><small>This does not prove the fact is absent. Keyword search can miss alternative wording.</small></div> :
          result.stage === "EvidenceReady" ? <p className="q-result-label">{result.retrieval.hits.length} evidence hit{result.retrieval.hits.length === 1 ? "" : "s"} · Not a generated answer</p> :
          <p className="q-error" role="status">Retrieval status: {result.retrieval.status}. No answer is available.</p>}
        {result.retrieval.hits.map(hit => <article className="q-hit" key={hit.hitId}>
          <div className="q-hit-heading"><span className="q-rank">{hit.rank.toString().padStart(2, "0")}</span><div><h3>{hit.filename}</h3>
            <small>{hit.kind === "ReviewedFact" ? "Reviewed fact" : "Original page text"} · review {hit.reviewSessionId.slice(0, 8)}</small></div></div>
          <pre>{hit.content}</pre>{hit.kind === "ReviewedFact" && <p className="q-muted">Reviewed values can differ from the original page evidence.</p>}
          <div className="q-evidence-buttons">{hit.spans.map((span, n) => <button className="q-secondary" disabled={busy !== null} key={span.id}
            onClick={() => openEvidence(hit, span.id)}>Open page {span.pageNumber}{hit.spans.length > 1 ? ` · span ${n + 1}` : ""}</button>)}
            {hit.kind === "ReviewedFact" && <button className="q-secondary" disabled={busy !== null} onClick={() => openEvidence(hit)}>Open review provenance</button>}</div>
        </article>)}
      </>}
    </section></div>

    {location && <section className="q-source-view" aria-labelledby="evidence-title"><div className="q-source-top"><div><span className="step">03 / SOURCE VERIFICATION</span>
      <h2 id="evidence-title" tabIndex={-1} ref={evidenceHeading}>{location.filename}</h2></div><button className="q-secondary" onClick={() => setLocation(null)}>Close source</button></div>
      <p className="q-muted">Pinned review {location.reviewSessionId} · extraction {location.extractionRunId}</p>
      {location.review && <div className="q-reviewed"><strong>Reviewed fact — {location.review.action}</strong><pre>{location.reviewedFact}</pre>
        <p>{location.review.reason ?? "No review reason recorded."}</p><small>Review action {location.review.actionId}</small>
        {!location.page && <p>Review provenance view. No page quotation is being asserted here.</p>}</div>}
      {location.page && <><h3>Page {location.page.pageNumber} · canonical extracted text</h3>
        <p className="q-muted">Highlighted UTF-8 bytes {location.page.byteStart}–{location.page.byteEnd}. This is extracted text, not a PDF image.{location.review && " The highlight is original evidence, not a quotation of the corrected value."}</p>
        <pre className="q-canonical">{location.page.before}<mark>{location.page.quote}</mark>{location.page.after}</pre></>}
    </section>}
  </main>;
}
