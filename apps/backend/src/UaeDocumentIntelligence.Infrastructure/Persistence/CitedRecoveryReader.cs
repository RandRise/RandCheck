using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedRecoveryReader(DbContextOptions<AppDbContext> options) : ICitedRecoveryReader
{
    public async Task<CitedRecoverySnapshot?> ReadAsync(Guid organizationId, Guid operatorActorId,
        Guid reservationId, CancellationToken ct)
    {
        await using var db = new AppDbContext(options);
        return await db.CitedSpendReservations.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Id == reservationId &&
                db.Actors.Any(a => a.OrganizationId == organizationId && a.Id == operatorActorId && a.IsActive))
            .Select(x => new CitedRecoverySnapshot(x.Id, x.OrganizationId, x.ActorId, x.OperationId,
                x.RequestSha256, x.Provider, x.Model, x.ModelVersion, x.State,
                db.CitedSpendEvents.Where(e => e.OrganizationId == organizationId && e.ReservationId == x.Id)
                    .Max(e => (int?)e.Sequence) ?? 0,
                x.ReservedUsd, x.ActualUsd, x.CreatedAtUtc)).SingleOrDefaultAsync(ct);
    }
}
