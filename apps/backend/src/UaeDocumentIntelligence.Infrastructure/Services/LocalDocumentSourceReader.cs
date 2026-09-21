using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;

namespace UaeDocumentIntelligence.Infrastructure.Services;

internal sealed class LocalDocumentSourceReader(IOptions<StorageOptions> options, IMalwareScanner scanner) : IDocumentSourceReader
{
    public async Task<byte[]> ReadVerifiedAsync(DocumentSource source, CancellationToken ct)
    {
        if (options.Value.SecurityMode != "Enforced") throw new MalwareScanUnavailableException();
        byte[] bytes;
        try
        {
            if (source.SizeBytes is < 1 or > 10485760) throw new InvalidDataException();
            var path = LocalDocumentStorage.ResolveDocumentSourcePath(options.Value.UploadDirectory,
                source.OrganizationId, source.DocumentId, source.StorageKey);
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            if (file.Length != source.SizeBytes) throw new InvalidDataException();
            bytes = new byte[(int)source.SizeBytes];
            await file.ReadExactlyAsync(bytes, ct);
            if (await file.ReadAsync(new byte[1], ct) != 0 ||
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != source.Sha256) throw new InvalidDataException();
        }
        catch (OperationCanceledException) { throw; }
        catch { throw new DocumentSourceUnavailableException(); }
        // Scan the same bounded snapshot that will be returned, never reopen a path after authorization.
        using var content = new MemoryStream(bytes, writable: false);
        await scanner.ScanAsync(content, bytes.Length, ct);
        return bytes;
    }
}
