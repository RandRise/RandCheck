using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

internal static class CaseMetadata
{
    public static ReconciliationWorkflowKind Workflow(string? value) => value switch
    {
        null or "Legacy" => ReconciliationWorkflowKind.Legacy,
        "InvoicePurchaseOrder" => ReconciliationWorkflowKind.InvoicePurchaseOrder,
        _ => throw new ReconciliationValidationException("invalid_workflow_kind", "Workflow kind must be Legacy or InvoicePurchaseOrder.")
    };

    public static ReconciliationCaseCategory Category(string? value) => value is null
        ? ReconciliationCaseCategory.Uncategorized
        : Enum.TryParse<ReconciliationCaseCategory>(value, out var parsed) && Enum.IsDefined(parsed) && parsed.ToString() == value
            ? parsed : throw new ReconciliationValidationException("invalid_case_category",
                "Category must be Uncategorized, OfficeSupplies, Services, or Equipment.");
}
