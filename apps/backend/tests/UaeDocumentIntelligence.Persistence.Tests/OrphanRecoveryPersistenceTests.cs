using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Phase6_orphan_recovery_private_cli_requires_offline_plan_and_never_overwrites_inventory()
    {
        using var f = await DeletionFixtureAsync(); var org = await SourceOrganization(f.Source); var orphan = WriteOrphan(f, org);
        var configFile = Path.Combine(f.Source.Root, "orphan-cli.json"); var controlFile = Path.Combine(f.Source.Root, "controls.json");
        var planFile = Path.Combine(f.Source.Root, "orphan-plan.json");
        async Task Configure(bool offline, string planPath, string database = "document_intelligence_test") =>
            await File.WriteAllTextAsync(configFile, JsonSerializer.Serialize(new {
                JournalDirectory = Path.Combine(f.Source.Root, "deletion-journal"), f.Journal.JournalId,
                UploadDirectory = Path.Combine(f.Source.Root, "uploads"), QuarantineDirectory = Path.Combine(f.Source.Root, "quarantine"),
                ExpectedDatabase = database, ControlFile = controlFile, OrphanPlanFile = planPath, OfflineMaintenanceConfirmed = offline }));
        async Task<int> Run(string command)
        {
            var dll = Path.Combine(AppContext.BaseDirectory, "EnterpriseRecovery.dll");
            if (!File.Exists(dll))
            {
                var root = new DirectoryInfo(AppContext.BaseDirectory);
                while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/continuation.md"))) root = root.Parent;
                var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
                dll = Path.Combine(root!.FullName, "apps/backend/tools/EnterpriseRecovery/bin", configuration, "net8.0/EnterpriseRecovery.dll");
            }
            Assert.True(File.Exists(dll));
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { dll, command, configFile }) start.ArgumentList.Add(arg);
            start.Environment["PHASE6_RECOVERY_CONNECTION"] = _database.TestConnectionString;
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(); var text = await output + await error;
            Assert.DoesNotContain(f.Source.Root, text); Assert.DoesNotContain("PRIVATE", text); return process.ExitCode;
        }
        await Configure(false, planFile); Assert.Equal(0, await Run("export-controls"));
        Assert.Equal(1, await Run("inventory-orphans")); Assert.False(File.Exists(planFile));
        await Configure(true, Path.Combine(f.Source.Root, "uploads", "plan.json")); Assert.Equal(1, await Run("inventory-orphans"));
        await Configure(true, planFile); Assert.Equal(0, await Run("inventory-orphans")); Assert.True(File.Exists(orphan));
        var original = await File.ReadAllTextAsync(planFile);
        Assert.Equal(1, await Run("inventory-orphans")); Assert.Equal(original, await File.ReadAllTextAsync(planFile));
        Assert.Equal(0, await Run("cleanup-orphans")); Assert.False(File.Exists(orphan)); Assert.True(File.Exists(f.Source.File));
        await Configure(true, planFile, "wrong_database"); Assert.Equal(1, await Run("verify-restore"));
        await Configure(true, planFile); await SetCapacity(org, 10, 100000);
        Assert.Equal(1, await Run("verify-restore"));
    }

    [Fact]
    public async Task Phase6_orphan_recovery_inventory_bounds_candidates_and_reports_partial_coverage()
    {
        using var f = await DeletionFixtureAsync(); var org = await SourceOrganization(f.Source);
        for (var i = 0; i < 101; i++) WriteOrphan(f, org);
        await using var db = _database.CreateContext(); var recovery = OrphanRecovery(db, f);
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        var plan = await recovery.InventoryAsync(controls, 24, true, default);
        Assert.Equal(100, plan.Sources.Length); Assert.True(plan.Truncated);
        Assert.Equal(102, Directory.GetFiles(Path.Combine(f.Source.Root, "uploads"), "*", SearchOption.AllDirectories).Length);
        Assert.False(await db.SecurityAuditEvents.AnyAsync(x => x.Action.StartsWith("storage.orphan")));
    }

    [Fact]
    public async Task Phase6_orphan_recovery_refuses_unresolved_deletion_journal_before_touching_untracked_sources()
    {
        using var f = await DeletionFixtureAsync(); var orphan = WriteOrphan(f, await SourceOrganization(f.Source));
        var deletion = await DeletePlan(f); File.WriteAllText(f.Source.File, "Changed registered source");
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, (await Delete(f, deletion)).StatusCode);
        await using var db = _database.CreateContext(); var recovery = OrphanRecovery(db, f);
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.InventoryAsync(controls, 24, true, default));
        Assert.True(File.Exists(orphan)); Assert.True(File.Exists(f.Source.File));
        Assert.False(await db.SecurityAuditEvents.AnyAsync(x => x.Action.StartsWith("storage.orphan")));
    }

    private OrphanSourceRecovery OrphanRecovery(AppDbContext db, DeletionFixture f) => new(db, f.Journal,
        new StorageOptions { SecurityMode = "Enforced", UploadDirectory = Path.Combine(f.Source.Root, "uploads"),
            QuarantineDirectory = Path.Combine(f.Source.Root, "quarantine") },
        new SecurityAuditSink(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_database.TestConnectionString).Options));

    private static string WriteOrphan(DeletionFixture f, Guid organization, Guid? document = null, bool old = true)
    {
        var path = Path.Combine(f.Source.Root, "uploads", "organizations", organization.ToString("N"), "documents",
            (document ?? Guid.NewGuid()).ToString("N"), "source.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "PRIVATE orphan content");
        if (old) File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-3));
        return path;
    }

    [Fact]
    public async Task Phase6_orphan_recovery_snapshot_pins_capacity_presence_limits_version_and_rejects_legacy_schema()
    {
        using var f = await DeletionFixtureAsync(); await using var db = _database.CreateContext();
        var guard = new DocumentRecoveryGuard(db, f.Journal); var empty = await guard.ControlSnapshotAsync(default);
        var org = await SourceOrganization(f.Source); var policy = await SetCapacity(org, 10, 100000);
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.VerifyControlSnapshotAsync(empty, default));
        var current = await guard.ControlSnapshotAsync(default); await guard.VerifyControlSnapshotAsync(current, default);
        var legacy = JsonNode.Parse(current)!; legacy["Schema"] = 1; legacy.AsObject().Remove("CapacityPolicies");
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.VerifyControlSnapshotAsync(legacy.ToJsonString(), default));
        await ExecuteAsync("UPDATE organization_capacity_policies SET max_stored_bytes=90000");
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.VerifyControlSnapshotAsync(current, default));
        await SetCapacity(org, 10, 100000, policy.PolicyVersion);
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.VerifyControlSnapshotAsync(current, default));
        await guard.VerifyControlSnapshotAsync(await guard.ControlSnapshotAsync(default), default);
    }

    [Fact]
    public async Task Phase6_orphan_recovery_inventory_preserves_registered_young_locked_unknown_and_held_sources()
    {
        using var f = await DeletionFixtureAsync(); var org = await SourceOrganization(f.Source);
        var eligible = WriteOrphan(f, org); var young = WriteOrphan(f, org, old: false);
        var unknown = WriteOrphan(f, Guid.NewGuid()); var busy = WriteOrphan(f, org);
        File.SetLastWriteTimeUtc(f.Source.File, DateTime.UtcNow.AddDays(-4));
        var arbitrary = Path.Combine(f.Source.Root, "uploads", "unrecognized.txt"); File.WriteAllText(arbitrary, "keep");
        using var heldFile = new FileStream(busy, FileMode.Open, FileAccess.Read, FileShare.None);
        await using var db = _database.CreateContext(); var recovery = OrphanRecovery(db, f);
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.InventoryAsync(controls, 24, false, default));
        var plan = await recovery.InventoryAsync(controls, 24, true, default);
        Assert.Single(plan.Sources); Assert.EndsWith(plan.Sources[0].StorageKey.Replace('/', Path.DirectorySeparatorChar), eligible);
        Assert.Equal(4, plan.Preserved); Assert.Equal(1, plan.Unrecognized);
        Assert.All(new[] { eligible, young, unknown, busy, f.Source.File, arbitrary }, p => Assert.True(File.Exists(p)));
        await RetentionHold(f.Source, null);
        controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        Assert.Empty((await recovery.InventoryAsync(controls, 24, true, default)).Sources);
    }

    [Fact]
    public async Task Phase6_orphan_recovery_executes_only_reviewed_sources_with_durable_safe_audit_and_absent_replay()
    {
        using var f = await DeletionFixtureAsync(); var org = await SourceOrganization(f.Source);
        var orphan = WriteOrphan(f, org); await using var db = _database.CreateContext(); var recovery = OrphanRecovery(db, f);
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        var plan = await recovery.InventoryAsync(controls, 24, true, default); var later = WriteOrphan(f, org);
        Assert.Equal(new OrphanRecoveryResult(1, 0, 0), await recovery.ExecuteAsync(plan, controls, true, default));
        Assert.False(File.Exists(orphan)); Assert.True(File.Exists(later)); Assert.True(File.Exists(f.Source.File));
        Assert.Equal(new OrphanRecoveryResult(0, 0, 1), await recovery.ExecuteAsync(plan, controls, true, default));
        var audit = await db.SecurityAuditEvents.Where(x => x.RequestId == plan.Id.ToString("N")).OrderBy(x => x.RecordedAtUtc).ToArrayAsync();
        Assert.Equal(new[] { "storage.orphan-removal-authorized", "storage.orphan-removed" }, audit.Select(x => x.Action));
        Assert.All(audit, x => { Assert.Equal("System", x.ActorKind); Assert.Equal(org, x.OrganizationId); Assert.Equal(plan.Sources[0].DocumentId, x.TargetId); });
        Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(audit)); Assert.DoesNotContain(f.Source.Root, JsonSerializer.Serialize(audit));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("registered")]
    [InlineData("hold")]
    public async Task Phase6_orphan_recovery_rechecks_file_identity_registration_and_current_holds(string change)
    {
        using var f = await DeletionFixtureAsync(); var org = await SourceOrganization(f.Source); var orphan = WriteOrphan(f, org);
        await using var db = _database.CreateContext(); var recovery = OrphanRecovery(db, f);
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        var plan = await recovery.InventoryAsync(controls, 24, true, default); var source = Assert.Single(plan.Sources);
        if (change == "registered")
        {
            var actor = await db.Documents.Select(x => x.CreatedByActorId).SingleAsync();
            db.Documents.Add(new Document(source.DocumentId, org, actor, "registered.txt", source.StorageKey, "text/plain", source.SizeBytes, source.Sha256, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
            Assert.Equal(new OrphanRecoveryResult(0, 1, 0), await recovery.ExecuteAsync(plan, controls, true, default));
        }
        else if (change == "hold")
        {
            await RetentionHold(f.Source, null);
            await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.ExecuteAsync(plan, controls, true, default));
        }
        else
        {
            File.WriteAllText(orphan, "CHANGED orphan content"); File.SetLastWriteTimeUtc(orphan, source.LastWriteUtc);
            await Assert.ThrowsAsync<InvalidDataException>(() => recovery.ExecuteAsync(plan, controls, true, default));
        }
        Assert.True(File.Exists(orphan)); Assert.False(await db.SecurityAuditEvents.AnyAsync(x => x.Action.StartsWith("storage.orphan")));
    }

    [Theory]
    [InlineData("database")]
    [InlineData("root")]
    [InlineData("key")]
    [InlineData("expired")]
    [InlineData("offline")]
    public async Task Phase6_orphan_recovery_refuses_misdirected_stale_or_online_plans(string change)
    {
        using var f = await DeletionFixtureAsync(); var orphan = WriteOrphan(f, await SourceOrganization(f.Source));
        await using var db = _database.CreateContext(); var recovery = OrphanRecovery(db, f);
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        var plan = await recovery.InventoryAsync(controls, 24, true, default);
        plan = change switch { "database" => plan with { Database = "other" }, "root" => plan with { UploadRoot = f.Source.Root },
            "key" => plan with { Sources = [plan.Sources[0] with { StorageKey = "../outside.txt" }] },
            "expired" => plan with { CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2) }, _ => plan };
        await Assert.ThrowsAnyAsync<Exception>(() => recovery.ExecuteAsync(plan, controls, change != "offline", default));
        Assert.True(File.Exists(orphan)); Assert.False(await db.SecurityAuditEvents.AnyAsync(x => x.Action.StartsWith("storage.orphan")));
    }

    [Theory]
    [InlineData("storage.orphan-removal-authorized", true)]
    [InlineData("storage.orphan-removed", false)]
    public async Task Phase6_orphan_recovery_audit_outages_preserve_the_authorization_and_completion_boundary(string action, bool remains)
    {
        using var f = await DeletionFixtureAsync(); var orphan = WriteOrphan(f, await SourceOrganization(f.Source));
        await using var db = _database.CreateContext(); var recovery = OrphanRecovery(db, f);
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        var plan = await recovery.InventoryAsync(controls, 24, true, default);
        await ExecuteAsync($"""
            CREATE FUNCTION orphan_audit_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
            IF NEW.action='{action}' THEN RAISE EXCEPTION 'test outage'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER orphan_audit_outage BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION orphan_audit_outage();
            """);
        try { await Assert.ThrowsAsync<DbUpdateException>(() => recovery.ExecuteAsync(plan, controls, true, default)); }
        finally { await ExecuteAsync("DROP TRIGGER orphan_audit_outage ON security_audit_events; DROP FUNCTION orphan_audit_outage()"); }
        Assert.Equal(remains, File.Exists(orphan));
        Assert.Equal(remains ? 0 : 1, await db.SecurityAuditEvents.CountAsync(x => x.Action == "storage.orphan-removal-authorized"));
        Assert.False(await db.SecurityAuditEvents.AnyAsync(x => x.Action == "storage.orphan-removed"));
        var retry = await recovery.ExecuteAsync(plan, controls, true, default);
        Assert.Equal(remains ? new OrphanRecoveryResult(1, 0, 0) : new OrphanRecoveryResult(0, 0, 1), retry);
        if (!remains) Assert.False(await db.SecurityAuditEvents.AnyAsync(x => x.Action == "storage.orphan-removed"));
    }
}
