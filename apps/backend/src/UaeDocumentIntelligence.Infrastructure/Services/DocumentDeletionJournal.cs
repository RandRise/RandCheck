using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Services;

public sealed class DocumentDeletionOptions
{
    public bool Enabled { get; set; }
    public string JournalDirectory { get; set; } = "";
    public Guid JournalId { get; set; }
}

public sealed record DeletionIntent(Guid Id, Guid OrganizationId, Guid DocumentId, string StorageKey, string Sha256,
    long SizeBytes, Guid DocumentVersion, Guid PolicyVersion, DateTimeOffset RequestedAtUtc)
{
    public static DeletionIntent From(DocumentDeletion d) => new(d.Id, d.OrganizationId, d.DocumentId, d.StorageKey,
        d.Sha256, d.SizeBytes, d.DocumentVersion, d.PolicyVersion, d.RequestedAtUtc);
    public DocumentDeletion ToRecord() => new(Id, OrganizationId, DocumentId, StorageKey, Sha256, SizeBytes, DocumentVersion, PolicyVersion, RequestedAtUtc);
}

public sealed class DocumentDeletionJournal(IOptions<DocumentDeletionOptions> options, IOptions<StorageOptions> storage)
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public bool Enabled => options.Value.Enabled;
    public Guid JournalId => options.Value.JournalId;
    public string RequireRoot()
    {
        if (!Enabled || options.Value.JournalId == Guid.Empty || storage.Value.SecurityMode != "Enforced" ||
            !Path.IsPathFullyQualified(options.Value.JournalDirectory)) throw new InvalidOperationException("Deletion is not configured.");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Value.JournalDirectory));
        foreach (var other in new[] { storage.Value.UploadDirectory, storage.Value.QuarantineDirectory })
        {
            if (string.IsNullOrWhiteSpace(other)) throw new InvalidOperationException();
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(other));
            if (root.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                root.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        }
        LocalDocumentStorage.EnsureNoReparsePoints(root, "journal-id");
        var idPath = LocalDocumentStorage.ResolveUnderRoot(root, "journal-id");
        if (new FileInfo(idPath).Length > 100 || File.ReadAllText(idPath).Trim() != options.Value.JournalId.ToString("D"))
            throw new InvalidOperationException("Deletion journal identity mismatch.");
        return root;
    }
    public static void Initialize(string directory, Guid id)
    {
        if (!Path.IsPathFullyQualified(directory) || id == Guid.Empty) throw new ArgumentException();
        var root = Path.GetFullPath(directory);
        if (Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(root)) is null) throw new ArgumentException();
        LocalDocumentStorage.EnsureNoReparsePoints(root, "journal-id");
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new InvalidOperationException("Use a new empty journal directory.");
        Directory.CreateDirectory(root);
        using var file = new FileStream(Path.Combine(root, "journal-id"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(System.Text.Encoding.UTF8.GetBytes(id.ToString("D"))); file.Flush(true);
    }
    private static string Key(Guid org, Guid document) => $"{org:N}.{document:N}.json";
    public DeletionIntent? Find(Guid org, Guid document)
    {
        var root = RequireRoot(); var key = Key(org, document);
        LocalDocumentStorage.EnsureNoReparsePoints(root, key);
        var path = LocalDocumentStorage.ResolveUnderRoot(root, key);
        return File.Exists(path) ? Read(root, path) : null;
    }
    public void Append(DeletionIntent intent)
    {
        Validate(intent); var root = RequireRoot(); var key = Key(intent.OrganizationId, intent.DocumentId);
        LocalDocumentStorage.EnsureNoReparsePoints(root, key);
        var path = LocalDocumentStorage.ResolveUnderRoot(root, key);
        if (File.Exists(path))
        {
            if (Read(root, path) != intent) throw new InvalidOperationException("Conflicting deletion intent.");
            return;
        }
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(JsonSerializer.SerializeToUtf8Bytes(intent, Json)); file.Flush(true);
    }
    public IReadOnlyList<DeletionIntent> ReadAll()
    {
        var root = RequireRoot(); var paths = Directory.EnumerateFileSystemEntries(root).Take(1002).ToArray();
        if (paths.Length > 1001) throw new InvalidOperationException("Journal inspection limit exceeded.");
        return paths.Where(p => Path.GetFileName(p) != "journal-id").Select(p => Read(root, p)).ToArray();
    }
    private static DeletionIntent Read(string root, string path)
    {
        LocalDocumentStorage.EnsureNoReparsePoints(root, Path.GetFileName(path));
        if (new FileInfo(path).Length is < 1 or > 4096) throw new InvalidDataException();
        var intent = JsonSerializer.Deserialize<DeletionIntent>(File.ReadAllBytes(path), Json) ?? throw new InvalidDataException();
        Validate(intent);
        if (Path.GetFileName(path) != Key(intent.OrganizationId, intent.DocumentId)) throw new InvalidDataException();
        return intent;
    }
    private static void Validate(DeletionIntent intent)
    {
        if (intent.Id == Guid.Empty || intent.OrganizationId == Guid.Empty || intent.DocumentId == Guid.Empty ||
            intent.DocumentVersion == Guid.Empty || intent.PolicyVersion == Guid.Empty || intent.SizeBytes is < 1 or > 10485760 ||
            intent.RequestedAtUtc == default || intent.RequestedAtUtc.Offset != TimeSpan.Zero ||
            intent.Sha256.Length != 64 || intent.Sha256.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new InvalidDataException();
        var prefix = $"organizations/{intent.OrganizationId:N}/documents/{intent.DocumentId:N}/source.";
        if (!intent.StorageKey.StartsWith(prefix, StringComparison.Ordinal) || intent.StorageKey[prefix.Length..].Length is < 1 or > 9 ||
            intent.StorageKey[prefix.Length..].Any(c => !char.IsAsciiLetterOrDigit(c))) throw new InvalidDataException();
    }
    public bool SourceExists(DeletionIntent intent) => File.Exists(SourcePath(intent));
    public string SourcePath(DeletionIntent intent) => LocalDocumentStorage.ResolveDocumentSourcePath(
        storage.Value.UploadDirectory, intent.OrganizationId, intent.DocumentId, intent.StorageKey);
    public async Task RemoveSourceAsync(DeletionIntent intent, CancellationToken ct)
    {
        RequireRoot(); var path = SourcePath(intent);
        if (!File.Exists(path)) return;
        await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.Asynchronous))
        {
            if (file.Length != intent.SizeBytes || Convert.ToHexString(await SHA256.HashDataAsync(file, ct)).ToLowerInvariant() != intent.Sha256)
                throw new InvalidDataException("Source integrity does not match deletion intent.");
        }
        ct.ThrowIfCancellationRequested();
        File.Delete(SourcePath(intent));
    }
}
