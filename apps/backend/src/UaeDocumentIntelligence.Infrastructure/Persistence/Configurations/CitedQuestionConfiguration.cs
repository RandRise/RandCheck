using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal static partial class CitedMapping
{
    internal static void Identity<T>(EntityTypeBuilder<T> b) where T : class
    {
        b.HasKey("Id");
        b.HasAlternateKey("OrganizationId", "Id");
        b.Property<Guid>("Id").ValueGeneratedNever();
        foreach (var property in b.Metadata.GetProperties())
            property.SetColumnName(WordBoundary().Replace(property.Name, "$1_$2").ToLowerInvariant());
    }
    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundary();
}

internal sealed class CitedDocumentIndexConfiguration : IEntityTypeConfiguration<CitedDocumentIndex>
{
    public void Configure(EntityTypeBuilder<CitedDocumentIndex> b)
    {
        b.ToTable("cited_document_indexes", t =>
        {
            t.HasCheckConstraint("ck_cited_indexes_status", "status IN ('Building','Ready','Superseded','Quarantined')");
            t.HasCheckConstraint("ck_cited_indexes_sha", "document_sha256 ~ '^[0-9a-f]{64}$'");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id, x.ReviewSessionId, x.ExtractionRunId });
        b.Property(x => x.Filename).HasMaxLength(255);
        b.Property(x => x.DocumentSha256).HasMaxLength(64);
        b.Property(x => x.ProfileName).HasMaxLength(100);
        b.Property(x => x.ProfileVersion).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.HasOne<ReviewSession>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId, x.ReviewSessionId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.DocumentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReviewSession>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ReviewSessionId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id, x.ExtractionRunId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ReviewSessionId, x.ProfileName, x.ProfileVersion })
            .IsUnique().HasDatabaseName("ux_cited_indexes_review_profile");
        b.HasIndex(x => new { x.OrganizationId, x.Status }).HasDatabaseName("ix_cited_indexes_eligible");
        b.Navigation(x => x.Chunks).UsePropertyAccessMode(PropertyAccessMode.Field);
        CitedMapping.Identity(b);
    }
}

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.ToTable("document_chunks", t =>
        {
            t.HasCheckConstraint("ck_document_chunks_ordinal", "ordinal >= 0");
            t.HasCheckConstraint("ck_document_chunks_content", "octet_length(content) BETWEEN 1 AND 65536 AND btrim(content) <> ''");
            t.HasCheckConstraint("ck_document_chunks_hash", "content_sha256 ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_document_chunks_kind", "(kind = 'PageText' AND review_action_id IS NULL) OR (kind = 'ReviewedFact' AND review_action_id IS NOT NULL)");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.IndexId, x.Id });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id, x.ExtractionRunId });
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ContentSha256).HasMaxLength(64);
        b.HasOne<CitedDocumentIndex>().WithMany(x => x.Chunks)
            .HasForeignKey(x => new { x.OrganizationId, x.IndexId, x.ReviewSessionId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id, x.ReviewSessionId, x.ExtractionRunId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReviewAction>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ReviewSessionId, x.ExtractionRunId, x.ReviewActionId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ReviewSessionId, x.ExtractionRunId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.IndexId, x.Ordinal }).IsUnique()
            .HasDatabaseName("ux_document_chunks_index_ordinal");
        b.Navigation(x => x.Spans).UsePropertyAccessMode(PropertyAccessMode.Field);
        CitedMapping.Identity(b);
    }
}

internal sealed class ChunkEvidenceSpanConfiguration : IEntityTypeConfiguration<ChunkEvidenceSpan>
{
    public void Configure(EntityTypeBuilder<ChunkEvidenceSpan> b)
    {
        b.ToTable("chunk_evidence_spans", t =>
        {
            t.HasCheckConstraint("ck_chunk_spans_range", "page_number > 0 AND byte_start >= 0 AND byte_end > byte_start AND octet_length(quote) = byte_end - byte_start");
            t.HasCheckConstraint("ck_chunk_spans_polygon", "(bounding_polygon IS NULL AND coordinate_unit IS NULL) OR (bounding_polygon IS NOT NULL AND coordinate_unit IS NOT NULL AND coordinate_unit = 'normalized')");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.ChunkId, x.Id });
        b.Property(x => x.BoundingPolygon).HasColumnType("jsonb");
        b.Property(x => x.CoordinateUnit).HasMaxLength(16);
        b.HasOne<DocumentChunk>().WithMany(x => x.Spans)
            .HasForeignKey(x => new { x.OrganizationId, x.ChunkId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id, x.ExtractionRunId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExtractionPage>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId, x.PageNumber })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ExtractionRunId, x.PageNumber }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FieldEvidence>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId, x.PageNumber, x.FieldEvidenceId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ExtractionRunId, x.PageNumber, x.Id }).OnDelete(DeleteBehavior.Restrict);
        CitedMapping.Identity(b);
    }
}

