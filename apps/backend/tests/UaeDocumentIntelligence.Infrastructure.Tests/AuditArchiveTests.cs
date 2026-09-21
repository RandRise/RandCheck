using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class AuditArchiveTests
{
    private static readonly Guid Organization = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static AuditExportScope Scope(int maximum = 100) => new(1, Guid.NewGuid(), "document_intelligence_test", "audit_reader",
        Organization, Start, Start.AddDays(1), Start.AddDays(2), maximum);
    private static AuditExportRow Row(int id) => new(Guid.Parse($"00000000-0000-0000-0000-{id:D12}"), Organization,
        null, "System", "request.admitted", "document", null, "Succeeded", "authorized_post", "test-request", null, null, Start);
    private static async IAsyncEnumerable<AuditExportRow> Rows(params AuditExportRow[] rows)
    { foreach (var row in rows) { await Task.Yield(); yield return row; } }
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task Archive_round_trip_preserves_order_and_can_be_verified_after_private_restore()
    {
        using var source = new Folder(); using var restore = new Folder(); var scope = Scope();
        var receipt = await AuditArchive.WriteAsync(source.Path, scope, Rows(Row(1), Row(2)), default);
        var archive = System.IO.Path.Combine(source.Path, "audit-" + scope.ExportId.ToString("N"));
        Assert.Equal(receipt, await AuditArchive.VerifyAsync(archive, receipt.ManifestSha256, default));
        Assert.Equal(2, receipt.EventCount);
        var lines = await File.ReadAllLinesAsync(System.IO.Path.Combine(archive, "events.ndjson"));
        Assert.Equal(new[] { Row(1), Row(2) }, lines.Select(l => JsonSerializer.Deserialize<AuditExportRow>(l, AuditArchive.JsonOptions)));
        var copy = System.IO.Path.Combine(restore.Path, System.IO.Path.GetFileName(archive)); Directory.CreateDirectory(copy);
        foreach (var file in new[] { "events.ndjson", "manifest.json" }) File.Copy(System.IO.Path.Combine(archive, file), System.IO.Path.Combine(copy, file));
        Assert.Equal(receipt, await AuditArchive.VerifyAsync(copy, receipt.ManifestSha256, default));
    }

    [Fact]
    public async Task Empty_window_has_a_valid_empty_archive_without_invented_events()
    {
        using var folder = new Folder(); var scope = Scope();
        var receipt = await AuditArchive.WriteAsync(folder.Path, scope, Rows(), default);
        Assert.Equal(0, receipt.EventCount); Assert.Equal(0, receipt.DataBytes);
        Assert.Equal(Digest([]), receipt.DataSha256);
        Assert.Equal(receipt, await AuditArchive.VerifyAsync(Directory.GetDirectories(folder.Path).Single(), receipt.ManifestSha256, default));
    }

    [Theory]
    [InlineData("data")]
    [InlineData("manifest")]
    [InlineData("count")]
    [InlineData("foreign-organization")]
    [InlineData("duplicate-id")]
    [InlineData("order")]
    [InlineData("outside-window")]
    [InlineData("missing-property")]
    [InlineData("unknown-property")]
    [InlineData("duplicate-property")]
    [InlineData("long-line")]
    [InlineData("truncated")]
    [InlineData("extra-file")]
    [InlineData("partial-directory")]
    public async Task Verification_refuses_corrupt_or_structurally_invalid_packets(string change)
    {
        using var folder = new Folder(); var scope = Scope();
        var receipt = await AuditArchive.WriteAsync(folder.Path, scope, Rows(Row(1), Row(2)), default);
        var archive = Directory.GetDirectories(folder.Path).Single(); var trusted = receipt.ManifestSha256;
        var dataPath = System.IO.Path.Combine(archive, "events.ndjson");
        var manifestPath = System.IO.Path.Combine(archive, "manifest.json");
        if (change == "extra-file") await File.WriteAllTextAsync(System.IO.Path.Combine(archive, "extra.txt"), "unexpected");
        else if (change == "partial-directory")
        { var renamed = archive + ".partial"; Directory.Move(archive, renamed); archive = renamed; }
        else if (change == "data") await File.AppendAllTextAsync(dataPath, "{}\n");
        else if (change == "manifest") await File.AppendAllTextAsync(manifestPath, " ");
        else
        {
            // Recomputed hashes deliberately exercise structural checks beyond digest comparison.
            var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!;
            var lines = await File.ReadAllLinesAsync(dataPath);
            var first = JsonNode.Parse(lines[0])!;
            switch (change)
            {
                case "count": manifest["eventCount"] = 1; break;
                case "foreign-organization": first["organizationId"] = Guid.NewGuid(); lines[0] = first.ToJsonString(); break;
                case "duplicate-id": lines[1] = lines[0]; break;
                case "order": Array.Reverse(lines); break;
                case "outside-window": first["recordedAtUtc"] = scope.BeforeUtc; lines[0] = first.ToJsonString(); break;
                case "missing-property": first.AsObject().Remove("actorId"); lines[0] = first.ToJsonString(); break;
                case "unknown-property": first["documentText"] = "not-an-audit-field"; lines[0] = first.ToJsonString(); break;
                case "duplicate-property": lines[0] = lines[0].Replace("\"actorId\":null", "\"actorId\":null,\"actorId\":null"); break;
                case "long-line": lines[0] = new string(' ', AuditArchive.MaxLineBytes + 1) + lines[0]; break;
            }
            var data = Encoding.UTF8.GetBytes(string.Join('\n', lines) + (change == "truncated" ? "" : "\n"));
            await File.WriteAllBytesAsync(dataPath, data);
            manifest["dataSha256"] = Digest(data); manifest["dataBytes"] = data.Length;
            var bytes = Encoding.UTF8.GetBytes(manifest.ToJsonString()); await File.WriteAllBytesAsync(manifestPath, bytes);
            trusted = Digest(bytes);
        }
        await Assert.ThrowsAnyAsync<Exception>(() => AuditArchive.VerifyAsync(archive, trusted, default));
    }

    [Theory]
    [InlineData("limit")]
    [InlineData("foreign")]
    [InlineData("order")]
    [InlineData("cancel")]
    [InlineData("failure")]
    public async Task Failed_write_does_not_publish_or_delete_unrelated_output(string failure)
    {
        using var folder = new Folder(); var scope = Scope(failure == "limit" ? 1 : 100);
        var sentinel = System.IO.Path.Combine(folder.Path, "keep.txt"); await File.WriteAllTextAsync(sentinel, "keep");
        using var cancel = new CancellationTokenSource();
        async IAsyncEnumerable<AuditExportRow> Input()
        {
            yield return Row(1); await Task.Yield();
            if (failure == "failure") throw new IOException("test-only interrupted source");
            if (failure == "cancel") { cancel.Cancel(); cancel.Token.ThrowIfCancellationRequested(); }
            yield return failure switch { "foreign" => Row(2) with { OrganizationId = Guid.NewGuid() }, "order" => Row(1), _ => Row(2) };
        }
        await Assert.ThrowsAnyAsync<Exception>(() => AuditArchive.WriteAsync(folder.Path, scope, Input(), cancel.Token));
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel));
        Assert.Empty(Directory.GetDirectories(folder.Path));
    }

    [Fact]
    public async Task Existing_archive_is_never_overwritten()
    {
        using var folder = new Folder(); var scope = Scope();
        var receipt = await AuditArchive.WriteAsync(folder.Path, scope, Rows(Row(1)), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditArchive.WriteAsync(folder.Path, scope, Rows(Row(2)), default));
        Assert.Equal(receipt, await AuditArchive.VerifyAsync(Directory.GetDirectories(folder.Path).Single(), receipt.ManifestSha256, default));
    }

    [Fact]
    public async Task Paths_and_private_plan_reads_are_bounded_without_implicit_directory_creation()
    {
        using var folder = new Folder(); var scope = Scope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditArchive.WriteAsync("relative-output", scope, Rows(), default));
        var missing = System.IO.Path.Combine(folder.Path, "missing");
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditArchive.WriteAsync(missing, scope, Rows(), default));
        Assert.False(Directory.Exists(missing));
        var input = System.IO.Path.Combine(folder.Path, "large-plan.json");
        await File.WriteAllBytesAsync(input, new byte[16385]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditArchive.ReadBoundedAsync(input, 16384, default));
    }

    [Theory]
    [InlineData("organization")]
    [InlineData("database")]
    [InlineData("retained")]
    [InlineData("window")]
    [InlineData("offset")]
    [InlineData("precision")]
    [InlineData("rows")]
    public void Invalid_export_scope_is_rejected_before_database_access(string invalid)
    {
        var request = new AuditExportRequest("document_intelligence_test", Organization, Start, Start.AddDays(1));
        request = invalid switch
        {
            "organization" => request with { OrganizationId = Guid.Empty },
            "database" => request with { ExpectedDatabase = "" },
            "retained" => request with { ExpectedDatabase = "phase5_e2e_20260908" },
            "window" => request with { BeforeUtc = Start },
            "offset" => request with { FromUtc = Start.ToOffset(TimeSpan.FromHours(4)) },
            "precision" => request with { BeforeUtc = request.BeforeUtc.AddTicks(1) },
            _ => request with { MaxRows = AuditArchive.MaxRows + 1 }
        };
        Assert.Throws<InvalidOperationException>(request.Validate);
    }

    private sealed class Folder : IDisposable
    {
        private readonly string _parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "audit-archive-tests"));
        public string Path { get; }
        public Folder() { Path = System.IO.Path.Combine(_parent, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public void Dispose()
        {
            if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)) != _parent) throw new InvalidOperationException();
            Directory.Delete(Path, true);
        }
    }
}
