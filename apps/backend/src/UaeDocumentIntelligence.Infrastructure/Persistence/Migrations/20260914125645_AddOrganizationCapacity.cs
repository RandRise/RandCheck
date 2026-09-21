using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_capacity_policies",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_active_jobs = table.Column<int>(type: "integer", nullable: false),
                    max_stored_bytes = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_capacity_policies", x => x.organization_id);
                    table.CheckConstraint("ck_capacity_bytes", "max_stored_bytes BETWEEN 1 AND 109951162777600");
                    table.CheckConstraint("ck_capacity_jobs", "max_active_jobs BETWEEN 1 AND 100000");
                    table.ForeignKey(
                        name: "FK_organization_capacity_policies_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.Sql("""
                CREATE FUNCTION public.protect_capacity_policy() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                    IF TG_OP IN ('DELETE','TRUNCATE') THEN
                        RAISE EXCEPTION 'Capacity policies cannot be removed' USING ERRCODE='55000';
                    END IF;
                    IF NEW.organization_id<>OLD.organization_id THEN
                        RAISE EXCEPTION 'Capacity organization is immutable' USING ERRCODE='55000';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER capacity_policy_no_removal BEFORE DELETE OR TRUNCATE ON public.organization_capacity_policies
                    FOR EACH STATEMENT EXECUTE FUNCTION public.protect_capacity_policy();
                CREATE TRIGGER capacity_policy_identity BEFORE UPDATE ON public.organization_capacity_policies
                    FOR EACH ROW EXECUTE FUNCTION public.protect_capacity_policy();
                REVOKE ALL ON public.organization_capacity_policies FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM public.organization_capacity_policies) THEN
                        RAISE EXCEPTION 'Refusing to erase organization capacity policies';
                    END IF;
                END $$;
                DROP TRIGGER capacity_policy_no_removal ON public.organization_capacity_policies;
                DROP TRIGGER capacity_policy_identity ON public.organization_capacity_policies;
                DROP FUNCTION public.protect_capacity_policy();
                """);
            migrationBuilder.DropTable(
                name: "organization_capacity_policies");
        }
    }
}
