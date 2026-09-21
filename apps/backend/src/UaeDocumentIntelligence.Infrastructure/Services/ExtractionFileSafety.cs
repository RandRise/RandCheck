using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;

namespace UaeDocumentIntelligence.Infrastructure.Services;

internal sealed class ExtractionFileSafety(IOptions<StorageOptions> options, IMalwareScanner scanner)
{
    internal async Task VerifyAsync(ClaimedExtractionJob claim, CancellationToken ct)
    {
        try
        {
            if (options.Value.SecurityMode != "Enforced") throw new MalwareScanUnavailableException();
            var path = LocalDocumentStorage.ResolveDocumentSourcePath(options.Value.UploadDirectory,
                claim.OrganizationId, claim.DocumentId, claim.StorageKey);
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
            if (file.Length != claim.Bytes || claim.Bytes is < 1 or > 10485760) throw new InvalidDataException();
            if (Convert.ToHexString(await SHA256.HashDataAsync(file, ct)).ToLowerInvariant() != claim.Sha256) throw new InvalidDataException();
            file.Position = 0;
            await scanner.ScanAsync(file, file.Length, ct);
            file.Position = 0;
            if (Convert.ToHexString(await SHA256.HashDataAsync(file, ct)).ToLowerInvariant() != claim.Sha256) throw new InvalidDataException();
        }
        catch (OperationCanceledException) { throw; }
        catch (MalwareDetectedException) { throw new ExtractionProviderException("malware_detected", "The source file did not pass malware screening."); }
        catch (MalwareScanUnavailableException) { throw new ExtractionProviderException("malware_scan_unavailable", "Malware screening is unavailable.", true); }
        catch { throw new ExtractionProviderException("storage_integrity_failed", "The source file failed integrity verification."); }
    }
}