internal sealed class CitedRetrievalRunConfiguration : IEntityTypeConfiguration<CitedRetrievalRun>
{
    public void Configure(EntityTypeBuilder<CitedRetrievalRun> b)
    {
        b.ToTable("cited_retrieval_runs", t =>
        {
            t.HasCheckConstraint("ck_cited_runs_status", "status IN ('Running','Retrieved','Completed','Failed')");
            t.HasCheckConstraint("ck_cited_runs_completion", "(status IN ('Running','Retrieved') AND completed_at_utc IS NULL AND failure_code IS NULL AND (status <> 'Retrieved' OR retrieval_duration_ms IS NOT NULL)) OR (status = 'Completed' AND completed_at_utc IS NOT NULL AND failure_code IS NULL) OR (status = 'Failed' AND completed_at_utc IS NOT NULL AND failure_code IS NOT NULL)");
            t.HasCheckConstraint("ck_cited_runs_duration", "(retrieval_duration_ms IS NULL OR retrieval_duration_ms >= 0) AND (completed_at_utc IS NULL OR completed_at_utc >= created_at_utc)");
            t.HasCheckConstraint("ck_cited_runs_question", "btrim(question) <> ''");
            t.HasCheckConstraint("ck_cited_runs_policy", "jsonb_typeof(policy_json) = 'object'");
        });
        b.Property(x => x.Question).HasMaxLength(4000);
        b.Property(x => x.RequestKey).HasMaxLength(100);
        b.Property(x => x.PolicyVersion).HasMaxLength(100);
        b.Property(x => x.PolicyJson).HasColumnType("jsonb");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.FailureCode).HasMaxLength(100);
        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.HasOne<Actor>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ActorId, x.RequestKey }).IsUnique()
            .HasDatabaseName("ux_cited_runs_request");
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc }).HasDatabaseName("ix_cited_runs_history");
        b.Navigation(x => x.Sources).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Hits).UsePropertyAccessMode(PropertyAccessMode.Field);
        CitedMapping.Identity(b);
    }
}

