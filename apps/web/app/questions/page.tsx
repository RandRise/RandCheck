"use client";

import { BidiText } from "../bidi-text";
import { PageWorkflow } from "../page-workflow";
import { can, useEnterprise } from "../security/enterprise-provider";
import Link from "next/link";
import { resultUrl, useQuestionsWorkflow } from "./use-questions-workflow";
import { REVIEW_SEARCH_MAX_LENGTH } from "./questions.constants";
import "./questions.css";
import "./questions-layout.css";

export default function QuestionsPage() {
  const { session } = useEnterprise(); const canPrepare = can(session, "Review"); const canRead = can(session, "Read");
  if (!canRead) return <main><h1>Your role does not include this workspace.</h1></main>;
  return <QuestionsWorkspace key={`${session.organizationId}:${session.actorId}:${session.role}`} canPrepare={canPrepare}/>;
}

function QuestionsWorkspace({ canPrepare }: { canPrepare: boolean }) {
  const {
    canRead, caseContext, caseName, linkedSource, linkedPinned,
    sourceLoading, sourceError, chosen, setChosen, caseId, setCaseId, cases,
    caseListError, caseLoading, caseOffset, nextOffset, availableSources, pinned, savedCorpus,
    question, mode, retrievalMode, sourceIds, busy, retryAvailable,
    result, location, setLocation, message, savedId, recent, recentError, evidenceHeading, resultHeading,
    pinSource, loadCase, ask, loadSaved, openSaved, openEvidence, moreCases,
    browseSources, setBrowseSources, sourceBrowser, searchReviews, pageReviews,
    retrySources, retryCases, retryRecent, removeSource, clearSavedSources, useSavedSources,
    changeMode, changeRetrievalMode, changeQuestion, changeSavedId,
  } = useQuestionsWorkflow(canPrepare);
  if (!canRead) return <main><h1>Your role does not include this workspace.</h1></main>;
  return <main className="questions-page">
    <header className="page-heading questions-hero"><h1>Ask about documents</h1>
      <p>Choose the saved reviews to use, ask your question, then check the sources behind the answer.</p></header>
    <PageWorkflow label="How to ask about documents" steps={[
      { title: "Choose sources", description: "Use a saved case or add completed document reviews." },
      { title: "Ask your question", description: "Ask about a value, or search the original text." },
      { title: "Read the evidence", description: "Open the sources. Missing facts stay unanswered." },
    ]}/>
    <div className="questions-notice"><strong>Answers from reviewed fields</strong><span>Ask about identifiers, dates, amounts and comparisons. Missing or unsupported facts remain unanswered. Evidence search is also available.</span></div>
    {linkedSource && <section className="q-case-context" aria-label="Document context"><h2 dir="auto">{linkedSource.filename}</h2><Link className="app-button" href={`/?document=${linkedSource.documentId}`}>Back to document</Link><p>Completed review {linkedSource.revisionNumber}. {linkedPinned ? "This exact review is pinned for your question." : canPrepare ? "Choose Add source to use this exact review in your question." : "A reviewer can prepare this review. You can reopen your saved results below."}</p></section>}
    {caseContext && <section className="q-case-context" aria-label="Case context"><h2 dir="auto">{caseContext.name}</h2><Link className="app-button" href={`/reconciliation?case=${caseContext.id}`}>Back to comparison</Link><p>{caseName ? "These sources keep the reviews pinned to this case." : canPrepare ? "Use this case's saved documents together. You do not need to find each review yourself." : "Your access allows reading this case, but not preparing its documents."}</p>{caseContext.version && <details><summary>Loaded case version</summary><code>{caseContext.version}</code></details>}</section>}

    <div className="questions-grid"><section className="question-composer" aria-labelledby="source-title">
      <h2 id="source-title">1. Choose documents</h2>
      <p className="q-muted">{browseSources ? "Choose one completed review per document. Your question will use only these saved versions." : "Use the saved documents together. Ask about one document or compare them."}</p>
      {!canPrepare && <p className="q-message">Your Viewer role can open saved results available to this account and reuse their sources for questions. A reviewer prepares new document and case sources.</p>}
      {caseName && <p className="q-muted">Sources loaded from {caseName}. This selection keeps those review revisions.</p>}
      {!browseSources && <div className="q-case-choice">
        <p>{caseName ? "The saved documents below are ready for your question." : "Use the completed reviews currently saved with this case. An older comparison may use different reviews."}</p>
        {!caseName && <button type="button" className="q-primary" disabled={!canPrepare || busy !== null || !caseContext} onClick={loadCase}>
          {busy === "case" ? "Preparing documents..." : "Use this case's documents"}</button>}
        <button type="button" className="q-secondary" disabled={busy !== null} onClick={() => setBrowseSources(true)}>Choose other documents</button>
      </div>}
      {browseSources && <div className="q-source-browser">
      <label htmlFor="question-case-select">Saved case</label>
      <select id="question-case-select" value={caseId} disabled={busy !== null} onChange={event => setCaseId(event.target.value)}>
        <option value="">Select a saved case</option>
        {caseId && !cases.some(item => item.id === caseId) && <option value={caseId}>{caseContext?.id === caseId ? caseContext.name : "Linked case"}</option>}
        {cases.map(item => <option key={item.id} value={item.id}>{item.name} · {item.reference} · {item.documentCount} documents</option>)}
      </select>
      {caseListError && <p className="q-muted">Saved cases could not be loaded. <button className="q-secondary" type="button" disabled={busy !== null || caseLoading} onClick={retryCases}>Retry saved cases</button></p>}
      {caseOffset !== null && <button className="q-secondary" disabled={busy !== null || caseLoading} onClick={moreCases}>Load more cases</button>}
      <details className="q-secondary-settings"><summary>Open a case by ID</summary><label htmlFor="question-case">Reconciliation case ID</label><input id="question-case" value={caseId} disabled={busy !== null} onChange={event => setCaseId(event.target.value)} /></details>
      <button type="button" className="q-primary" disabled={!canPrepare || busy !== null || !caseId.trim()} onClick={loadCase}>Use this case&apos;s documents</button>
      <form className="q-source-search" onSubmit={event => { event.preventDefault(); searchReviews(); }}>
        <label htmlFor="review-search">Find a reviewed document</label>
        <p className="q-muted">Search by filename across all completed reviews.</p>
        <input id="review-search" type="search" dir="auto" maxLength={REVIEW_SEARCH_MAX_LENGTH}
          placeholder="For example: invoice October" value={sourceBrowser.searchDraft} disabled={busy !== null}
          onChange={event => sourceBrowser.setSearchDraft(event.target.value)} />
        <div className="q-source-actions"><button type="submit" className="q-secondary" disabled={busy !== null || sourceLoading}>Search reviews</button>
          <button type="button" className="q-secondary" disabled={busy !== null || sourceLoading} onClick={() => { searchReviews(true); }}>Clear search</button></div>
      </form>
      <label htmlFor="question-source">Completed review</label>
      {sourceError && <div role="alert"><p>Completed reviews could not be loaded.</p><button onClick={retrySources}>Retry loading reviews</button></div>}
      <select id="question-source" value={chosen} disabled={busy !== null || sourceLoading} onChange={event => setChosen(event.target.value)}>
        <option value="">{sourceLoading ? "Loading completed reviews…" : "Select a completed review"}</option>
        {availableSources.map(source => <option key={source.reviewSessionId} value={source.reviewSessionId}
          disabled={pinned.some(x => x.documentId === source.documentId)}>{source.filename} · review {source.revisionNumber}</option>)}
      </select>
      <button className="q-primary" disabled={!canPrepare || !chosen || sourceLoading || sourceError || busy !== null || pinned.length >= 100} onClick={pinSource}>{busy === "pin" ? "Preparing index…" : "Add source"}</button>
      {!sourceLoading && !sourceError && availableSources.length === 0 && <p className="q-muted">No completed reviews found. Try another filename, or complete the document review first.</p>}
      <div className="q-source-actions" aria-label="Review pages">
        <button type="button" className="q-secondary" disabled={busy !== null || sourceLoading || sourceBrowser.sourceOffset === 0}
          onClick={() => { pageReviews(false); }}>Previous reviews</button>
        <span role="status">{sourceLoading ? "Loading reviews..." : sourceError ? "Reviews unavailable" : `${sourceBrowser.sources.length} ${sourceBrowser.sources.length === 1 ? "review" : "reviews"} on this page`}</span>
        <button type="button" className="q-secondary" disabled={busy !== null || sourceLoading || sourceError || nextOffset === null}
          onClick={() => { pageReviews(true); }}>Next reviews</button>
      </div>
      </div>}
      <ul className="q-pinned">{pinned.map(source => <li key={source.indexId}><div><strong>{source.filename}</strong>
        <small>Review {source.revisionNumber}</small></div>
        <button aria-label={`Remove ${source.filename}`} disabled={busy !== null} onClick={() => removeSource(source.indexId)}>×</button></li>)}</ul>
      {savedCorpus && <div className="q-saved-selection"><p>{savedCorpus.indexIds.length} source{savedCorpus.indexIds.length === 1 ? "" : "s"} from your saved result. Their original index versions will be used.</p><button disabled={busy !== null} onClick={clearSavedSources}>Clear saved sources</button></div>}
    </section>
    <section className="question-draft" aria-labelledby="question-title">
      <h2 id="question-title">2. Ask your question</h2>
      <form onSubmit={ask}><label htmlFor="question-mode">Question mode</label>
        <select id="question-mode" value={mode} disabled={busy !== null} onChange={event => changeMode(event.target.value)}>
          <option value="reviewed">Answer from reviewed fields</option><option value="evidence">Search evidence</option>
        </select>
        {mode === "evidence" && <><p className="q-muted">Keyword search finds matching source text. Check each hit against your question.</p><details className="q-secondary-settings"><summary>Experimental search settings</summary><label htmlFor="retrieval-mode">Search method</label>
          <select id="retrieval-mode" value={retrievalMode} disabled={busy !== null} onChange={event => changeRetrievalMode(event.target.value)}>
            <option value="lexical">Keyword search</option><option value="vector">Vector search</option>
            <option value="hybrid-nonredundant">Hybrid search</option>
          </select><p className="q-muted">Vector and hybrid search reuse prepared searches for the selected reviews. Unavailable searches do not start paid processing.</p></details></>}
        {mode === "reviewed" && <p className="q-muted">Try “When is the invoice due?” or “What is the total difference between the order and invoice?” Select one document per role. Training/onboarding line comparisons are supported.</p>}
        <label htmlFor="question-text">Your question</label><textarea dir="auto" id="question-text" rows={4} maxLength={4000}
        placeholder="What total is stated on the invoice?" value={question} onChange={event => changeQuestion(event.target.value)}/>
        <div className="q-question-meta"><span>{question.length}/4,000</span><span>{sourceIds.length} source{sourceIds.length === 1 ? "" : "s"} selected</span></div>
        <button className="q-primary" disabled={busy !== null || !question.trim() || !sourceIds.length}>{busy === "search" ? "Checking sources…" : retryAvailable ? "Retry same request" : mode === "reviewed" ? "Answer from selected reviews" : "Search selected sources"}</button>
      </form>

    </section>

    <section className="question-results" aria-labelledby="results-title" aria-busy={busy === "search" || busy === "reload"}>
      <h2 id="results-title" tabIndex={-1} ref={resultHeading}>3. Read the answer and evidence</h2>
      {message && <p className="q-error" role="alert">{message}</p>}
      {!result && <div className="q-empty"><h3>{busy === "search" ? "Checking your selected sources" : "Your answer will appear here"}</h3>
        <p>Choose completed reviews and ask a question. You can then open the saved review and page evidence for each result.</p></div>}
      {result && <><div className="q-run"><h3 dir="auto">{result.question}</h3><p>{result.indexIds.length} frozen source{result.indexIds.length === 1 ? "" : "s"}</p>
        {result.retrieval.replayed && <small>Loaded from saved history</small>}
        <p>This result uses its saved sources, independently of the current question selection.</p><a className="app-button" href={resultUrl(result.retrieval.id)}>Reopen this result</a>
        <button className="q-secondary" disabled={busy !== null || result.indexIds.length === 0} onClick={useSavedSources}>Use saved sources</button>
        <details><summary>Saved source versions</summary><code>Retrieval ID: {result.retrieval.id}</code><p>{result.retrieval.policyVersion}</p><ul>{result.indexIds.map(id => <li key={id}><code>{id}</code></li>)}</ul></details></div>
        {result.reviewedAnswer ? <div className="q-reviewed-answer" aria-label="Reviewed answer">
          <h3>{result.reviewedAnswer.status === "Answered" ? "Answer from reviewed fields" : result.reviewedAnswer.status === "PartiallyAnswered" ? "Partial answer" : "No supported answer"}</h3>
          {result.reviewedAnswer.reasonCode && <p className="q-muted">{result.reviewedAnswer.reasonCode === "unsupported_scope" ?
            "This question is outside the supported reviewed-field questions. Try a single field or amount comparison, or use evidence search." :
            "The selected reviews do not contain enough unambiguous information to answer fully."}</p>}
          {result.reviewedAnswer.claims.map((claim, n) => <article className="q-hit" key={n}><p dir="auto">{claim.text}</p>
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
          <div className="q-hit-heading"><span className="q-rank">{hit.rank.toString().padStart(2, "0")}</span><div><h3 dir="auto">{hit.filename}</h3>
            <small>{hit.kind === "ReviewedFact" ? "Reviewed fact" : "Original page text"}</small></div></div>
          <pre dir="auto"><BidiText text={hit.content} /></pre>{hit.kind === "ReviewedFact" && <p className="q-muted">Reviewed values can differ from the original page evidence.</p>}
          <div className="q-evidence-buttons">{hit.spans.map((span, n) => <button className="q-secondary" disabled={busy !== null} key={span.id}
            onClick={() => openEvidence(hit, span.id)}>Open page {span.pageNumber}{hit.spans.length > 1 ? ` · span ${n + 1}` : ""}</button>)}
            {hit.kind === "ReviewedFact" && <button className="q-secondary" disabled={busy !== null} onClick={() => openEvidence(hit)}>Open review provenance</button>}</div>
        </article>)}
      </>}
    </section>
    <div className="question-history">
      <section className="q-recent" aria-label="Your saved results"><h2>Your saved results</h2>
        <p className="q-muted">Your latest available questions, with their original sources.</p>
        {recentError ? <p>Saved results could not be loaded. <button className="q-secondary" disabled={busy !== null} onClick={retryRecent}>Retry saved results</button></p> : recent.length === 0 ? <p className="q-muted">No saved results are available to this account yet.</p> :
          <ul>{recent.map(item => <li key={item.id}><button className="q-secondary" dir="auto" disabled={busy !== null} onClick={() => openSaved(item.id)}>{item.question}</button><small>{item.mode === "reviewed" ? "Reviewed answer" : "Evidence search"} · {item.sourceCount} sources · {new Date(item.createdAt).toLocaleDateString()}</small></li>)}</ul>}
      </section>
      <details className="q-reload"><summary>Reopen a saved result</summary><form onSubmit={loadSaved}><label htmlFor="saved-question">Retrieval ID</label>
        <p>Only saved results available to this account can be reopened. Keep the result link to return later.</p>
        <input id="saved-question" value={savedId} onChange={event => changeSavedId(event.target.value)} placeholder="Paste a retrieval ID"/>
        <button className="q-secondary" disabled={busy !== null || !savedId.trim()}>Load saved result</button></form></details>
    </div></div>

    {location && <section className="q-source-view" aria-labelledby="evidence-title"><div className="q-source-top"><div>
      <h2 dir="auto" id="evidence-title" tabIndex={-1} ref={evidenceHeading}>{location.filename}</h2></div><button className="q-secondary" onClick={() => setLocation(null)}>Close source</button></div>
      <details><summary>Source version details</summary><p className="q-muted">Pinned review {location.reviewSessionId} · extraction {location.extractionRunId}</p>{location.review && <small>Review action {location.review.actionId}</small>}{location.page && <p>Highlighted UTF-8 bytes {location.page.byteStart}–{location.page.byteEnd}.</p>}</details>
      {location.review && <div className="q-reviewed"><strong>Reviewed fact — {location.review.action}</strong><pre dir="auto"><BidiText text={location.reviewedFact ?? ""} /></pre>
        <p dir="auto">{location.review.reason ?? "No review reason recorded."}</p>
        {!location.page && <p>Review provenance view. No page quotation is being asserted here.</p>}</div>}
      {location.page && <><h3>Page {location.page.pageNumber} · canonical extracted text</h3>
        <p className="q-muted">This is extracted text, not a PDF image.{location.review && " The highlight is original evidence, not a quotation of the corrected value."}</p>
        <pre dir="auto" className="q-canonical"><BidiText text={location.page.before + location.page.quote + location.page.after}
          highlightStart={location.page.before.length} highlightEnd={location.page.before.length + location.page.quote.length} /></pre></>}
    </section>}
  </main>;
}
