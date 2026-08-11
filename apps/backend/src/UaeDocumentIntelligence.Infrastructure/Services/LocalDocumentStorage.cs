using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;

namespace UaeDocumentIntelligence.Infrastructure.Services;

public sealed class StorageOptions
{
    public string UploadDirectory { get; set; } = "./data/uploads";
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
}

internal sealed class LocalDocumentStorage(IOptions<StorageOptions> options) : IDocumentStorage
{
    public async Task<StoredFile> SaveAsync(Stream content, string originalFilename, long declaredSize,
        Guid organizationId, Guid documentId, CancellationToken cancellationToken)
    {
        if (declaredSize <= 0 || declaredSize > options.Value.MaxUploadBytes) throw new DocumentTooLargeException();
        var root = Path.GetFullPath(options.Value.UploadDirectory);
        var extension = Path.GetExtension(originalFilename).ToLowerInvariant();
        if (extension.Length is < 2 or > 10 || extension.Skip(1).Any(character => !char.IsLetterOrDigit(character)))
            extension = ".bin";
        var key = Path.Combine("organizations", organizationId.ToString("N"), "documents",
            documentId.ToString("N"), $"source{extension}").Replace('\\', '/');
        var path = ResolveUnderRoot(root, key);
        CreateSafeParent(root, key);
        try
        {
            await using (var destination = File.Create(path))
                await content.CopyToAsync(destination, cancellationToken);
            var actualSize = new FileInfo(path).Length;
            if (actualSize <= 0 || actualSize > options.Value.MaxUploadBytes || actualSize != declaredSize)
                throw new DocumentTooLargeException();
            await using var saved = File.OpenRead(path);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(saved, cancellationToken)).ToLowerInvariant();
            return new StoredFile(key, path, actualSize, hash);
        }
        catch
        {
            File.Delete(path);
            throw;
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

    internal static string ResolveUnderRoot(string root, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains('\\') ||
            Path.IsPathRooted(storageKey))
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
        Directory.CreateDirectory(root);
        if (File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Storage root cannot be a reparse point.");
        var current = root;
        foreach (var part in storageKey.Split('/').SkipLast(1))
        {
            current = Path.Combine(current, part);
            if (Directory.Exists(current) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Storage key contains a reparse point.");
            Directory.CreateDirectory(current);
        }
    }

    private static void EnsureNoReparsePoints(string root, string storageKey)
    {
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
