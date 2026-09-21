using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class ReconciliationRepository(AppDbContext db) : IReconciliationRepository
{
    public async Task AddAsync(ReconciliationCase reconciliationCase,
        CancellationToken cancellationToken) =>
        await db.ReconciliationCases.AddAsync(reconciliationCase, cancellationToken);

    public Task<ReconciliationCase?> GetAsync(Guid organizationId, Guid reconciliationCaseId,
        CancellationToken cancellationToken) => db.ReconciliationCases.AsSplitQuery()
        .Include(item => item.DocumentLinks)
        .Include(item => item.Runs).ThenInclude(run => run.DocumentSnapshots)
            .ThenInclude(snapshot => snapshot.Fields)
        .Include(item => item.Runs).ThenInclude(run => run.Findings)
            .ThenInclude(finding => finding.Resolutions)
        .SingleOrDefaultAsync(item => item.OrganizationId == organizationId &&
            item.Id == reconciliationCaseId, cancellationToken);

    public async Task<ReconciliationSaveOutcome> SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return ReconciliationSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ReconciliationSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return ReconciliationSaveOutcome.ConstraintConflict;
        }
    }
}
