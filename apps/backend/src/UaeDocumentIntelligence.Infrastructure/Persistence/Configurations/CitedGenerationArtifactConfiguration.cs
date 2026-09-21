using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class CitedGenerationArtifactConfiguration : IEntityTypeConfiguration<CitedGenerationArtifact>
{
    public void Configure(EntityTypeBuilder<CitedGenerationArtifact> b)
    {
        b.ToTable("cited_generation_artifacts", table =>
        {
            table.HasCheckConstraint("ck_cited_generation_artifact_kind", "kind IN ('Preparation', 'Outcome')");
            table.HasCheckConstraint("ck_cited_generation_artifact_bytes",
                "octet_length(payload_utf8) BETWEEN 1 AND CASE WHEN kind = 'Preparation' THEN 98304 ELSE 524288 END");
            table.HasCheckConstraint("ck_cited_generation_artifact_hashes",
                "payload_sha256 ~ '^[a-f0-9]{64}$' AND command_sha256 ~ '^[a-f0-9]{64}$'");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.ReservationId).HasColumnName("reservation_id");
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PayloadUtf8).HasColumnName("payload_utf8").IsRequired();
        b.Property(x => x.PayloadSha256).HasColumnName("payload_sha256").HasMaxLength(64).IsRequired();
        b.Property(x => x.CommandSha256).HasColumnName("command_sha256").HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<CitedSpendReservation>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ReservationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id, x.ActorId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ReservationId, x.Kind }).IsUnique()
            .HasDatabaseName("ux_cited_generation_artifact_kind");
    }
}
