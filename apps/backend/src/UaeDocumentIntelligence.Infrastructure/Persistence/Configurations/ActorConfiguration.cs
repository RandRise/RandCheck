using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ActorConfiguration : IEntityTypeConfiguration<Actor>
{
    public void Configure(EntityTypeBuilder<Actor> b)
    {
        b.ToTable("actors", t => t.HasCheckConstraint("ck_actors_kind", "kind IN ('Human','System')"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ExternalSubject).HasColumnName("external_subject").HasMaxLength(200);
        b.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ExternalSubject }).IsUnique()
            .HasFilter("external_subject IS NOT NULL").HasDatabaseName("ux_actors_org_external_subject");
    }
}
