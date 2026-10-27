namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class ReconciliationNotFoundException()
    : ReconciliationWorkflowException("reconciliation_not_found",
        "The reconciliation case or source was not found.");
