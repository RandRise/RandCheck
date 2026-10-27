import { BidiText } from "../bidi-text";
import { fieldIdentity, labels } from "../review/review-fields";
import type { ReviewWorkspace } from "../review/review.types";

type Props = {
  review: ReviewWorkspace;
  hasHistory: boolean;
  historyDownload?: { url: string; filename: string };
  onExportHistory: () => void;
};

/** Reading a saved file never confirms extracted values or starts a review. */
export function LibraryReview({ review, hasHistory, historyDownload, onExportHistory }: Props) {
  return <section className="library-saved-review" aria-label="Saved file values">
    <p className="library-review-status">
      <strong>{review.session?.status === "Completed" ? "Review completed" : review.session ? "Review in progress" : "Not reviewed yet"}</strong>
      <span>Read only. Use Home to continue an invoice check.</span>
    </p>
    {review.extractionRunId !== review.selectedExtractionRunId &&
      <p className="warning">These values belong to an earlier extraction. The saved review has been kept.</p>}
    <details className="library-disclosure" open>
      <summary>Saved values and evidence</summary>
      <p>Unchecked values may be wrong. Reading them here does not confirm them.</p>
      {review.fields.length === 0 && <p>No values are available for this file.</p>}
      <div className="library-values">{review.fields.map(field => {
        const unchecked = ["Unreviewed", "Pending"].includes(field.decisionStatus) || field.effective.source === "MachinePending";
        const value = field.effective.isPresent ? field.effective.value : field.decisionStatus === "Missing" ? "Not extracted" : "Excluded";

        return <article className="library-value" key={fieldIdentity(field)}>
          <h3>{labels[field.fieldKey] ?? field.fieldKey}
            {field.fieldKey.startsWith("line_") ? ` · Line ${field.occurrenceIndex}` : field.occurrenceIndex > 0 ? ` · Position ${field.occurrenceIndex + 1}` : ""}
          </h3>
          <p dir="auto"><BidiText text={value ?? "Unavailable"} /></p>
          <small>{unchecked ? "Not checked" : field.decisionStatus}</small>
          {field.latestAction?.reason && <p dir="auto">Reason: <BidiText text={field.latestAction.reason} /></p>}
          <details>
            <summary>Source evidence</summary>
            {field.machine && <p dir="auto">Extracted text: <BidiText text={field.machine.value} /></p>}
            {field.machine?.evidence.length ? field.machine.evidence.map((evidence, index) =>
              <div key={index}><p>Page {evidence.page}</p>
                {evidence.quotedText ? <blockquote dir="auto"><BidiText text={evidence.quotedText} /></blockquote>
                  : <p>No text excerpt is saved. Check the original file.</p>}
              </div>) : <p>No source evidence is saved for this value.</p>}
          </details>
        </article>;
      })}</div>
    </details>
    {hasHistory && <details className="library-disclosure">
      <summary>Review history</summary>
      <p>Download the saved review record, including earlier decisions.</p>
      {historyDownload
        ? <a href={historyDownload.url} download={historyDownload.filename}>Save history JSON</a>
        : <button className="secondary" onClick={onExportHistory}>Download review history</button>}
    </details>}
  </section>;
}