internal sealed class RetrievalSourceConfiguration : IEntityTypeConfiguration<RetrievalSource>
{
    public void Configure(EntityTypeBuilder<RetrievalSource> b)
    {
        b.ToTable("retrieval_sources");
        b.HasAlternateKey(x => new { x.OrganizationId, x.RetrievalRunId, x.IndexId });
        b.HasOne<CitedRetrievalRun>().WithMany(x => x.Sources)
            .HasForeignKey(x => new { x.OrganizationId, x.RetrievalRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CitedDocumentIndex>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.IndexId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        CitedMapping.Identity(b);
    }
}

internal sealed class RetrievalHitConfiguration : IEntityTypeConfiguration<RetrievalHit>
{
    public void Configure(EntityTypeBuilder<RetrievalHit> b)
    {
        b.ToTable("retrieval_hits", t =>
        {
            t.HasCheckConstraint("ck_retrieval_hits_rank", "rank BETWEEN 1 AND 100");
            foreach (var column in new[] { "lexical_score", "vector_score", "fused_score" })
                t.HasCheckConstraint($"ck_hits_{column}", $"{column} IS NULL OR ({column} > '-Infinity'::float8 AND {column} < 'Infinity'::float8)");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.RetrievalRunId, x.Id, x.ChunkId });
        b.HasOne<CitedRetrievalRun>().WithMany(x => x.Hits)
            .HasForeignKey(x => new { x.OrganizationId, x.RetrievalRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RetrievalSource>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.RetrievalRunId, x.IndexId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.RetrievalRunId, x.IndexId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DocumentChunk>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.IndexId, x.ChunkId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.IndexId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.RetrievalRunId, x.Rank }).IsUnique()
            .HasDatabaseName("ux_retrieval_hits_rank");
        b.HasIndex(x => new { x.OrganizationId, x.RetrievalRunId, x.ChunkId }).IsUnique()
            .HasDatabaseName("ux_retrieval_hits_chunk");
        CitedMapping.Identity(b);
    }
}

internal sealed class CitedAnswerConfiguration : IEntityTypeConfiguration<CitedAnswer>
{
    public void Configure(EntityTypeBuilder<CitedAnswer> b)
    {
        b.ToTable("cited_answers", t =>
        {
            t.HasCheckConstraint("ck_cited_answers_status", "status IN ('Answered','PartiallyAnswered','InsufficientEvidence')");
            t.HasCheckConstraint("ck_cited_answers_reason", "(status = 'Answered' AND reason_code IS NULL) OR (status IN ('PartiallyAnswered','InsufficientEvidence') AND reason_code IS NOT NULL AND reason_code IN ('no_relevant_evidence','incomplete_evidence','conflicting_evidence','unsupported_scope','unsafe_source'))");
            t.HasCheckConstraint("ck_cited_answers_cost", "(estimated_cost IS NULL AND cost_currency IS NULL) OR (estimated_cost IS NOT NULL AND estimated_cost >= 0 AND cost_currency IS NOT NULL AND cost_currency ~ '^[A-Z]{3}$')");
            t.HasCheckConstraint("ck_cited_answers_duration", "generation_duration_ms >= 0");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id, x.RetrievalRunId });
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ReasonCode).HasMaxLength(100);
        b.Property(x => x.ProviderAlias).HasMaxLength(100);
        b.Property(x => x.ModelVersion).HasMaxLength(100);
        b.Property(x => x.PromptVersion).HasMaxLength(100);
        b.Property(x => x.EstimatedCost).HasPrecision(18, 6);
        b.Property(x => x.CostCurrency).HasMaxLength(3);
        b.HasOne<CitedRetrievalRun>().WithOne(x => x.Answer)
            .HasForeignKey<CitedAnswer>(x => new { x.OrganizationId, x.RetrievalRunId })
            .HasPrincipalKey<CitedRetrievalRun>(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Claims).UsePropertyAccessMode(PropertyAccessMode.Field);
        CitedMapping.Identity(b);
    }
}

internal sealed class AnswerClaimConfiguration : IEntityTypeConfiguration<AnswerClaim>
{
    public void Configure(EntityTypeBuilder<AnswerClaim> b)
    {
        b.ToTable("answer_claims", t =>
        {
            t.HasCheckConstraint("ck_answer_claims_ordinal", "ordinal >= 0");
            t.HasCheckConstraint("ck_answer_claims_text", "btrim(text) <> ''");
        });
        b.HasAlternateKey(x => new { x.OrganizationId, x.RetrievalRunId, x.Id });
        b.Property(x => x.Text).HasMaxLength(4000);
        b.HasOne<CitedAnswer>().WithMany(x => x.Claims)
            .HasForeignKey(x => new { x.OrganizationId, x.AnswerId, x.RetrievalRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id, x.RetrievalRunId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.AnswerId, x.Ordinal }).IsUnique()
            .HasDatabaseName("ux_answer_claims_ordinal");
        b.Navigation(x => x.Citations).UsePropertyAccessMode(PropertyAccessMode.Field);
        CitedMapping.Identity(b);
    }
}

internal sealed class AnswerCitationConfiguration : IEntityTypeConfiguration<AnswerCitation>
{
    public void Configure(EntityTypeBuilder<AnswerCitation> b)
    {
        b.ToTable("answer_citations");
        b.HasOne<AnswerClaim>().WithMany(x => x.Citations)
            .HasForeignKey(x => new { x.OrganizationId, x.RetrievalRunId, x.ClaimId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.RetrievalRunId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RetrievalHit>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.RetrievalRunId, x.HitId, x.ChunkId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.RetrievalRunId, x.Id, x.ChunkId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ChunkEvidenceSpan>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ChunkId, x.SpanId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ChunkId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ClaimId, x.HitId, x.SpanId })
            .IsUnique().HasFilter("span_id IS NOT NULL").HasDatabaseName("ux_answer_citations_span");
        b.HasIndex(x => new { x.OrganizationId, x.ClaimId, x.HitId })
            .IsUnique().HasFilter("span_id IS NULL").HasDatabaseName("ux_answer_citations_review");
        CitedMapping.Identity(b);
    }
}
