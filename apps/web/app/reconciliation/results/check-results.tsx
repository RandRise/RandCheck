import { ReactNode } from "react";
import { BidiText } from "../../bidi-text";
import { CaseRun, Finding, ResultCheck } from "./results.types";
import { checkExplanation, checkNextAction, checkTitle, isSimulation, resultSummary } from "./result-presentation";
import "./results.css";

export function ResultSummary({ run }: { run: CaseRun }) {
  const summary = resultSummary(run);
  return <div className="result-summary" role="status">
    {isSimulation(run) && <p><strong>Sample preview only.</strong> This older simulation does not run the real backend checks.</p>}
    <h3>{summary.outcome}</h3>
    {summary.recorded ? <ul aria-label="Check counts">
      <li><strong>{summary.passed}</strong> Passed</li>
      <li><strong>{summary.differed}</strong> Differed</li>
      <li><strong>{summary.incomplete}</strong> Could not be checked</li>
      <li><strong>{summary.total}</strong> Total checks</li>
    </ul> : <p>Full check counts were not recorded. No complete match can be claimed from this result.</p>}
    <p>These results cover {/^invoice-po-[12]\.0$/.test(run.ruleSetVersion) ? "the saved invoice and purchase order only" : "the saved documents"}. They do not approve payment or establish what remains on the order.</p>
  </div>;
}

type Props = {
  run: CaseRun;
  busy: boolean;
  renderDecision: (finding: Finding) => ReactNode;
  onFix?: (role: string, key?: string, occurrence?: number, documentId?: string, extractionRunId?: string) => void;
};

export function CheckCards({ run, busy, onFix, renderDecision }: Props) {
  function card(check: ResultCheck) {
    const finding = run.findings.find(f => f.id === check.findingId);
    const action = checkNextAction(run, check);
    const status = check.status === "NoDifference" ? "Passed" : check.status === "DifferenceFound" ? "Differed" : "Could not be checked";
    const hasValues = check.leftValue != null || check.rightValue != null || !["documents", "source_coverage", "order_scope"].includes(check.fieldKey);
    const single = check.code === "PAIR-ID";
    return <article className="finding result-check" key={check.id} aria-label={`${checkTitle(check)}: ${status}`}>
      <div className="finding-head"><h3>{checkTitle(check)}{check.occurrence != null && check.occurrence > 0 ? ` · Item row ${check.occurrence}` : ""}</h3><strong>{status}</strong></div>
      <p>{checkExplanation(check)}</p>
      {check.lineIdentity && <p>Item: <BidiText text={check.lineIdentity.replace(/^[CD]:/, "")}/></p>}
      {hasValues && <div className="comparison-values">{(["Invoice", "PurchaseOrder"] as const).filter(role => !single || (check.fieldKey === "invoice_number" ? role === "Invoice" : role === "PurchaseOrder")).map(role => {
        const isInvoice = role === "Invoice";
        const field = run.fields.find(f => f.id === (isInvoice ? check.rightFieldSnapshotId : check.leftFieldSnapshotId));
        const value = isInvoice ? check.rightValue : check.leftValue;
        return <div key={role}><small>{isInvoice ? "Invoice" : "Purchase order"}</small>
          <strong dir="auto">{value ?? "Not available"}</strong>
          <em>{field?.page ? `Page ${field.page}` : "Page not recorded"}</em>
          {field?.quote ? <blockquote dir="auto"><BidiText text={field.quote}/></blockquote> : <p>No supporting text was saved for this value.</p>}
        </div>;
      })}</div>}
      {check.signedDifference != null && <p>Invoice minus purchase order: <strong>{check.signedDifference} {check.currency ?? check.unit}</strong>. Allowed difference: {check.tolerance ?? "Not recorded"}. Do not add this to other differences; the same amount may appear in more than one check.</p>}
      {check.status !== "NoDifference" && <div className="result-next"><p><strong>Next:</strong> {action.label.toLowerCase()}.{check.status !== "DifferenceFound" ? " If information is absent from the document, ask the supplier for it. This check stays incomplete until it can be performed in a new comparison." : " Confirm the difference before recording a decision."}</p>
        {onFix && action.source && <button className="secondary" disabled={busy} onClick={() => onFix(action.role, action.key, action.occurrence, action.source?.documentId, action.source?.extractionRunId)}>{action.label}</button>}
      </div>}
      <details><summary>Saved check explanation</summary><p>{check.explanation}</p></details>
      {finding && renderDecision(finding)}
    </article>;
  }
  const checks = run.checks ?? [];
  const passed = checks.filter(c => c.status === "NoDifference");
  return <div className="finding-list">
    {checks.filter(c => c.status !== "NoDifference").map(card)}
    {passed.length > 0 && <details className="result-passed"><summary>Passed checks ({passed.length})</summary>{passed.map(card)}</details>}
  </div>;
}
