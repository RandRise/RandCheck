using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class ReconciliationDraftRequestStore(AppDbContext db) : IReconciliationDraftRequestStore
{
    public Task<ReconciliationDraftRequest?> FindAsync(Guid organizationId, Guid actorId,
        string requestKey, CancellationToken ct) => db.ReconciliationDraftRequests.AsNoTracking()
        .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.ActorId == actorId && x.RequestKey == requestKey, ct);
    public async Task AddAsync(ReconciliationDraftRequest request, CancellationToken ct) =>
        await db.ReconciliationDraftRequests.AddAsync(request, ct);
}
