import Link from "next/link";
import { isQuotationDocument } from "../../document-types";
import type { InvoiceWorkflowState } from "../hooks/use-invoice-workflow";
import { labels } from "../workflow.constants";
import type { Case, Role } from "../workflow.types";
import { ReviewChoiceConfirmation } from "./review-choice-confirmation";

type Props = Pick<InvoiceWorkflowState,
  "documents" | "selections" | "choices" | "mutable" | "busy" | "reloadNeeded" |
  "disabled" | "save" | "chooseRevision" | "pin" | "selectDocument" | "cancelReviewChoice"
> & { item: Case; role: Role; canOperate: boolean };

export function InvoiceDocumentCard({
  item, role, canOperate, documents, selections, choices, mutable, busy,
  reloadNeeded, disabled, save, chooseRevision, pin, selectDocument, cancelReviewChoice,
}: Props) {
  const draft = item.draftDocuments?.find(source => source.role === role);
  const doc = documents.find(source => source.id === draft?.documentId);
  const pinned = item.documents.find(source => source.role === role);
  const choice = choices[role];
  const availableDocuments = documents.filter(source => !isQuotationDocument(source.documentType));
  const savedQuotation = doc && isQuotationDocument(doc.documentType);
  const changed = (selections[role] || "") !== (draft?.documentId ?? "");

  return (
    <article>
      <h3>{labels[role]}</h3>

      {draft ? (
        <>
          <p>Saved document: <bdi>{doc?.originalFilename ?? draft.documentId}</bdi></p>
          {doc && (
            <p>Processing: {doc.latestExtractionStatus ?? doc.status} · Review: {doc.reviewState ?? "Check in document workspace"}</p>
          )}
          <Link href={`/?document=${draft.documentId}&pairCase=${item.id}&pairRole=${role}`}>
            Open {labels[role].toLowerCase()} review
          </Link>
        </>
      ) : <p>No {labels[role].toLowerCase()} selected.</p>}

      {savedQuotation && <p>This saved document is a quotation. It stays available for review, but this check needs an invoice and a purchase order. Choose a replacement before comparing.</p>}

      {pinned && <p>Saved reviewed revision: <bdi>{pinned.reviewSessionId}</bdi></p>}

      {mutable && (
        <>
          <label>
            {labels[role]} document
            <select
              disabled={busy || reloadNeeded}
              value={selections[role] ?? ""}
              onChange={event => selectDocument(role, event.target.value)}
            >
              <option value="">No document</option>
              {draft && !doc && <option value={draft.documentId}>{draft.documentId}</option>}
              {savedQuotation && <option value={doc.id} disabled>{doc.originalFilename} · Saved earlier; choose an invoice or purchase order</option>}
              {availableDocuments.map(source => (
                <option key={source.id} value={source.id}>{source.originalFilename} · {source.status}</option>
              ))}
            </select>
          </label>

          <button disabled={disabled || !changed} onClick={() => void save(role)}>
            Save {labels[role].toLowerCase()} selection
          </button>
          {changed && <p>Unsaved selection. Save it before reviewing or comparing this pair.</p>}
          {canOperate && <Link href={`/?pairCase=${item.id}&pairRole=${role}`}>Add {labels[role].toLowerCase()}</Link>}

          {draft && (
            <button className="secondary" disabled={disabled || changed} onClick={() => void chooseRevision(role, draft.documentId)}>
              Choose {labels[role].toLowerCase()} reviewed revision
            </button>
          )}

          {choice && (
            <ReviewChoiceConfirmation
              role={role}
              choice={choice}
              filename={doc?.originalFilename}
              changed={changed}
              busy={busy}
              disabled={disabled}
              pin={pin}
              cancelReviewChoice={cancelReviewChoice}
            />
          )}
        </>
      )}
    </article>
  );
}
