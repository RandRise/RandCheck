using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;

namespace UaeDocumentIntelligence.Infrastructure.Services;

public sealed class StorageOptions
{
    public string UploadDirectory { get; set; } = "./data/uploads";
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
    public string SecurityMode { get; set; } = "Development";
    public string QuarantineDirectory { get; set; } = string.Empty;
    public static string ResolveSecurityMode(string? configured, string environment)
    {
        var local = environment is "Development" or "Testing";
        var mode = string.IsNullOrEmpty(configured) ? (local ? "Development" : "Locked") : configured;
        return mode == "Enforced" || mode == "Development" && local ? mode : "Locked";
    }
}

internal sealed class LocalDocumentStorage(IOptions<StorageOptions> options, IMalwareScanner? scanner = null) : IDocumentStorage
{
    public async Task<StoredFile> SaveAsync(Stream content, string originalFilename, long declaredSize,
        Guid organizationId, Guid documentId, CancellationToken cancellationToken)
    {
        if (options.Value.SecurityMode is not ("Development" or "Enforced")) throw new MalwareScanUnavailableException();
        if (organizationId == Guid.Empty || documentId == Guid.Empty) throw new InvalidDataException("Storage identity is required.");
        if (declaredSize <= 0 || declaredSize > Math.Min(options.Value.MaxUploadBytes, 10485760)) throw new DocumentTooLargeException();
        var root = Path.GetFullPath(options.Value.UploadDirectory);
        var extension = Path.GetExtension(originalFilename).ToLowerInvariant();
        if (extension.Length is < 2 or > 10 || extension.Skip(1).Any(c => !char.IsAsciiLetterOrDigit(c)))
            extension = ".bin";
        var key = Path.Combine("organizations", organizationId.ToString("N"), "documents",
            documentId.ToString("N"), $"source{extension}").Replace('\\', '/');
        var path = ResolveUnderRoot(root, key);
        CreateSafeParent(root, key);
        EnsureNoReparsePoints(root, key);
        if (File.Exists(path)) throw new IOException("A stored source already exists.");
        var quarantine = options.Value.SecurityMode == "Enforced" ? Path.GetFullPath(
            string.IsNullOrWhiteSpace(options.Value.QuarantineDirectory) ? throw new MalwareScanUnavailableException() : options.Value.QuarantineDirectory)
            : Path.Combine(root, ".incoming");
        if (options.Value.SecurityMode == "Enforced" && (string.Equals(root, quarantine, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(root)), Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(quarantine)), StringComparison.OrdinalIgnoreCase)))
            throw new MalwareScanUnavailableException();
        var temporaryKey = options.Value.SecurityMode == "Enforced"
            ? QuarantineFiles.Key(organizationId, documentId, Guid.NewGuid())
            : Guid.NewGuid().ToString("N") + ".upload";
        CreateSafeParent(quarantine, temporaryKey);
        var temporary = ResolveUnderRoot(quarantine, temporaryKey);
        var leasePath = options.Value.SecurityMode == "Enforced"
            ? ResolveUnderRoot(quarantine, QuarantineFiles.LeaseKey(temporaryKey)) : null;
        FileStream? lease = leasePath is null ? null : new FileStream(leasePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var ownsTemporary = false;
        try
        {
            string hash;
            await using (var destination = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                ownsTemporary = true;
                var buffer = new byte[65536]; long total = 0; int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, declaredSize - total + 1)), cancellationToken)) != 0)
                {
                    total += read;
                    if (total > declaredSize) throw new DocumentTooLargeException();
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (total != declaredSize) throw new DocumentTooLargeException();
                await destination.FlushAsync(cancellationToken);
                destination.Position = 0;
                hash = Convert.ToHexString(await SHA256.HashDataAsync(destination, cancellationToken)).ToLowerInvariant();
                if (options.Value.SecurityMode == "Enforced")
                {
                    destination.Position = 0;
                    await (scanner ?? throw new MalwareScanUnavailableException()).ScanAsync(destination, total, cancellationToken);
                    destination.Position = 0;
                    if (hash != Convert.ToHexString(await SHA256.HashDataAsync(destination, cancellationToken)).ToLowerInvariant())
                        throw new MalwareScanUnavailableException();
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNoReparsePoints(root, key);
            EnsureNoReparsePoints(quarantine, temporaryKey);
            File.Move(temporary, path, overwrite: false);
            ownsTemporary = false;
            return new StoredFile(key, path, declaredSize, hash, options.Value.SecurityMode == "Enforced"
                ? new FileScanReceipt(hash, declaredSize, "clamav-instream-v1") : null);
        }
        catch
        {
            if (ownsTemporary) { EnsureNoReparsePoints(quarantine, temporaryKey); File.Delete(temporary); }
            throw;
        }
        finally
        {
            lease?.Dispose();
            if (leasePath is not null)
            {
                try { EnsureNoReparsePoints(quarantine, QuarantineFiles.LeaseKey(temporaryKey)); File.Delete(leasePath); }
                catch (IOException) { /* A cleanup reader may briefly own the completed lease. */ }
            }
        }
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(options.Value.UploadDirectory);
        var path = ResolveUnderRoot(root, storageKey);
        EnsureNoReparsePoints(root, storageKey);
        File.Delete(path);
        var directory = Path.GetDirectoryName(path);
        while (directory is not null && !string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
        return Task.CompletedTask;
    }

    internal static string ResolveDocumentSourcePath(string root, Guid organizationId, Guid documentId, string storageKey)
    {
        var prefix = $"organizations/{organizationId:N}/documents/{documentId:N}/source.";
        if (organizationId == Guid.Empty || documentId == Guid.Empty || !storageKey.StartsWith(prefix, StringComparison.Ordinal) ||
            storageKey[prefix.Length..].Length is < 1 or > 9 ||
            storageKey[prefix.Length..].Any(c => !char.IsAsciiLetterOrDigit(c))) throw new InvalidDataException();
        root = Path.GetFullPath(root);
        var path = ResolveUnderRoot(root, storageKey);
        EnsureNoReparsePoints(root, storageKey);
        return path;
    }

    internal static string ResolveUnderRoot(string root, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains('\\') ||
            Path.IsPathRooted(storageKey) || storageKey.Any(char.IsControl) || storageKey.Contains(':') ||
            storageKey.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Storage key must be relative.");
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var path = Path.GetFullPath(Path.Combine(normalizedRoot,
            storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Storage key escapes the configured root.");
        return path;
    }

    private static void CreateSafeParent(string root, string storageKey)
    {
        EnsureNoReparsePoints(root, storageKey);
        Directory.CreateDirectory(root);
        var current = root;
        foreach (var part in storageKey.Split('/').SkipLast(1))
        {
            current = Path.Combine(current, part);
            if (Directory.Exists(current) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Storage key contains a reparse point.");
            Directory.CreateDirectory(current);
        }
    }

    internal static void EnsureNoReparsePoints(string root, string storageKey)
    {
        for (var ancestor = new DirectoryInfo(Path.GetFullPath(root)); ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.Exists && ancestor.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Storage ancestors cannot be reparse points.");
        var current = root;
        foreach (var part in storageKey.Split('/'))
        {
            current = Path.Combine(current, part);
            if ((Directory.Exists(current) || File.Exists(current)) &&
                File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Storage key contains a reparse point.");
        }
    }
}
