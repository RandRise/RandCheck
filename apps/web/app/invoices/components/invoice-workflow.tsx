import Link from "next/link";
import { useInvoiceWorkflow } from "../hooks/use-invoice-workflow";
import { roles } from "../workflow.constants";
import { CompareInvoiceForm } from "./compare-invoice-form";
import { CreateInvoiceCheckForm } from "./create-invoice-check-form";
import { InvoiceCheckList } from "./invoice-check-list";
import { InvoiceDocumentCard } from "./invoice-document-card";
import { SavedComparisons } from "./saved-comparisons";

type Props = { canReview: boolean; canOperate: boolean; identity: string };

export function InvoiceWorkflow({ canReview, canOperate, identity }: Props) {
  const workflow = useInvoiceWorkflow(canReview, identity);
  const { item, loading, error, busy, mutable, reloadNeeded, documentError, message } = workflow;

  return (
    <main className="invoice-workflow">
      <header className="page-heading">
        <h1>Invoice checks</h1>
        <p>Select an invoice and its purchase order, check their values, then compare saved reviews.</p>
        <Link href="/reconciliation">Existing comparison cases</Link>
      </header>

      {loading && <p role="status">Opening invoice check…</p>}
      {error && (
        <div role="alert">
          <p>{error}</p>
          <button disabled={busy} onClick={workflow.retryOpen}>Retry opening check</button>
          <Link href="/reconciliation">Open existing comparisons</Link>
        </div>
      )}

      {!loading && !item && !error && (
        <>
          {canReview && (
            <CreateInvoiceCheckForm
              name={workflow.name}
              busy={busy}
              setName={workflow.setName}
              create={workflow.create}
            />
          )}
          <InvoiceCheckList
            cases={workflow.cases}
            nextOffset={workflow.nextOffset}
            listLoading={workflow.listLoading}
            listError={workflow.listError}
            loadCases={workflow.loadCases}
            navigate={workflow.navigate}
          />
        </>
      )}

      {item && (
        <>
          <button className="secondary" disabled={busy} onClick={() => workflow.navigate(null)}>
            All invoice checks
          </button>

          <section>
            <h2 dir="auto">{item.name}</h2>
            <p><bdi>{item.reference}</bdi> · {item.status}</p>
            <p>Selections save the pair. Reviewed revisions save the exact values used by the next comparison.</p>
          </section>

          {!mutable && <p>{item.status === "Closed"
            ? "This invoice check is closed. Its saved results are read only."
            : "Read-only access. A reviewer or operator can change the pair."}</p>}
          {reloadNeeded && (
            <p role="alert">Check the saved result before another write. Reloading preserves your unsaved selections and never repeats a save.</p>
          )}
          <button className="secondary" disabled={busy} onClick={() => void workflow.reload()}>
            Reload saved check
          </button>

          {documentError && (
            <div role="alert">
              <p>{documentError}</p>
              <button disabled={busy} onClick={() => void workflow.loadDocuments()}>Retry document list</button>
            </div>
          )}

          <section className="invoice-pair" aria-label="Invoice and purchase order">
            {roles.map(role => (
              <InvoiceDocumentCard
                key={role}
                item={item}
                role={role}
                canOperate={canOperate}
                documents={workflow.documents}
                selections={workflow.selections}
                choices={workflow.choices}
                mutable={mutable}
                busy={busy}
                reloadNeeded={reloadNeeded}
                disabled={workflow.disabled}
                save={workflow.save}
                chooseRevision={workflow.chooseRevision}
                pin={workflow.pin}
                selectDocument={workflow.selectDocument}
                cancelReviewChoice={workflow.cancelReviewChoice}
              />
            ))}
          </section>

          {mutable && (
            <CompareInvoiceForm
              hasRuns={item.runs.length > 0}
              pairReady={workflow.pairReady}
              newComparison={workflow.newComparison}
              disabled={workflow.disabled}
              busy={busy}
              scope={workflow.scope}
              selectionsDirty={workflow.selectionsDirty}
              compare={workflow.compare}
              setScope={workflow.setScope}
              startComparison={workflow.startComparison}
              cancelComparison={workflow.cancelComparison}
            />
          )}
          <SavedComparisons item={item} />
        </>
      )}

      {message && <p role="status" className="message">{message}</p>}
    </main>
  );
}
