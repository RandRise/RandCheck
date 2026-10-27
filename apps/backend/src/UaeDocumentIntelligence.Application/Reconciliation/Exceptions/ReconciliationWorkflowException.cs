namespace UaeDocumentIntelligence.Application.Reconciliation;

public abstract class ReconciliationWorkflowException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
