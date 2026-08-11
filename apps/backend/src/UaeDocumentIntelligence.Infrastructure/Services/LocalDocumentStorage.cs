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
        CancellationToken cancellationToken)
    {
        if (declaredSize <= 0 || declaredSize > options.Value.MaxUploadBytes) throw new DocumentTooLargeException();
        Directory.CreateDirectory(options.Value.UploadDirectory);
        var extension = Path.GetExtension(originalFilename).ToLowerInvariant();
        if (extension.Length > 10) extension = string.Empty;
        var key = $"{Guid.NewGuid()}{extension}";
        var path = Path.GetFullPath(Path.Combine(options.Value.UploadDirectory, key));
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
        var path = Path.GetFullPath(Path.Combine(root, storageKey));
        if (Path.GetDirectoryName(path) == root) File.Delete(path);
        return Task.CompletedTask;
    }
}
