import { CaseRun, Finding, ResultCheck } from "./results.types";

const fieldNames: Record<string, string> = {
  documents: "Invoice and purchase order", source_coverage: "All item rows available",
  order_scope: "What this comparison covers", purchase_order_number: "Purchase order number",
  invoice_number: "Invoice number", purchase_order_reference: "Order number on the invoice",
  supplier_trn: "Supplier tax number", currency: "Currency", total: "Document total", vat: "VAT",
  line_identity: "Item match", line_quantity: "Item quantity", line_unit_price: "Item price",
  line_total: "Item total", line_tax: "Item VAT",
};

export const checkTitle = (check: Pick<ResultCheck, "fieldKey">) => fieldNames[check.fieldKey] ?? check.fieldKey.replaceAll("_", " ");
export const isSimulation = (run: CaseRun) => run.ruleSetVersion.startsWith("synthetic") || run.findings.some(f => f.ruleCode.startsWith("Preview."));

/** Count saved checks, not findings or decisions. Unknown/legacy coverage cannot imply a pass. */
export function resultSummary(run: CaseRun) {
  const checks = run.checks ?? [];
  const passed = checks.filter(c => c.status === "NoDifference").length;
  const differed = checks.filter(c => c.status === "DifferenceFound").length;
  const incomplete = checks.length - passed - differed;
  const recorded = checks.length > 0 && !isSimulation(run);
  const unmatched = run.findings.filter(f => !checks.some(c => c.findingId === f.id));
  const extraFindings = unmatched.length > 0;
  const knownOlderDifference = unmatched.some(f => f.difference != null && f.tolerance != null && Math.abs(f.difference) > f.tolerance);
  const allPassed = recorded && run.status === "Completed" && incomplete === 0 && differed === 0 && !extraFindings &&
    run.coverage?.status === "Complete";
  return { recorded, passed, differed, incomplete, total: checks.length,
    outcome: differed > 0 || knownOlderDifference ? "Differences found" : allPassed ? "All required checks passed" : "Checks incomplete" };
}

const missing: Record<string, string> = {
  required_pair_missing: "Both documents need a saved review before they can be compared.",
  duplicate_source: "The same file was used twice. An invoice and a separate purchase order are needed.",
  source_rows_unverified: "We could not confirm that all item rows were read. Check the original document for missing rows.",
  line_set_unverified: "We could not confirm that all item rows were read, so we could not safely match them.",
  different_or_unverified_quantities: "The item quantities differ or could not be checked. These totals cannot be compared fairly.",
  full_order_scope_unavailable: "This older comparison did not establish which part of the order the invoice covers.",
  currency_unavailable: "The currencies are missing, unchecked or different. A shared currency is needed for this check.",
  tax_currency_unavailable: "The VAT currency is missing, unchecked or does not agree with the document currency.",
  unit_unavailable: "The units are missing, unchecked or different, such as boxes and individual items.",
  price_basis_unavailable: "We could not confirm what one price covers, such as one item or one box.",
  amount_basis_unavailable: "We could not confirm whether both amounts include or exclude tax in the same way.",
  checked_numeric_value_unavailable: "A number is missing, unchecked or cannot be read as a valid number with supporting text.",
  document_identity_unavailable: "The document number needs a checked value and supporting text from the document.",
  checked_identity_unavailable: "A value is missing, unchecked or lacks supporting text from the document.",
  line_identity_unavailable: "An item needs a checked code or description before it can be matched.",
  unknown_line_identity: "An unclear item could hide a repeated item. Item matching needs clearer details.",
  duplicate_line_identity: "This item code or description appears more than once. We cannot safely choose a matching row.",
  line_match_unavailable: "We could not find one clear matching item in the other document.",
};

export function checkExplanation(check: ResultCheck) {
  if (check.status === "CouldNotCheck") return missing[check.reasonCode ?? ""] ?? "This check could not be completed. Review the saved explanation below.";
  if (check.status !== "NoDifference" && check.status !== "DifferenceFound") return "This check has no recognized outcome. Read its saved explanation before deciding what it means.";
  if (check.reasonCode === "document_pair_only") return "Only these two documents are compared. This does not tell us if the order is fully billed or what remains to pay.";
  if (check.reasonCode === "source_rows_verified") return "Both documents have a complete set of item rows available for checking.";
  if (check.reasonCode === "document_identity_checked") return "The document number has been checked against its supporting text.";
  if (check.reasonCode === "full_order_declared") return "For this older comparison, the reviewer said the invoice covered the whole order.";
  if (check.fieldKey === "supplier_trn") return check.status === "NoDifference"
    ? "The tax numbers agree. This alone does not prove who the supplier is."
    : "The tax numbers differ. Confirm which supplier each document belongs to.";
  if (check.fieldKey === "line_quantity") return check.status === "DifferenceFound"
    ? "The quantities differ. Fewer invoiced items do not by themselves mean an error or show what remains on the order."
    : "The quantities agree within the allowed difference.";
  return check.status === "NoDifference" ? "The checked values agree within the allowed difference." : "The checked values differ beyond the allowed difference.";
}

/** Reasons are optional suggestions, never selected automatically or used to reclassify a check. */
export function decisionReasons(finding: Finding, check?: ResultCheck): readonly string[] {
  if (check && check.status !== "NoDifference" && check.status !== "DifferenceFound" || finding.ruleCode.includes("Unavailable"))
    return ["Requested the missing information", "Asked for a clearer document", "Recorded the limit; this check is still incomplete"];
  if (finding.fieldKey === "line_quantity")
    return ["Supplier confirmed a partial invoice", "Asked the supplier to explain the quantity", "Requested a corrected quantity"];
  if (["supplier_trn", "currency", "purchase_order_reference", "invoice_number", "purchase_order_number"].includes(finding.fieldKey ?? ""))
    return ["Requested the correct document", "Asked the supplier to confirm these details", "Checked the difference with the supplier"];
  return ["Agreed price confirmed with the supplier", "Requested a corrected amount", "Asked the supplier to explain the difference"];
}

/** Navigate using the saved source, never the case's newer pinned document. */
export function checkNextAction(run: CaseRun, check: ResultCheck) {
  const left = run.fields.find(f => f.id === check.leftFieldSnapshotId);
  const right = run.fields.find(f => f.id === check.rightFieldSnapshotId);
  const role = check.fieldKey === "purchase_order_number" || (!left && !!right && check.code !== "PAIR-ID") ? "PurchaseOrder" : "Invoice";
  const field = role === "Invoice" ? right : left;
  const source = run.sources?.find(s => s.id === field?.documentSnapshotId) ?? run.sources?.find(s => s.role === role);
  const keys: Record<string, string> = {
    unit_unavailable: "line_unit", price_basis_unavailable: "line_price_basis",
    different_or_unverified_quantities: "line_quantity", currency_unavailable: "currency", tax_currency_unavailable: "tax_currency",
  };
  const broad = ["documents", "source_coverage", "line_identity", "order_scope"].includes(check.fieldKey) ||
    check.reasonCode === "amount_basis_unavailable" || check.reasonCode === "different_or_unverified_quantities";
  const key = broad ? undefined : keys[check.reasonCode ?? ""] ?? field?.key ?? check.fieldKey;
  return { role, source, key,
    occurrence: key === "currency" || key === "tax_currency" ? 0 : field?.occurrence ?? check.occurrence ?? 0,
    label: role === "Invoice" ? "Review invoice details" : "Review purchase order details" };
}
