import type { InvoiceWorkflowState } from "../hooks/use-invoice-workflow";

type Props = Pick<InvoiceWorkflowState, "name" | "busy" | "setName" | "create">;

export function CreateInvoiceCheckForm({ name, busy, setName, create }: Props) {
  return (
    <section>
      <h2>Start an invoice check</h2>

      <form onSubmit={create}>
        <label>
          Check name
          <input
            dir="auto"
            required
            maxLength={200}
            value={name}
            disabled={busy}
            onChange={event => setName(event.target.value)}
          />
        </label>

        <button disabled={busy}>Create invoice check</button>
      </form>
    </section>
  );
}
