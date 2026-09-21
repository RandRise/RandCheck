namespace UaeDocumentIntelligence.Infrastructure.Services;

// Upload and cleanup cooperate through a lease held across scan AND promotion.
// Flat legacy staging names have no trusted tenant binding and are never candidates.
internal static class QuarantineFiles
{
    internal static string Root(StorageOptions options)
    {
        if (options.SecurityMode != "Enforced" || string.IsNullOrWhiteSpace(options.QuarantineDirectory))
            throw new InvalidOperationException("Enforced quarantine is required.");
        var uploads = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.UploadDirectory));
        var quarantine = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.QuarantineDirectory));
        if (string.Equals(uploads, quarantine, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(uploads), Path.GetDirectoryName(quarantine), StringComparison.OrdinalIgnoreCase) ||
            Path.GetDirectoryName(quarantine) is null)
            throw new InvalidOperationException("Quarantine must be a separate sibling directory.");
        LocalDocumentStorage.EnsureNoReparsePoints(quarantine, "probe");
        return quarantine;
    }

    internal static string Key(Guid organizationId, Guid documentId, Guid uploadId) =>
        $"{organizationId:N}.{documentId:N}.{uploadId:N}.upload";
    internal static string LeaseKey(string key) => key[..^7] + ".lease";

    internal static IReadOnlyList<Candidate> Candidates(string root, Guid organizationId)
    {
        if (!Directory.Exists(root)) return [];
        var result = new List<Candidate>();
        // Bounded work per request; callers receive a truncated flag at 501.
        foreach (var file in Directory.EnumerateFiles(root, $"{organizationId:N}.*.upload", SearchOption.TopDirectoryOnly).Take(501))
        {
            var key = Path.GetFileName(file); var parts = key.Split('.');
            if (parts.Length != 4 || parts[0] != organizationId.ToString("N") || parts[3] != "upload" ||
                !Guid.TryParseExact(parts[1], "N", out var documentId) || documentId == Guid.Empty ||
                !Guid.TryParseExact(parts[2], "N", out var uploadId) || uploadId == Guid.Empty ||
                key != Key(organizationId, documentId, uploadId)) continue;
            result.Add(new(key, documentId, uploadId));
        }
        return result;
    }

    internal static FileStream? TryLease(string root, Candidate candidate, DateTimeOffset cutoff)
    {
        var leaseKey = LeaseKey(candidate.Key);
        LocalDocumentStorage.EnsureNoReparsePoints(root, candidate.Key);
        LocalDocumentStorage.EnsureNoReparsePoints(root, leaseKey);
        var leasePath = LocalDocumentStorage.ResolveUnderRoot(root, leaseKey);
        FileStream lease;
        try { lease = new FileStream(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return null; }
        try
        {
            var path = LocalDocumentStorage.ResolveUnderRoot(root, candidate.Key);
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) > cutoff.UtcDateTime ||
                File.GetLastWriteTimeUtc(leasePath) > cutoff.UtcDateTime)
            { lease.Dispose(); return null; }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    internal static void DeleteStaged(string root, Candidate candidate)
    {
        LocalDocumentStorage.EnsureNoReparsePoints(root, candidate.Key);
        File.Delete(LocalDocumentStorage.ResolveUnderRoot(root, candidate.Key));
    }
    internal sealed record Candidate(string Key, Guid DocumentId, Guid UploadId);
}
