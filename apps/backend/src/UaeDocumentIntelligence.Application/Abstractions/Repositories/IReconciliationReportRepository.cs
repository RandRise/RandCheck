using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Abstractions;

public interface IReconciliationReportRepository
{
    /// <summary>Read the selected run, its decisions and case version from one database snapshot.</summary>
    Task<ReconciliationCase?> ReadAsync(Guid organizationId, Guid caseId, Guid runId, Guid expectedVersion,
        CancellationToken cancellationToken);
}
