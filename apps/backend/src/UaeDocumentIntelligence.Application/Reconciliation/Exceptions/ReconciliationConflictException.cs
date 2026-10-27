namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class ReconciliationConflictException(string code, string message)
    : ReconciliationWorkflowException(code, message);
