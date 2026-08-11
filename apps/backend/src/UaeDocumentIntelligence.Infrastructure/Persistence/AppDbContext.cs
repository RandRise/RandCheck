using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Actor> Actors => Set<Actor>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ExtractionRun> ExtractionRuns => Set<ExtractionRun>();
    public DbSet<ExtractionPage> ExtractionPages => Set<ExtractionPage>();
    public DbSet<MachineFieldValue> MachineFieldValues => Set<MachineFieldValue>();
    public DbSet<FieldEvidence> FieldEvidence => Set<FieldEvidence>();
    public DbSet<ExtractionWarning> ExtractionWarnings => Set<ExtractionWarning>();
    public DbSet<ReviewSession> ReviewSessions => Set<ReviewSession>();
    public DbSet<ReviewAction> ReviewActions => Set<ReviewAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
