import { useEffect, useRef, useState } from "react";
import { BidiText } from "../bidi-text";
import { correctionReasons, unableToVerifyReason } from "./guided-review.constants";
import { fieldValue, labels } from "./review-fields";
import type { ReviewField } from "./review.types";

type Props = {
  field: ReviewField;
  value: string;
  reason: string;
  disabled: boolean;
  onValue: (value: string) => void;
  onReason: (reason: string) => void;
  onDiscard: () => void;
  onAction: (action: "Confirmed" | "Corrected" | "Cleared", reason?: string) => void;
};

export function GuidedValueCard({ field, value, reason, disabled, onValue, onReason, onDiscard, onAction }: Props) {
  const dirty = value !== fieldValue(field) || !!reason;
  const [mode, setMode] = useState<"read" | "change" | "unsure">(dirty ? "change" : "read");
  const heading = useRef<HTMLHeadingElement>(null);

  useEffect(() => { heading.current?.focus(); }, []);

  const label = labels[field.fieldKey] ?? field.fieldKey;
  const position = field.fieldKey.startsWith("line_") ? ` · Item ${field.occurrenceIndex}`
    : field.occurrenceIndex > 0 ? ` · Position ${field.occurrenceIndex + 1}` : "";

  return <article className="guided-value review-field" aria-label={label}>
    <h3 ref={heading} tabIndex={-1}>{label}{position}</h3>
    <p>Read the source beside this value. Does it say the same thing?</p>
    {field.cues?.length > 0 && <p className="warning">This value needs a closer look. The system could not read it with confidence.</p>}
    <div className="review-field-context">
      <div className="guided-suggested-value">
        <small>Value to check</small>
        <p dir="auto"><BidiText text={fieldValue(field)}/></p>
        <small>{field.latestAction ? "Saved reviewed value" : "Not yet saved as checked"}{dirty ? " · You have an unsaved change" : ""}</small>
      </div>
      <div className="source-evidence">
        <h4>From the document</h4>
        {field.machine?.evidence.length ? field.machine.evidence.map((evidence, index) => <div key={index}>
          <small>Page {evidence.page}</small>
          {evidence.quotedText ? <blockquote dir="auto"><BidiText text={evidence.quotedText}/></blockquote>
            : <p>No text is shown here. Check this page in the original file.</p>}
        </div>) : <p>No source was returned for this value. Check the original file, or choose I can&apos;t tell.</p>}
      </div>
    </div>

    {field.latestAction && <p>This value already has a saved decision. Open All fields to inspect it and your unsaved edit. Use Revise decision there to change it.</p>}

    {!field.latestAction && mode === "read" && <div className="guided-actions">
      <button disabled={disabled || dirty} onClick={() => onAction("Confirmed")}>Keep this value</button>
      <button className="secondary" disabled={disabled} onClick={() => setMode("change")}>Change it</button>
      <button className="secondary" disabled={disabled || dirty} onClick={() => setMode("unsure")}>I can&apos;t tell</button>
    </div>}

    {!field.latestAction && mode === "change" && <div className="guided-edit">
      <label>Correct value<input dir="auto" disabled={disabled} value={value} onChange={event => onValue(event.target.value)}/></label>
      <label>Why are you changing it?<input dir="auto" disabled={disabled} value={reason} onChange={event => onReason(event.target.value)}/></label>
      <div className="guided-actions" aria-label="Suggested reasons">
        {correctionReasons.map(suggestion => <button key={suggestion} className="secondary" disabled={disabled}
          onClick={() => onReason(suggestion)}>{suggestion}</button>)}
      </div>
      <p>Choose a reason above, or write your own. It will be saved in the history.</p>
      <div className="guided-actions">
        <button disabled={disabled || !value.trim() || !reason.trim()} onClick={() => onAction("Corrected")}>Save correction</button>
        <button className="secondary" disabled={disabled} onClick={() => { onDiscard(); setMode("read"); }}>Cancel change</button>
      </div>
    </div>}

    {!field.latestAction && mode === "unsure" && <div className="guided-unsure">
      <p>Leaving this value out means checks that need it may not run. The original value stays in the history.</p>
      <p>We will save this reason: {unableToVerifyReason}</p>
      <div className="guided-actions">
        <button disabled={disabled} onClick={() => onAction("Cleared", unableToVerifyReason)}>Leave this value out</button>
        <button className="secondary" disabled={disabled} onClick={() => setMode("read")}>Cancel</button>
      </div>
    </div>}
  </article>;
}
