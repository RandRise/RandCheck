namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class ReconciliationPreconditionException(string code, string message)
    : ReconciliationWorkflowException(code, message);
