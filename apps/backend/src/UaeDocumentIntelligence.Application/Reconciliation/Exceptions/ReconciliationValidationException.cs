namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class ReconciliationValidationException(string code, string message)
    : ReconciliationWorkflowException(code, message);
