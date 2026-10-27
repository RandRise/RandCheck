import type { InvoiceWorkflowState } from "../hooks/use-invoice-workflow";

type Props = Pick<InvoiceWorkflowState,
  "cases" | "nextOffset" | "listLoading" | "listError" | "loadCases" | "navigate"
>;

export function InvoiceCheckList({ cases, nextOffset, listLoading, listError, loadCases, navigate }: Props) {
  return (
    <section>
      <h2>Saved invoice checks</h2>
      <button className="secondary" disabled={listLoading} onClick={() => void loadCases()}>
        Refresh invoice checks
      </button>

      {listError && <p role="alert">{listError}</p>}
      {listLoading && <p role="status">Loading invoice checks…</p>}
      {!listLoading && !listError && cases.length === 0 && <p>No saved invoice checks yet.</p>}

      <ul>
        {cases.map(saved => (
          <li key={saved.id}>
            <a href={`/invoices?case=${saved.id}`} onClick={event => {
              if (event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) {
                event.preventDefault();
                navigate(saved.id);
              }
            }}>
              <bdi>{saved.name}</bdi>
            </a> · <bdi>{saved.reference}</bdi> · {saved.status}
          </li>
        ))}
      </ul>

      {nextOffset !== null && (
        <button disabled={listLoading} onClick={() => void loadCases(nextOffset)}>
          Load more invoice checks
        </button>
      )}
    </section>
  );
}
