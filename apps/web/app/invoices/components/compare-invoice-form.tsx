import type { InvoiceWorkflowState } from "../hooks/use-invoice-workflow";

type Props = Pick<InvoiceWorkflowState,
  "pairReady" | "newComparison" | "disabled" | "busy" | "scope" | "selectionsDirty" |
  "compare" | "setScope" | "startComparison" | "cancelComparison"
> & { hasRuns: boolean };

export function CompareInvoiceForm({
  hasRuns, pairReady, newComparison, disabled, busy, scope, selectionsDirty,
  compare, setScope, startComparison, cancelComparison,
}: Props) {
  return (
    <section>
      <h2>Compare the reviewed pair</h2>
      <p>This check supports AED only. Check the currency on both documents. We do not convert money. Earlier saved results stay available.</p>
      <p>{pairReady
        ? "Both selected documents have saved reviewed revisions."
        : "Save both selections and choose a completed reviewed revision for each document first."}</p>

      {hasRuns && !newComparison ? (
        <button className="secondary" disabled={disabled} onClick={startComparison}>
          Start another comparison
        </button>
      ) : (
        <form onSubmit={compare}>
          <label>
            Invoice scope
            <select required disabled={disabled} value={scope} onChange={event => setScope(event.target.value)}>
              <option value="">Choose invoice scope</option>
              <option value="Unknown">Not sure</option>
              <option value="FullOrder">Covers the full purchase order</option>
              <option value="Partial">Covers part of the purchase order</option>
            </select>
          </label>

          <p>Only choose the full order after checking the invoice scope. Partial or uncertain scope limits quantity and total checks.</p>
          <button disabled={disabled || !pairReady || selectionsDirty || !scope}>
            Compare invoice and purchase order
          </button>
          {newComparison && (
            <button className="secondary" type="button" disabled={busy} onClick={cancelComparison}>
              Cancel new comparison
            </button>
          )}
        </form>
      )}
    </section>
  );
}
