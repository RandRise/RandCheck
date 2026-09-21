using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options, SecurityAuditContext? audit = null) : DbContext(options)
{
    public DbSet<OrganizationCapacityPolicy> OrganizationCapacityPolicies => Set<OrganizationCapacityPolicy>();
    public DbSet<DocumentDeletion> DocumentDeletions => Set<DocumentDeletion>();
    public DbSet<OrganizationRetentionPolicy> OrganizationRetentionPolicies => Set<OrganizationRetentionPolicy>();
    public DbSet<RetentionHold> RetentionHolds => Set<RetentionHold>();
    public DbSet<EnterpriseIdentity> EnterpriseIdentities => Set<EnterpriseIdentity>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Actor> Actors => Set<Actor>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ExtractionRun> ExtractionRuns => Set<ExtractionRun>();
    public DbSet<ExtractionJob> ExtractionJobs => Set<ExtractionJob>();
    public DbSet<ExtractionPage> ExtractionPages => Set<ExtractionPage>();
    public DbSet<MachineFieldValue> MachineFieldValues => Set<MachineFieldValue>();
    public DbSet<FieldEvidence> FieldEvidence => Set<FieldEvidence>();
    public DbSet<ExtractionWarning> ExtractionWarnings => Set<ExtractionWarning>();
    public DbSet<ReviewSession> ReviewSessions => Set<ReviewSession>();
    public DbSet<ReviewAction> ReviewActions => Set<ReviewAction>();
    public DbSet<ReconciliationCase> ReconciliationCases => Set<ReconciliationCase>();
    public DbSet<ReconciliationDocumentLink> ReconciliationDocumentLinks => Set<ReconciliationDocumentLink>();
    public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();
    public DbSet<ReconciliationDocumentSnapshot> ReconciliationDocumentSnapshots => Set<ReconciliationDocumentSnapshot>();
    public DbSet<ReconciliationFieldSnapshot> ReconciliationFieldSnapshots => Set<ReconciliationFieldSnapshot>();
    public DbSet<ReconciliationFinding> ReconciliationFindings => Set<ReconciliationFinding>();
    public DbSet<FindingResolution> FindingResolutions => Set<FindingResolution>();
    public DbSet<CitedDocumentIndex> CitedDocumentIndexes => Set<CitedDocumentIndex>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ChunkEvidenceSpan> ChunkEvidenceSpans => Set<ChunkEvidenceSpan>();
    public DbSet<CitedRetrievalRun> CitedRetrievalRuns => Set<CitedRetrievalRun>();
    public DbSet<RetrievalSource> RetrievalSources => Set<RetrievalSource>();
    public DbSet<RetrievalHit> RetrievalHits => Set<RetrievalHit>();
    public DbSet<CitedAnswer> CitedAnswers => Set<CitedAnswer>();
    public DbSet<AnswerClaim> AnswerClaims => Set<AnswerClaim>();
    public DbSet<AnswerCitation> AnswerCitations => Set<AnswerCitation>();
    public DbSet<CitedPilotBudget> CitedPilotBudgets => Set<CitedPilotBudget>();
    public DbSet<CitedSpendReservation> CitedSpendReservations => Set<CitedSpendReservation>();
    public DbSet<CitedSpendEvent> CitedSpendEvents => Set<CitedSpendEvent>();
    public DbSet<CitedRecoveryAudit> CitedRecoveryAudits => Set<CitedRecoveryAudit>();
    public DbSet<CitedGenerationArtifact> CitedGenerationArtifacts => Set<CitedGenerationArtifact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectCitedHistory();
        var events = PrepareSecurityAudit();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch { DetachAudit(events); throw; }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ProtectCitedHistory();
        var events = PrepareSecurityAudit();
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch { DetachAudit(events); throw; }
    }

    private void ProtectCitedHistory()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.Entity is DocumentChunk or ChunkEvidenceSpan or RetrievalSource or RetrievalHit
                or CitedAnswer or AnswerClaim or AnswerCitation or CitedSpendEvent or CitedRecoveryAudit
                or CitedGenerationArtifact)
                throw new InvalidOperationException("Persisted cited evidence and answers are immutable.");
            if (entry.Entity is CitedDocumentIndex && (entry.State == EntityState.Deleted ||
                entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not
                    (nameof(CitedDocumentIndex.Status) or nameof(CitedDocumentIndex.ConcurrencyToken)))))
                throw new InvalidOperationException("An index may change eligibility, but not source or content.");
            if (entry.Entity is CitedRetrievalRun && (entry.State == EntityState.Deleted ||
                entry.Property(nameof(CitedRetrievalRun.Status)).OriginalValue is not
                    (CitedRetrievalStatus.Running or CitedRetrievalStatus.Retrieved) ||
                entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not
                    (nameof(CitedRetrievalRun.Status) or nameof(CitedRetrievalRun.ConcurrencyToken) or
                     nameof(CitedRetrievalRun.CompletedAtUtc) or nameof(CitedRetrievalRun.RetrievalDurationMs) or
                     nameof(CitedRetrievalRun.FailureCode)))))
                throw new InvalidOperationException("Retrieval inputs and terminal history are immutable.");
            if (entry.Entity is CitedRetrievalRun &&
                entry.Property(nameof(CitedRetrievalRun.Status)).OriginalValue is CitedRetrievalStatus.Retrieved &&
                (entry.Property(nameof(CitedRetrievalRun.RetrievalDurationMs)).IsModified ||
                 entry.Property(nameof(CitedRetrievalRun.Status)).CurrentValue is not
                    (CitedRetrievalStatus.Retrieved or CitedRetrievalStatus.Completed or CitedRetrievalStatus.Failed)))
                throw new InvalidOperationException("Retrieved evidence cannot restart or change its recorded duration.");
        }
    }
}
