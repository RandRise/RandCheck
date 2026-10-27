using System.Data;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class ReconciliationReportRepository(AppDbContext db) : IReconciliationReportRepository
{
    public async Task<ReconciliationCase?> ReadAsync(Guid organizationId, Guid caseId, Guid runId, Guid expectedVersion,
        CancellationToken cancellationToken)
    {
        // Split queries avoid multiplying every field/check/decision into a large join.
        // Repeatable Read keeps all of those reads on the same committed snapshot,
        // including the case token when another person records a decision concurrently.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var item = await db.ReconciliationCases.AsNoTracking().AsSplitQuery()
            .Where(value => value.OrganizationId == organizationId && value.Id == caseId)
            .Include(value => value.Runs.Where(run => run.Id == runId)).ThenInclude(run => run.DocumentSnapshots)
                .ThenInclude(source => source.Fields)
            .Include(value => value.Runs.Where(run => run.Id == runId)).ThenInclude(run => run.Findings)
                .ThenInclude(finding => finding.Resolutions)
            .Include(value => value.Runs.Where(run => run.Id == runId)).ThenInclude(run => run.Checks)
            .SingleOrDefaultAsync(cancellationToken);
        if (item is not null && item.ConcurrencyToken != expectedVersion)
            throw new ReconciliationPreconditionException("report_version_changed",
                "The saved results or decisions changed. Refresh results before downloading.");
        await transaction.CommitAsync(cancellationToken);
        return item;
    }
}
