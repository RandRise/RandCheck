"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { documentRoleIssue } from "../document-role";
import { useStartInvoiceCheck } from "../hooks/use-start-invoice-check";
import { labels, roles } from "../workflow.constants";
import { PairUpload } from "./pair-upload";
import { WorkflowPhase } from "./workflow-phase";
import "../workflow.css";
import "../journey.css";

export function StartInvoiceCheck({ identity, canReview, canOperate }: {
  identity: string; canReview: boolean; canOperate: boolean;
}) {
  const router = useRouter();
  const work = useStartInvoiceCheck(identity, canReview, id => router.push(`/invoices?case=${id}&step=2`));
  const [uploadBusy, setUploadBusy] = useState(false);
  const uploadLock = useRef(false);
  const busy = work.busy || uploadBusy;
  const disabled = busy || !!work.draft.intent;

  useEffect(() => {
    if (!busy) return;
    const location = window.location.href;
    const unload = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    const anchor = (event: MouseEvent) => {
      const link = (event.target as HTMLElement)?.closest?.("a");
      if (link && link.target !== "_blank" && !event.ctrlKey && !event.metaKey) { event.preventDefault(); event.stopPropagation(); }
    };
    const back = (event: PopStateEvent) => { event.stopImmediatePropagation(); window.history.pushState(null, "", location); };
    window.addEventListener("beforeunload", unload);
    document.addEventListener("click", anchor, true);
    window.addEventListener("popstate", back, true);
    return () => {
      window.removeEventListener("beforeunload", unload);
      document.removeEventListener("click", anchor, true);
      window.removeEventListener("popstate", back, true);
    };
  }, [busy]);

  if (!canReview) return <main><h1>Invoice access required</h1><p>Your account cannot start a new invoice check.</p></main>;

  return <main className="invoice-workflow workflow-overview">
    <header className="page-heading"><h1>Start an invoice check</h1>
      <p>Add the invoice and purchase order, then continue to review their values.</p></header>
    <p>{work.draft.intent ? "These file choices are kept while we prepare your check." : "No invoice check is created until you continue with both files. Uploaded files stay in Document library."}</p>
    {busy && <p role="status">Please wait for the upload or save to finish before leaving.</p>}
    {work.message && <p role="alert">{work.message}</p>}
    <ol className="workflow-phases" aria-label="Full invoice workflow">
      <WorkflowPhase number={1} title="Add documents" description="Choose the invoice and its purchase order."
        summary={work.draft.intent ? "Recover this request before changing the file choices." : work.ready ? "Both files are selected. Continue to save this pair." : "Add both files to continue."} active disabled={busy}>
        {work.loading && <p role="status">Loading saved documents…</p>}
        {work.listError && <div role="alert"><p>{work.listError}</p><button disabled={busy} onClick={() => void work.refresh()}>Try loading saved documents again</button></div>}
        <div className="pair-inputs">{roles.map(role => {
          const selected = work.documents.find(doc => doc.id === work.draft.selections[role]);
          const other = work.draft.selections[role === "Invoice" ? "PurchaseOrder" : "Invoice"];
          const issue = selected && documentRoleIssue(selected.documentType, role);
          return <section key={role}><h3>{labels[role]}</h3>
            <p dir="auto">{selected ? `Selected: ${selected.originalFilename}` : work.draft.selections[role] ? work.draft.intent ? "This file is unavailable. Retry preparation to recover the saved check." : "This selected file is unavailable. Refresh the saved documents or choose another file." : "No document selected yet."}</p>
            {issue && <p role="alert">{issue}</p>}
            {canOperate && <PairUpload role={role} disabled={disabled || work.loading} onUploaded={doc => work.uploaded(role, doc)}
              onBusy={value => { uploadLock.current = value; setUploadBusy(value); }}/>} 
            <details className="saved-file-choice" open={!canOperate}><summary>Use a saved {labels[role].toLowerCase()} instead</summary>
              <label>Choose a saved {labels[role].toLowerCase()}<select value={work.draft.selections[role]} disabled={disabled || work.loading}
                onChange={event => work.select(role, event.target.value)}>
                <option value="">Choose a document</option>
                {work.draft.selections[role] && !selected && <option value={work.draft.selections[role]}>Unavailable selected file</option>}
                {work.documents.filter(doc => doc.id === selected?.id || doc.id !== other && !documentRoleIssue(doc.documentType, role)).map(doc =>
                  <option key={doc.id} value={doc.id} disabled={!!documentRoleIssue(doc.documentType, role)}>{doc.originalFilename} · {doc.status}</option>)}
              </select></label>
            </details>
          </section>;
        })}</div>
        <div className="journey-actions">
          <button className="app-button-primary" disabled={busy || work.loading || !work.ready && !work.draft.intent}
            onClick={() => { if (!uploadLock.current) void work.continueToReview(); }}>{work.busy ? "Preparing your check…" : work.draft.intent ? "Retry preparation" : "Continue to review"}</button>
          {work.draft.intent?.caseId && <button disabled={busy} className="secondary" onClick={work.openSaved}>Open saved check</button>}
        </div>
        <p>Continuing saves the document choices. It does not confirm any values or compare the files.</p>
      </WorkflowPhase>
      <WorkflowPhase number={2} title="Check details" description="Check each value against its source." summary="Available after both files are saved to the check." active={false} disabled />
      <WorkflowPhase number={3} title="See results" description="Read differences and record decisions." summary="Available after you review the documents and run the checks." active={false} disabled />
    </ol>
  </main>;
}
