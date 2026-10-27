"""Review/apply the Staff/Admin migration using psql's PG* environment settings.

Inventory is read-only. Apply requires an explicitly reviewed plan, checks the exact
database snapshot inside the schema transaction, and never prints credentials.
Stop application/worker writes and take a backup before an approved cutover.
"""

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
MIGRATION = "20261019190302_UseStaffAndAdminRoles"
PREVIOUS = "20261006164334_AddInvoicePurchaseOrderDraftWorkflow"
INVENTORY = ROOT / "apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/Migrations/Sql/StaffAdminInventory.sql"
SCRIPT = ROOT / "tools/migrate-access-roles.sql"


def run_psql(sql: str, executable: str, *, read_only: bool = False) -> str:
    env = dict(os.environ, PGCONNECT_TIMEOUT="5")
    if read_only:
        env["PGOPTIONS"] = "-c default_transaction_read_only=on"
    result = subprocess.run(
        [executable, "-X", "-w", "-A", "-t", "-v", "ON_ERROR_STOP=1"],
        input=sql, capture_output=True, encoding="utf-8", env=env, check=False,
    )
    if result.returncode:
        # A lost connection after COMMIT is indistinguishable from an earlier
        # failure here. Never promise rollback or encourage a blind retry.
        raise RuntimeError("Database operation failed or its outcome is unknown. Inspect schema and migration audit records read-only before retrying.")
    return result.stdout.strip()


def load_approved_plan(path: Path) -> dict:
    plan = json.loads(path.read_text(encoding="utf-8"))
    if plan.get("approved") is not True or plan.get("migration") != MIGRATION:
        raise ValueError("Review the account changes and explicitly approve this migration plan first.")
    if not isinstance(plan.get("snapshot"), dict) or not isinstance(plan["snapshot"].get("members"), list):
        raise ValueError("Invalid inventory snapshot.")
    return plan


def migration_script(path: Path | None) -> str:
    """Accept only the versioned, single-migration script, never arbitrary SQL."""
    canonical = SCRIPT.read_text(encoding="utf-8-sig")
    if path is not None and path.read_text(encoding="utf-8-sig") != canonical:
        raise ValueError("SQL differs from the repository's guarded migration. Regenerate and review it before applying.")
    return canonical


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["inventory", "apply"])
    parser.add_argument("--plan", type=Path, required=True)
    parser.add_argument("--psql", default="psql")
    parser.add_argument("--script", type=Path, help="Optional exact copy of the versioned guarded migration SQL")
    args = parser.parse_args()
    if args.command == "inventory":
        if args.plan.exists():
            raise ValueError("Use a new plan path; an earlier review must not be overwritten.")
        snapshot = json.loads(run_psql(INVENTORY.read_text(encoding="utf-8"), args.psql, read_only=True))
        args.plan.write_text(json.dumps({"migration": MIGRATION, "approved": False, "snapshot": snapshot},
                                       indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"Wrote {len(snapshot['members'])} account changes for review. No access changed.")
        return
    plan = load_approved_plan(args.plan)
    script = migration_script(args.script)
    # A session setting is data, not executable SQL. Escape single quotes after
    # JSON serialization, including account display names containing apostrophes.
    snapshot = json.dumps(plan["snapshot"], ensure_ascii=False).replace("'", "''")
    prelude = f"""
DO $check$ BEGIN
 IF (SELECT max("MigrationId") FROM "__EFMigrationsHistory") IS DISTINCT FROM '{PREVIOUS}' THEN
  RAISE EXCEPTION 'Unexpected schema version; inspect migration status before continuing';
 END IF;
END $check$;
SELECT set_config('uaedi.approved_role_inventory', '{snapshot}', false);
"""
    run_psql(prelude + script, args.psql)
    print("Reviewed Staff/Admin migration committed. Original audit records are preserved.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, OSError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
