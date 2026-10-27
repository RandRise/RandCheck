"use client";

import Link from "next/link";
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import DocumentWorkspace, { type DocumentWorkspaceState } from "../../document-workspace";
import ReconciliationWorkspace from "../../reconciliation/reconciliation-workspace";
import { isQuotationDocument } from "../../document-types";
import { documentChoiceLabel, documentRoleIssue } from "../document-role";
import { useInvoiceWorkflow } from "../hooks/use-invoice-workflow";
import { labels, roles } from "../workflow.constants";
import type { Role, ReviewChoice } from "../workflow.types";
import { WorkflowPhase } from "./workflow-phase";
import { WorkflowDetails, type WorkflowStep } from "./workflow-details";
import { PairUpload } from "./pair-upload";
import "../journey.css";

type Step = WorkflowStep;
const emptyReview: DocumentWorkspaceState = { review: null, dirty: false, busy: false, unavailable: true };

/** One saved case owns the URL. Step/role changes never imply saved or confirmed values. */
export function InvoiceJourney({ canReview, canOperate, identity }: {
  canReview: boolean; canOperate: boolean; identity: string;
}) {
  const work = useInvoiceWorkflow(canReview, identity);
  const [step, setStep] = useState<Step>(1);
  const [detailsPhase, setDetailsPhase] = useState<Step | null>(null);
  const returnFocus = useRef(false);
  const workflowElement = useRef<HTMLElement>(null);
  const [role, setRole] = useState<Role>("Invoice");
  const [reviewState, setReviewState] = useState(emptyReview);
  const [checkedInvoice, setCheckedInvoice] = useState<ReviewChoice>();
  const [resultsDirty, setResultsDirty] = useState(false);
  const [resultsBusy, setResultsBusy] = useState(false);
  const [reviewEpoch, setReviewEpoch] = useState(0);
  const [uploadBusy, setUploadBusy] = useState(false);
  const [focusField, setFocusField] = useState<{ key: string; occurrence: number; extractionRunId?: string }>();
  const [notice, setNotice] = useState("");
  const [resultsEpoch, setResultsEpoch] = useState(0);
  const initialized = useRef<string | null>(null);
  const location = useRef("");
  const guard = useRef({ dirty: false, busy: false });
  const busy = work.busy || uploadBusy || reviewState.busy || resultsBusy;
  useLayoutEffect(() => {
    guard.current = { dirty: reviewState.dirty || resultsDirty || work.selectionsDirty, busy };
  }, [reviewState.dirty, resultsDirty, work.selectionsDirty, busy]);

  const onReviewState = useCallback((state: DocumentWorkspaceState) => setReviewState(state), []);
  const onResultsDirty = useCallback((dirty: boolean) => setResultsDirty(dirty), []);
  const onResultsBusy = useCallback((value: boolean) => setResultsBusy(value), []);
  const onUploadBusy = useCallback((value: boolean) => setUploadBusy(value), []);

  function mayLeave() {
    if (guard.current.busy) {
      setNotice("Wait for the current save to finish.");
      return false;
    }
    return !guard.current.dirty || window.confirm("Some edits are not saved. Cancel to stay and save them, or OK to discard these edits and leave. Saved decisions will stay.");
  }

  function go(next: Step, nextRole: Role = role, field?: { key: string; occurrence: number; extractionRunId?: string }, saved = false) {
    if (!saved && next === step && nextRole === role && !field) return;
    if (!saved && !mayLeave()) return;
    if (work.selectionsDirty) work.discardSelections();
    setReviewEpoch(value => value + 1);
    setReviewState(emptyReview); setResultsDirty(false);
    setStep(next); setRole(nextRole); setFocusField(field); setNotice("");
    setDetailsPhase(null);
    const url = new URL(window.location.href);
    url.searchParams.delete("view"); url.searchParams.delete("details");
    url.searchParams.set("step", String(next));
    url.searchParams.set("role", nextRole);
    if (next !== 3) url.searchParams.delete("run");
    window.history.pushState(null, "", `${url.pathname}${url.search}`);
    location.current = window.location.href;
  }

  function showDetails(phase: Step | null) {
    if (phase !== null && (guard.current.busy || guard.current.dirty)) {
      setNotice("Save or discard your edits before opening details. Your place in the workflow is kept.");
      return;
    }
    const url = new URL(window.location.href);
    url.searchParams.set("step", String(step));
    url.searchParams.set("role", role);
    if (phase) { url.searchParams.set("view", "details"); url.searchParams.set("details", String(phase)); }
    else { url.searchParams.delete("view"); url.searchParams.delete("details"); returnFocus.current = true; }
    window.history.pushState(null, "", `${url.pathname}${url.search}`);
    location.current = window.location.href;
    setDetailsPhase(phase);
    setNotice("");
  }

  useEffect(() => {
    if (detailsPhase === null && returnFocus.current) {
      returnFocus.current = false;
      workflowElement.current?.querySelector<HTMLElement>('[aria-current="step"] h2')?.focus();
    }
  }, [detailsPhase]);

  useEffect(() => {
    if (!work.item || initialized.current === work.item.id) return;
    initialized.current = work.item.id;
    setCheckedInvoice(undefined);
    const url = new URL(window.location.href);
    const requested = Number(url.searchParams.get("step"));
    const hasPair = roles.every(value => work.item!.draftDocuments?.some(source => source.role === value));
    const next = requested >= 1 && requested <= 3 ? requested as Step
      : !hasPair ? 1 : work.item.runs.length ? 3 : 2;
    const availableStep = next === 3 && !work.item.runs.length ? hasPair ? 2 : 1 : next === 2 && !hasPair ? 1 : next;
    setStep(availableStep);
    url.searchParams.set("step", String(availableStep));
    const requestedDetails = Number(url.searchParams.get("details"));
    setDetailsPhase(url.searchParams.get("view") === "details" && requestedDetails >= 1 && requestedDetails <= 3
      ? requestedDetails as Step : null);
    window.history.replaceState(null, "", `${url.pathname}${url.search}`);
    setRole(url.searchParams.get("role") === "PurchaseOrder" ? "PurchaseOrder" : "Invoice");
    location.current = window.location.href;
  }, [work.item]);

  useEffect(() => {
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (guard.current.dirty || guard.current.busy) { event.preventDefault(); event.returnValue = ""; }
    };
    const anchor = (event: MouseEvent) => {
      const target = (event.target as HTMLElement)?.closest?.("a");
      if (!target || target.target === "_blank" || event.ctrlKey || event.metaKey || event.shiftKey) return;
      if (guard.current.busy || guard.current.dirty &&
        !window.confirm("Some edits are not saved. Cancel to stay and save, or OK to discard them and leave.")) {
        event.preventDefault(); event.stopPropagation();
      }
    };
    const restore = (event: PopStateEvent) => {
      if (guard.current.dirty && new URL(window.location.href).searchParams.get("view") === "details") {
        event.stopImmediatePropagation();
        if (location.current) window.history.pushState(null, "", location.current);
        setNotice("Save or discard your edits before opening details. Your place in the workflow is kept.");
        return;
      }
      if (guard.current.busy || guard.current.dirty &&
        !window.confirm("Leave without saving these edits? Saved decisions will stay.")) {
        event.stopImmediatePropagation();
        if (location.current) window.history.pushState(null, "", location.current);
        return;
      }
      const url = new URL(window.location.href);
      const previous = new URL(location.current || window.location.href);
      const nextDetails = Number(url.searchParams.get("details"));
      const details = url.searchParams.get("view") === "details" && nextDetails >= 1 && nextDetails <= 3 ? nextDetails as Step : null;
      setDetailsPhase(details);
      // A details round trip is read-only. Keep the exact inspected reviews and mounted work.
      const samePosition = ["case", "step"].every(key => url.searchParams.get(key) === previous.searchParams.get(key)) &&
        (url.searchParams.get("role") ?? "Invoice") === (previous.searchParams.get("role") ?? "Invoice");
      if (!samePosition) {
        const next = Number(url.searchParams.get("step"));
        setStep(next >= 1 && next <= 3 ? next as Step : 1);
        setRole(url.searchParams.get("role") === "PurchaseOrder" ? "PurchaseOrder" : "Invoice");
        setReviewEpoch(value => value + 1);
        setReviewState(emptyReview); setResultsDirty(false);
      }
      if (!details && previous.searchParams.get("view") === "details") returnFocus.current = true;
      location.current = window.location.href;
    };
    window.addEventListener("beforeunload", beforeUnload);
    document.addEventListener("click", anchor, true);
    window.addEventListener("popstate", restore, true);
    return () => {
      window.removeEventListener("beforeunload", beforeUnload);
      document.removeEventListener("click", anchor, true);
      window.removeEventListener("popstate", restore, true);
    };
  }, []);

  if (work.loading) return <main role="status">Opening your saved check…</main>;
  if (work.error || !work.item) return <main><h1>Invoice check</h1>
    <p role="alert">{work.error || "Choose a saved check or start a new one from Home."}</p>
    {work.error && <button onClick={work.retryOpen}>Try opening again</button>}
    <Link href="/">Back to Home</Link></main>;

  const item = work.item;
  const hasPair = roles.every(value => item.draftDocuments?.some(source => source.role === value));
  const documentId = item.draftDocuments?.find(source => source.role === role)?.documentId;
  const roleIssues = Object.fromEntries(roles.map(value => {
    const source = item.draftDocuments?.find(draft => draft.role === value);
    return [value, documentRoleIssue(work.documents.find(doc => doc.id === source?.documentId)?.documentType, value)];
  }));
  const hasRoleIssue = roles.some(value => !!roleIssues[value]);
  // These badges describe persisted setup, never a match or confirmation of unchecked values.
  // A pending selection or uncertain save must not look finished.
  const filesSaved = hasPair && !hasRoleIssue && !work.selectionsDirty && !work.reloadNeeded && !work.documentError;
  const reviewsSaved = filesSaved && work.pairReady && !reviewState.dirty;
  const completed = !roleIssues[role] && reviewState.review?.documentId === documentId &&
    reviewState.review?.session?.status === "Completed" && !reviewState.unavailable && !reviewState.dirty;
  const inspected = completed ? {
    documentId: documentId!, reviewSessionId: reviewState.review!.session!.id,
    revisionNumber: reviewState.review!.session!.revisionNumber,
  } : undefined;
  const invoicePinned = !!checkedInvoice && item.documents.some(source => source.role === "Invoice" &&
    source.reviewSessionId === checkedInvoice.reviewSessionId && source.documentId === checkedInvoice.documentId &&
    source.documentId === item.draftDocuments?.find(draft => draft.role === "Invoice")?.documentId);

  return <>
    {detailsPhase && <WorkflowDetails phase={detailsPhase} item={item} documents={work.documents}
      role={role} identity={identity} onReturn={() => showDetails(null)}/>}
    <main ref={workflowElement} hidden={detailsPhase !== null} className="invoice-workflow workflow-overview">
    <header className="workflow-heading"><div><h1>Your invoice check</h1>
      <p><bdi>{item.reference}</bdi> - {item.status === "Closed" ? "Closed. Saved history." : "Save your work as you go."}</p></div>
      <p>Follow the steps below. Your whole workflow stays here. Open Details only when you need a closer look.</p>
    </header>
    {(notice || work.message) && <p role="status">{notice || work.message}</p>}
    {work.reloadNeeded && <div role="alert"><p>A save could not be confirmed. Check what reached the server before continuing.</p>
      <button disabled={busy} onClick={async () => { setCheckedInvoice(undefined); await work.reload(); setResultsEpoch(value => value + 1); }}>Check saved progress</button>
    </div>}

    <ol className="workflow-phases" aria-label="Full invoice workflow">
    <WorkflowPhase number={1} title="Add documents" description="Put the invoice and its purchase order together."
      completed={filesSaved} completedLabel="Files saved"
      summary={hasRoleIssue ? "A selected file has the wrong type. Change it below." : hasPair ? "2 files selected for this check." : "Start here. Add the 2 files below."}
      active={step === 1} disabled={busy} action="Change documents" onOpen={() => go(1)}
      onDetails={item.draftDocuments?.length ? () => showDetails(1) : undefined}>
      <div className="pair-inputs">{roles.map(value => {
        const draft = item.draftDocuments?.find(source => source.role === value);
        const other = item.draftDocuments?.find(source => source.role !== value)?.documentId;
        return <section key={value}><h3>{labels[value]}</h3>
          <p>{draft ? `Saved: ${work.documents.find(doc => doc.id === draft.documentId)?.originalFilename ?? draft.documentId}` : "No document selected yet."}</p>
          {roleIssues[value] && <p role="alert" className="warning">{roleIssues[value]}</p>}
          {work.mutable && <>
            {canOperate && <PairUpload role={value} disabled={busy || work.disabled} onBusy={onUploadBusy} onUploaded={async doc => {
              await work.loadDocuments();
              const issue = documentRoleIssue(doc.documentType, value);
              if (issue) throw new Error(issue);
              if (!await work.save(value, doc.id)) throw new Error("Choose the uploaded file from saved documents to retry selection.");
            }} />}
            <details className="saved-file-choice"><summary>Use a saved {labels[value].toLowerCase()} instead</summary>
            <label>Choose a saved {labels[value].toLowerCase()}<select disabled={busy || work.disabled}
              value={work.selections[value] ?? ""} onChange={event => work.selectDocument(value, event.target.value)}>
              <option value="">Choose a document</option>
              {work.documents.filter(doc => doc.id === work.selections[value] ||
                !isQuotationDocument(doc.documentType) && !documentRoleIssue(doc.documentType, value) && doc.id !== other).map(doc =>
                <option key={doc.id} value={doc.id} disabled={!!documentRoleIssue(doc.documentType, value)}>
                  {documentChoiceLabel(doc.originalFilename, doc.documentType, value)} · {doc.status}
                </option>)}
            </select></label>
            <button disabled={busy || work.disabled || !work.selections[value] || work.selections[value] === draft?.documentId}
              onClick={() => void work.save(value)}>Use this {labels[value].toLowerCase()}</button>
            </details>
          </>}
        </section>;
      })}</div>
      {work.documentError && <p role="alert">{work.documentError}</p>}
      {work.documentError && <button disabled={busy} className="secondary" onClick={() => void work.loadDocuments()}>Try loading saved documents again</button>}
      <div className="journey-actions"><button className="app-button-primary" disabled={!hasPair || hasRoleIssue || busy || work.selectionsDirty || work.reloadNeeded} onClick={() => go(2, "Invoice")}>Continue to check values</button></div>
    </WorkflowPhase>

    <WorkflowPhase number={2} title="Check details" description="Check each value against the original file."
      completed={reviewsSaved} completedLabel="Reviews saved"
      summary={hasRoleIssue ? "First, choose the correct files in Add documents." : work.pairReady ? "Reviewed versions saved. Missing facts can still limit the checks." : hasPair ? "Your files are selected. Check the invoice, then the purchase order." : "Next: check the values after adding both files."}
      active={step === 2} disabled={busy || !hasPair} action="Continue checking" onOpen={hasPair && step === 3 ? () => go(2, "Invoice") : undefined}
      onDetails={work.pairReady ? () => showDetails(2) : undefined}>
      <div className="journey-actions">{roles.map(value => <button key={value} className="secondary"
        aria-pressed={role === value} disabled={busy} onClick={() => go(2, value)}>{labels[value]}</button>)}</div>
      <h3>{labels[role]}</h3>
      {roleIssues[role] && <p role="alert" className="warning">{roleIssues[role]} Use Change documents in phase 1 to fix the selection.</p>}
      {/* A later type receipt must not unmount an open review or discard its unsaved edits. */}
      {documentId ? <DocumentWorkspace key={`${role}:${documentId}:${reviewEpoch}`} embedded readOnly={!canReview || item.status === "Closed"} documentId={documentId} onState={onReviewState} focusField={focusField} />
        : <p>Add this document in step 1 first.</p>}
      <div className="journey-actions">
        {canReview && item.status !== "Closed" && <button className="app-button-primary" disabled={busy || work.disabled || !inspected || role === "PurchaseOrder" && !invoicePinned}
          onClick={async () => {
            if (!inspected) return;
            if (await work.pin(role, inspected, role === "PurchaseOrder")) {
              if (role === "Invoice") setCheckedInvoice(inspected);
              setResultsEpoch(value => value + 1);
              go(role === "Invoice" ? 2 : 3, "PurchaseOrder", undefined, true);
            }
          }}>{role === "Invoice" ? "Continue to purchase order" : "Show results"}</button>}
      </div>
      {!completed && <p>Finish and save this document’s review before continuing. No value is confirmed by moving to another step.</p>}
      {role === "PurchaseOrder" && !invoicePinned && <p>Check the invoice and choose Continue to purchase order first.</p>}
    </WorkflowPhase>

    <WorkflowPhase number={3} title="See results" description="Read the differences, check the evidence and record your decision."
      pendingLabel={item.runs.length ? "Saved results available" : "Next step"}
      summary={item.runs.length ? "Results saved. Read the gaps before deciding what they mean." : "Last: results appear after you finish both reviews and choose Show results."}
      active={step === 3} disabled={busy} action="View results" onOpen={item.runs.length ? () => go(3) : undefined}
      onDetails={item.runs.length ? () => showDetails(3) : undefined}>
      <p>These are saved results. Later document edits do not update them. Check both documents again to create updated results.</p>
      <ReconciliationWorkspace key={`${item.id}:${resultsEpoch}`} embedded fixedCaseId={item.id}
        canReview={canReview} creationScope={identity} onDirty={onResultsDirty} onBusy={onResultsBusy} onSaved={work.acceptResultSave}
        onLocationChange={() => { location.current = window.location.href; }}
        onFix={(value, key, occurrence, sourceDocumentId, extractionRunId) => {
          const targetRole = value === "PurchaseOrder" ? "PurchaseOrder" : "Invoice";
          if (!sourceDocumentId || item.draftDocuments?.find(source => source.role === targetRole)?.documentId !== sourceDocumentId) {
            setNotice("This result uses an earlier document. Its evidence is shown here. Open the current documents from step 2 to make a new comparison.");
            return;
          }
          go(2, targetRole, key ? { key, occurrence: occurrence ?? 0, extractionRunId } : undefined);
        }} />
    </WorkflowPhase>
    </ol>
    <p className="workflow-save-note">Saved decisions stay. Save individual edits before leaving. Use Home in the side menu to return later.</p>
  </main></>;
}
