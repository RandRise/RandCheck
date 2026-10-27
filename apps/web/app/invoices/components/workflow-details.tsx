import { useEffect, useRef, useState } from "react";
import DocumentWorkspace from "../../document-workspace";
import ReconciliationWorkspace from "../../reconciliation/reconciliation-workspace";
import { labels, roles } from "../workflow.constants";
import type { Case, Document, Role } from "../workflow.types";

export type WorkflowStep = 1 | 2 | 3;
const titles = { 1: "File details", 2: "Saved review details", 3: "Result details" };

/** Read-only detail pages do not feed current review state or tokens back into the workflow. */
export function WorkflowDetails({ phase, item, documents, role, identity, onReturn }: {
  phase: WorkflowStep; item: Case; documents: Document[]; role: Role;
  identity: string; onReturn: () => void;
}) {
  const [shownRole, setShownRole] = useState(role);
  const heading = useRef<HTMLHeadingElement>(null);
  const documentId = item.draftDocuments?.find(source => source.role === shownRole)?.documentId;

  useEffect(() => { heading.current?.focus(); }, [phase]);

  return <main className="workflow-details">
    <button className="secondary" onClick={onReturn}>Return to workflow</button>
    <header className="workflow-heading">
      <div><h1 ref={heading} tabIndex={-1}>{titles[phase]}</h1>
        <p><bdi>{item.reference}</bdi> · Read only</p></div>
      <p>Your place in the workflow is kept. Reading these details does not confirm values or change results.</p>
    </header>
    {phase === 1 && <section aria-label="Selected files">
      <h2>The files in this check</h2>
      <dl className="workflow-file-details">{roles.map(value => {
        const selected = item.draftDocuments?.find(source => source.role === value);
        const file = documents.find(document => document.id === selected?.documentId);
        return <div key={value}><dt>{labels[value]}</dt>
          <dd><bdi>{file?.originalFilename ?? (selected ? "Saved document" : "No file selected")}</bdi>
            {file && <p>File status: {file.status}. This is not review completion.</p>}
          </dd></div>;
      })}</dl>
      <p>To change a file, return to the workflow and use Add documents. Earlier results keep their original files.</p>
    </section>}
    {phase === 2 && <section>
      <h2>Read the saved values</h2>
      <p>This shows the current saved review for each document. Earlier comparisons keep their own evidence under Result details.</p>
      <div className="journey-actions">{roles.map(value => <button key={value} className="secondary"
        aria-pressed={shownRole === value} onClick={() => setShownRole(value)}>{labels[value]}</button>)}</div>
      {documentId ? <DocumentWorkspace key={documentId} embedded readOnly documentId={documentId}/>
        : <p>No file selected for this side yet.</p>}
    </section>}
    {phase === 3 && <ReconciliationWorkspace embedded syncLocation={false} fixedCaseId={item.id} canReview={false} creationScope={identity}/>}
  </main>;
}
