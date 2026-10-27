using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class ReconciliationRepository(AppDbContext db) : IReconciliationRepository
{
    public async Task<IReadOnlyList<ReconciliationCaseSummary>> ListAsync(Guid organizationId,
        int offset, int limit, CancellationToken cancellationToken, ReconciliationCaseFilter? filter = null)
    {
        var query = db.ReconciliationCases.AsNoTracking().Where(item => item.OrganizationId == organizationId);
        if (!string.IsNullOrEmpty(filter?.Search))
        {
            var search = filter.Search.ToLowerInvariant();
            query = query.Where(item => item.Name.ToLower().Contains(search) || item.Reference.ToLower().Contains(search));
        }
        if (filter?.Category is { } category) query = query.Where(item => item.Category == category);
        if (filter?.Status is { } status) query = query.Where(item => item.Status == status);
        if (filter?.WorkflowKind is { } workflow) query = query.Where(item => item.WorkflowKind == workflow);
        if (filter?.CreatedFrom is { } from) query = query.Where(item => item.CreatedAtUtc >= from);
        if (filter?.CreatedTo is { } to) query = query.Where(item => item.CreatedAtUtc <= to);
        return await query
            .OrderByDescending(item => item.CreatedAtUtc).ThenByDescending(item => item.Id)
            .Skip(offset).Take(limit)
            .Select(item => new ReconciliationCaseSummary(item.Id, item.Name, item.Status.ToString(),
                item.CreatedAtUtc, item.UpdatedAtUtc, item.DocumentLinks.Count, item.Runs.Count,
                item.Reference, item.Category.ToString(), item.WorkflowKind.ToString()))
            .ToListAsync(cancellationToken);
    }

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
        .Include(item => item.Runs).ThenInclude(run => run.Checks)
        .SingleOrDefaultAsync(item => item.OrganizationId == organizationId &&
            item.Id == reconciliationCaseId, cancellationToken);

    public async Task<ReconciliationCase?> GetByCreationRequestAsync(Guid organizationId, Guid actorId,
        string requestKey, CancellationToken cancellationToken)
    {
        var id = await db.ReconciliationCases.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.CreatedByActorId == actorId &&
                item.CreationRequestKey == requestKey).Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return id.HasValue ? await GetAsync(organizationId, id.Value, cancellationToken) : null;
    }

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
