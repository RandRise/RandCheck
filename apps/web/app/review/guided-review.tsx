import { GuidedValueCard } from "./guided-value-card";
import { guidedReviewQueue } from "./guided-review.selectors";
import { fieldIdentity, fieldValue, labels } from "./review-fields";
import type { ReviewField } from "./review.types";
import "./guided-review.css";

type Props = {
  fields: ReviewField[];
  drafts: Record<string, string>;
  reasons: Record<string, string>;
  disabled: boolean;
  onValue: (key: string, value: string) => void;
  onReason: (key: string, value: string) => void;
  onDiscard: (field: ReviewField) => void;
  onAction: (field: ReviewField, action: "Confirmed" | "Corrected" | "Cleared", reason?: string) => void;
  onShowAll: () => void;
};

export function GuidedReview({ fields, drafts, reasons, disabled, onValue, onReason, onDiscard, onAction, onShowAll }: Props) {
  const queue = guidedReviewQueue(fields, drafts, reasons);
  const field = queue[0];
  const missing = fields.filter(value => value.decisionStatus === "Missing");
  const excluded = fields.filter(value => value.decisionStatus === "Cleared");
  const key = field ? fieldIdentity(field) : "";

  return <section className="guided-review" aria-label="Check one value at a time">
    <p className="guided-count" role="status">{queue.length} {queue.length === 1 ? "value needs" : "values need"} your review.</p>
    {field ? <GuidedValueCard
      key={`${key}:${field.machine?.id}:${field.latestAction?.id ?? "unreviewed"}`}
      field={field}
      value={drafts[key] ?? fieldValue(field)}
      reason={reasons[key] ?? ""}
      disabled={disabled}
      onValue={value => onValue(key, value)}
      onReason={value => onReason(key, value)}
      onDiscard={() => onDiscard(field)}
      onAction={(action, reason) => onAction(field, action, reason)}
    /> : <p>No extracted values are waiting for a decision. Check any gaps below, then use Complete review to save this review as finished. This does not mean the documents match.</p>}

    {(missing.length > 0 || excluded.length > 0) && <div className="guided-gaps">
      <h3>Some values are unavailable</h3>
      <p>{missing.length} missing · {excluded.length} left out. Checks that need these values may not run.</p>
      <ul>{[...missing, ...excluded].map(value => <li key={fieldIdentity(value)}>
        {labels[value.fieldKey] ?? value.fieldKey}{value.fieldKey.startsWith("line_") ? ` · Item ${value.occurrenceIndex}` : ""}
        {value.decisionStatus === "Missing" ? " — not found in the extracted details" : " — left out of checked values"}
      </li>)}</ul>
      <p>Use All fields to inspect these gaps or add a value you can read. Adding a value does not supply missing source evidence.</p>
    </div>}
    <button className="secondary" disabled={disabled} onClick={onShowAll}>Show all fields and saved decisions</button>
  </section>;
}
