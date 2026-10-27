using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationCaseMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "reconciliation_cases",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Uncategorized");

            migrationBuilder.AddColumn<string>(
                name: "reference",
                table: "reconciliation_cases",
                type: "character varying(28)",
                maxLength: 28,
                nullable: false,
                defaultValue: "");

            // ALTER TABLE holds the case table lock until this transaction commits, so
            // concurrent inserts cannot race the deterministic legacy allocation.
            migrationBuilder.Sql("""
                CREATE TABLE public.reconciliation_reference_counters (
                    organization_id uuid NOT NULL REFERENCES public.organizations(id) ON DELETE RESTRICT,
                    reference_year integer NOT NULL CHECK (reference_year BETWEEN 1 AND 9999),
                    last_number bigint NOT NULL CHECK (last_number > 0),
                    PRIMARY KEY (organization_id, reference_year)
                );
                WITH numbered AS (
                    SELECT id, EXTRACT(YEAR FROM created_at_utc AT TIME ZONE 'UTC')::integer AS year,
                        row_number() OVER (PARTITION BY organization_id, EXTRACT(YEAR FROM created_at_utc AT TIME ZONE 'UTC')
                            ORDER BY created_at_utc, id) AS number
                    FROM public.reconciliation_cases
                )
                UPDATE public.reconciliation_cases c SET reference = 'REC-' || lpad(n.year::text,4,'0') || '-' ||
                    lpad(n.number::text,greatest(6,length(n.number::text)),'0') FROM numbered n WHERE c.id=n.id;
                INSERT INTO public.reconciliation_reference_counters (organization_id, reference_year, last_number)
                    SELECT organization_id, EXTRACT(YEAR FROM created_at_utc AT TIME ZONE 'UTC')::integer, count(*)
                    FROM public.reconciliation_cases GROUP BY organization_id, EXTRACT(YEAR FROM created_at_utc AT TIME ZONE 'UTC');

                CREATE FUNCTION public.allocate_reconciliation_reference() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
                DECLARE year integer; number bigint;
                BEGIN
                    IF NEW.reference IS NOT NULL AND NEW.reference<>'' THEN
                        RAISE EXCEPTION 'Case references are server assigned' USING ERRCODE='55000';
                    END IF;
                    year := EXTRACT(YEAR FROM NEW.created_at_utc AT TIME ZONE 'UTC')::integer;
                    INSERT INTO public.reconciliation_reference_counters AS counters (organization_id,reference_year,last_number)
                        VALUES (NEW.organization_id,year,1)
                        ON CONFLICT (organization_id,reference_year) DO UPDATE SET last_number=counters.last_number+1
                        RETURNING last_number INTO number;
                    NEW.reference := 'REC-' || lpad(year::text,4,'0') || '-' || lpad(number::text,greatest(6,length(number::text)),'0');
                    RETURN NEW;
                END $$;
                REVOKE ALL ON public.reconciliation_reference_counters FROM PUBLIC;
                REVOKE ALL ON FUNCTION public.allocate_reconciliation_reference() FROM PUBLIC;
                CREATE TRIGGER reconciliation_case_allocate_reference BEFORE INSERT ON public.reconciliation_cases
                    FOR EACH ROW EXECUTE FUNCTION public.allocate_reconciliation_reference();

                CREATE FUNCTION public.protect_reconciliation_reference() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.reference IS DISTINCT FROM OLD.reference OR NEW.organization_id<>OLD.organization_id
                        OR NEW.created_at_utc<>OLD.created_at_utc THEN
                        RAISE EXCEPTION 'Case reference and allocation identity are immutable' USING ERRCODE='55000';
                    END IF;
                    IF OLD.status='Closed' AND (NEW.name<>OLD.name OR NEW.category<>OLD.category) THEN
                        RAISE EXCEPTION 'Closed case metadata is immutable' USING ERRCODE='55000';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER reconciliation_case_protect_reference BEFORE UPDATE ON public.reconciliation_cases
                    FOR EACH ROW EXECUTE FUNCTION public.protect_reconciliation_reference();
                """);

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_cases_org_category_created",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "category", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_cases_org_created_id",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "created_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_cases_org_reference",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "reference" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reconciliation_cases_category",
                table: "reconciliation_cases",
                sql: "category IN ('Uncategorized','OfficeSupplies','Services','Equipment')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reconciliation_cases_reference",
                table: "reconciliation_cases",
                sql: "reference ~ '^REC-[0-9]{4}-[0-9]{6,19}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM public.reconciliation_cases) OR EXISTS (SELECT 1 FROM public.reconciliation_reference_counters) THEN
                        RAISE EXCEPTION 'Refusing to erase assigned case references or categories';
                    END IF;
                END $$;
                DROP TRIGGER reconciliation_case_allocate_reference ON public.reconciliation_cases;
                DROP TRIGGER reconciliation_case_protect_reference ON public.reconciliation_cases;
                DROP FUNCTION public.allocate_reconciliation_reference();
                DROP FUNCTION public.protect_reconciliation_reference();
                DROP TABLE public.reconciliation_reference_counters;
                """);
            migrationBuilder.DropIndex(
                name: "ix_reconciliation_cases_org_category_created",
                table: "reconciliation_cases");

            migrationBuilder.DropIndex(
                name: "ix_reconciliation_cases_org_created_id",
                table: "reconciliation_cases");

            migrationBuilder.DropIndex(
                name: "ux_reconciliation_cases_org_reference",
                table: "reconciliation_cases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reconciliation_cases_category",
                table: "reconciliation_cases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reconciliation_cases_reference",
                table: "reconciliation_cases");

            migrationBuilder.DropColumn(
                name: "category",
                table: "reconciliation_cases");

            migrationBuilder.DropColumn(
                name: "reference",
                table: "reconciliation_cases");
        }
    }
}
