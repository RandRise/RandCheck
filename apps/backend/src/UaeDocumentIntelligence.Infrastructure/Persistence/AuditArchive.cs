using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed record AuditExportManifest(AuditExportScope Scope, int EventCount, long DataBytes, string DataSha256);
public sealed record AuditExportReceipt(Guid ExportId, int EventCount, long DataBytes, string DataSha256, string ManifestSha256);

public static class AuditArchive
{
    public const int MaxRows = 50000;
    public const long MaxBytes = 64 * 1024 * 1024;
    public const int MaxLineBytes = 8192;
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type == typeof(AuditExportManifest) || info.Type == typeof(AuditExportScope) || info.Type == typeof(AuditExportRow))
                foreach (var property in info.Properties) property.IsRequired = true;
        });
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 8, TypeInfoResolver = resolver
        };
        options.MakeReadOnly();
        return options;
    }

    public static async Task<AuditExportReceipt> WriteAsync(string outputDirectory, AuditExportScope scope,
        IAsyncEnumerable<AuditExportRow> rows, CancellationToken ct)
    {
        ValidateScope(scope);
        var root = PrivateDirectory(outputDirectory);
        var name = "audit-" + scope.ExportId.ToString("N");
        var staging = Path.GetFullPath(Path.Combine(root, "." + name + ".partial"));
        var final = Path.GetFullPath(Path.Combine(root, name));
        // Both generated paths are immediate children of the validated output root.
        if (Path.GetDirectoryName(staging) != root || Path.GetDirectoryName(final) != root ||
            Directory.Exists(staging) || File.Exists(staging) || Directory.Exists(final) || File.Exists(final))
            throw new InvalidOperationException("Audit archive already exists or has an invalid location.");
        Directory.CreateDirectory(staging);
        try
        {
            var validation = new RowValidation(scope);
            long bytes = 0;
            string digest;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using var output = new FileStream(Path.Combine(staging, "events.ndjson"), FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
                await foreach (var row in rows.WithCancellation(ct))
                {
                    validation.Add(row);
                    var line = JsonSerializer.SerializeToUtf8Bytes(row, JsonOptions);
                    if (line.Length > MaxLineBytes || bytes + line.Length + 1 > MaxBytes)
                        throw new InvalidOperationException("Audit archive size limit exceeded.");
                    await output.WriteAsync(line, ct); await output.WriteAsync(new byte[] { 10 }, ct);
                    hash.AppendData(line); hash.AppendData(new byte[] { 10 }); bytes += line.Length + 1;
                }
                output.Flush(true);
                digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }
            var manifest = new AuditExportManifest(scope, validation.Count, bytes, digest);
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
            var manifestHash = Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant();
            await using (var output = new FileStream(Path.Combine(staging, "manifest.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await output.WriteAsync(manifestBytes, ct); output.Flush(true); }
            var receipt = await VerifyFilesAsync(staging, manifestHash, false, ct);
            ct.ThrowIfCancellationRequested();
            PrivateDirectory(root); PrivateDirectory(staging);
            Directory.Move(staging, final); // Publish only the fully verified packet; never overwrite.
            return receipt;
        }
        catch
        {
            // Only remove this operation's two known files. Never recursively delete
            // an operator-supplied directory or follow a replaced staging link.
            try
            {
                PrivateDirectory(staging);
                foreach (var file in new[] { "events.ndjson", "manifest.json" })
                {
                    var path = Path.Combine(staging, file);
                    if (File.Exists(path)) { RegularFile(path); File.Delete(path); }
                }
                Directory.Delete(staging, false);
            }
            catch { /* An incomplete .partial directory requires private operator review. */ }
            throw;
        }
    }

    public static Task<AuditExportReceipt> VerifyAsync(string directory, string trustedManifestSha256, CancellationToken ct) =>
        VerifyFilesAsync(directory, trustedManifestSha256, true, ct);

    private static async Task<AuditExportReceipt> VerifyFilesAsync(string directory, string trustedHash, bool published, CancellationToken ct)
    {
        var root = PrivateDirectory(directory);
        var expected = Hash(trustedHash);
        if (!Directory.EnumerateFileSystemEntries(root).Take(3).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .SequenceEqual(new[] { "events.ndjson", "manifest.json" })) throw new InvalidOperationException("Unexpected archive contents.");
        var manifestBytes = await ReadBoundedAsync(Path.Combine(root, "manifest.json"), 16384, ct);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(manifestBytes), expected))
            throw new InvalidOperationException("Audit manifest does not match the trusted receipt.");
        var manifest = Parse<AuditExportManifest>(manifestBytes);
        ValidateScope(manifest.Scope);
        if (published && Path.GetFileName(root) != "audit-" + manifest.Scope.ExportId.ToString("N"))
            throw new InvalidOperationException("Audit archive is not published under its export ID.");
        if (manifest.EventCount < 0 || manifest.EventCount > manifest.Scope.MaxRows || manifest.DataBytes < 0 || manifest.DataBytes > MaxBytes)
            throw new InvalidOperationException("Invalid audit manifest bounds.");
        var dataHash = Hash(manifest.DataSha256);
        var data = Path.Combine(root, "events.ndjson"); RegularFile(data);
        await using var stream = new FileStream(data, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        var validation = new RowValidation(manifest.Scope);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long length = 0;
        await foreach (var line in LinesAsync(stream, ct))
        {
            hash.AppendData(line); hash.AppendData(new byte[] { 10 }); length += line.Length + 1;
            validation.Add(Parse<AuditExportRow>(line));
        }
        if (length != manifest.DataBytes || validation.Count != manifest.EventCount ||
            !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), dataHash))
            throw new InvalidOperationException("Audit archive content does not match its manifest.");
        return new(manifest.Scope.ExportId, validation.Count, length, manifest.DataSha256, trustedHash.ToLowerInvariant());
    }

    public static async Task<byte[]> ReadBoundedAsync(string path, int maximum, CancellationToken ct)
    {
        RegularFile(path);
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > maximum) throw new InvalidOperationException("Audit input is too large.");
        var bytes = new byte[maximum + 1]; var count = 0;
        while (count < bytes.Length)
        {
            var read = await input.ReadAsync(bytes.AsMemory(count), ct);
            if (read == 0) break;
            count += read;
        }
        if (count > maximum) throw new InvalidOperationException("Audit input is too large.");
        return bytes[..count];
    }

    public static T Parse<T>(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        UniqueProperties(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, JsonOptions) ?? throw new InvalidOperationException("Invalid audit input.");
    }

    private static void UniqueProperties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name)) throw new InvalidOperationException("Duplicate audit property.");
            UniqueProperties(property.Value);
        }
    }

    private static async IAsyncEnumerable<byte[]> LinesAsync(Stream stream, [EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new byte[65536]; using var line = new MemoryStream(); long length = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            length += read;
            if (length > MaxBytes) throw new InvalidOperationException("Audit archive size limit exceeded.");
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == 10)
                {
                    if (line.Length == 0) throw new InvalidOperationException("Empty audit record.");
                    yield return line.ToArray(); line.SetLength(0);
                }
                else
                {
                    if (line.Length >= MaxLineBytes) throw new InvalidOperationException("Audit record is too large.");
                    line.WriteByte(buffer[i]);
                }
            }
        }
        if (line.Length != 0) throw new InvalidOperationException("Truncated audit archive.");
    }

    private static byte[] Hash(string value)
    {
        if (value is null || value.Length != 64) throw new InvalidOperationException("Invalid audit checksum.");
        return Convert.FromHexString(value);
    }

    private static void ValidateScope(AuditExportScope scope)
    {
        if (scope is null || scope.SchemaVersion != 1 || scope.ExportId == Guid.Empty ||
            string.IsNullOrWhiteSpace(scope.DatabaseRole) || scope.DatabaseRole.Length > 63 ||
            scope.CapturedAtUtc.Offset != TimeSpan.Zero || scope.BeforeUtc > scope.CapturedAtUtc)
            throw new InvalidOperationException("Invalid audit snapshot metadata.");
        new AuditExportRequest(scope.Database, scope.OrganizationId, scope.FromUtc, scope.BeforeUtc, scope.MaxRows).Validate();
    }

    private static string PrivateDirectory(string directory)
    {
        if (!Path.IsPathFullyQualified(directory)) throw new InvalidOperationException("An absolute private directory is required.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var info = new DirectoryInfo(full);
        if (!info.Exists) throw new InvalidOperationException("Private directory must already exist.");
        for (var current = info; current is not null; current = current.Parent)
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("Linked audit directories are forbidden.");
        return full;
    }

    private static void RegularFile(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("An absolute audit file is required.");
        PrivateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var file = new FileInfo(path);
        if (!file.Exists || file.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("A regular audit file is required.");
    }

    private sealed class RowValidation(AuditExportScope scope)
    {
        private AuditExportRow? _previous;
        private readonly HashSet<Guid> _ids = [];
        public int Count => _ids.Count;
        public void Add(AuditExportRow row)
        {
            if (row.OrganizationId != scope.OrganizationId || row.Id == Guid.Empty || row.RecordedAtUtc.Offset != TimeSpan.Zero ||
                row.RecordedAtUtc < scope.FromUtc || row.RecordedAtUtc >= scope.BeforeUtc || Count >= scope.MaxRows || !_ids.Add(row.Id) ||
                _previous is not null && (row.RecordedAtUtc < _previous.RecordedAtUtc || row.RecordedAtUtc == _previous.RecordedAtUtc &&
                    StringComparer.Ordinal.Compare(row.Id.ToString("D"), _previous.Id.ToString("D")) <= 0))
                throw new InvalidOperationException("Invalid audit record scope, order or count.");
            _ = new SecurityAuditEvent(row.OrganizationId, row.ActorId, row.ActorKind, row.Action, row.TargetType,
                row.TargetId, row.Outcome, row.ReasonCode, row.RequestId, row.BeforeState, row.AfterState);
            _previous = row;
        }
    }
}
