import type { ReactNode } from "react";
import { Check } from "lucide-react";

/** Every phase stays visible; only the current phase contains working controls. */
export function WorkflowPhase({ number, title, description, summary, active, disabled, completed = false,
  completedLabel = "Step saved", pendingLabel = "Next step",
  action, onOpen, onDetails, children }: {
  number: number; title: string; description: string; summary: string;
  active: boolean; disabled: boolean; action?: string; onOpen?: () => void;
  completed?: boolean; completedLabel?: string; pendingLabel?: string;
  onDetails?: () => void; children?: ReactNode;
}) {
  const state = active ? "current" : completed ? "saved" : "upcoming";
  return <li className="workflow-phase" data-progress={state} aria-current={active ? "step" : undefined}>
    <div className="workflow-phase-heading">
      <div>
        <h2 tabIndex={-1} className="workflow-phase-title" aria-label={`${number}. ${title}`}>
          <span className="workflow-phase-number" aria-hidden="true">{number}</span>
          <span>{title}</span>
        </h2>
        <span className="workflow-phase-state">
          {state === "saved" && <Check size={15} strokeWidth={2} aria-hidden="true" focusable="false" />}
          {active ? "Current step" : completed ? completedLabel : pendingLabel}
        </span>
        <p>{description}</p>
        <p className="workflow-phase-status">{active ? "You are here. " : ""}{summary}</p>
      </div>
      <div className="workflow-phase-actions">
        {!active && onOpen && <button className="secondary" disabled={disabled} onClick={onOpen}>{action}</button>}
        {onDetails && <button className="secondary" disabled={disabled} onClick={onDetails}
          aria-label={`${title} details`}>Details</button>}
      </div>
    </div>
    {active && <div className="workflow-phase-work">{children}</div>}
  </li>;
}
