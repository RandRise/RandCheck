using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class CapacityConfiguration : IEntityTypeConfiguration<OrganizationCapacityPolicy>
{
    public void Configure(EntityTypeBuilder<OrganizationCapacityPolicy> b)
    {
        b.ToTable("organization_capacity_policies", t =>
        {
            t.HasCheckConstraint("ck_capacity_jobs", "max_active_jobs BETWEEN 1 AND 100000");
            t.HasCheckConstraint("ck_capacity_bytes", "max_stored_bytes BETWEEN 1 AND 109951162777600");
        });
        b.HasKey(x => x.OrganizationId);
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.MaxActiveJobs).HasColumnName("max_active_jobs");
        b.Property(x => x.MaxStoredBytes).HasColumnName("max_stored_bytes");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
