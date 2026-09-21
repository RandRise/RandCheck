using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class EnterpriseIdentityConfiguration : IEntityTypeConfiguration<EnterpriseIdentity>
{
    public void Configure(EntityTypeBuilder<EnterpriseIdentity> b)
    {
        b.ToTable("enterprise_identities"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ObjectId).HasColumnName("object_id");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasIndex(x => new { x.TenantId, x.ObjectId }).IsUnique();
    }
}

internal sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> b)
    {
        b.ToTable("organization_memberships", t => t.HasCheckConstraint("ck_membership_role", "role IN ('None','Viewer','Reviewer','Operator')"));
        b.HasKey(x => x.Id); b.Ignore(x => x.AccessState);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.IdentityId).HasColumnName("identity_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.CanManageMembers).HasColumnName("can_manage_members");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.HasIndex(x => new { x.OrganizationId, x.IdentityId }).IsUnique();
        b.HasIndex(x => x.ActorId).IsUnique();
        // First UI/API contract exposes one active organization; future multi-org
        // support must deliberately remove this constraint and add a verified selector.
        b.HasIndex(x => x.IdentityId).IsUnique().HasFilter("is_active");
        b.HasOne<EnterpriseIdentity>().WithMany().HasForeignKey(x => x.IdentityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> b)
    {
        b.ToTable("security_audit_events", t =>
        {
            t.HasCheckConstraint("ck_security_audit_outcome", "outcome IN ('Succeeded','Denied','Failed')");
            t.HasCheckConstraint("ck_security_audit_actor", "actor_kind IN ('Human','System','Anonymous') AND (actor_id IS NULL OR organization_id IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.ActorKind).HasColumnName("actor_kind").HasMaxLength(16);
        b.Property(x => x.Action).HasColumnName("action").HasMaxLength(80);
        b.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(64);
        b.Property(x => x.TargetId).HasColumnName("target_id");
        b.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(16);
        b.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(64);
        b.Property(x => x.RequestId).HasColumnName("request_id").HasMaxLength(100);
        b.Property(x => x.BeforeState).HasColumnName("before_state").HasMaxLength(200);
        b.Property(x => x.AfterState).HasColumnName("after_state").HasMaxLength(200);
        b.Property(x => x.RecordedAtUtc).HasColumnName("recorded_at_utc").HasDefaultValueSql("clock_timestamp()");
        b.HasIndex(x => new { x.OrganizationId, x.RecordedAtUtc, x.Id });
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
