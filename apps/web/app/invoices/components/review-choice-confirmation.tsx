import type { InvoiceWorkflowState } from "../hooks/use-invoice-workflow";
import { labels } from "../workflow.constants";
import type { ReviewChoice, Role } from "../workflow.types";

type Props = Pick<InvoiceWorkflowState, "busy" | "disabled" | "pin" | "cancelReviewChoice"> & {
  role: Role;
  choice: ReviewChoice;
  filename?: string;
  changed: boolean;
};

export function ReviewChoiceConfirmation({
  role, choice, filename, changed, busy, disabled, pin, cancelReviewChoice,
}: Props) {
  return (
    <div>
      <p>
        Use completed review {choice.revisionNumber ?? ""}: <bdi>{choice.reviewSessionId}</bdi> for <bdi>{filename ?? choice.documentId}</bdi>?
      </p>
      <p>This selects saved reviewed values; it does not confirm any unchecked field.</p>

      <button disabled={disabled || changed} onClick={() => void pin(role)}>
        Use {labels[role].toLowerCase()} reviewed revision
      </button>
      <button className="secondary" disabled={busy} onClick={() => cancelReviewChoice(role)}>
        Cancel review choice
      </button>
    </div>
  );
}
