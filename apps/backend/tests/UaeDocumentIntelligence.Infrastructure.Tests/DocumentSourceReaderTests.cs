using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class DocumentSourceReaderTests
{
    [Theory]
    [InlineData("snapshot")]
    [InlineData("organization")]
    [InlineData("document")]
    [InlineData("locked")]
    public async Task Reader_returns_only_the_scanned_snapshot_and_rejects_identity_substitution(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "p6-source-reader-" + Guid.NewGuid().ToString("N"));
        try
        {
            var organization = Guid.NewGuid(); var document = Guid.NewGuid();
            var key = $"organizations/{organization:N}/documents/{document:N}/source.txt";
            var path = Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bytes = "original snapshot"u8.ToArray(); await File.WriteAllBytesAsync(path, bytes);
            var scanner = new SnapshotScanner(path, bytes);
            var reader = new LocalDocumentSourceReader(Options.Create(new StorageOptions
                { SecurityMode = scenario == "locked" ? "Locked" : "Enforced", UploadDirectory = root }), scanner);
            var source = new DocumentSource(scenario == "organization" ? Guid.NewGuid() : organization,
                scenario == "document" ? Guid.NewGuid() : document, key,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Length);
            if (scenario == "snapshot")
            {
                Assert.Equal(bytes, await reader.ReadVerifiedAsync(source, default));
                Assert.Equal("changed after snapshot", await File.ReadAllTextAsync(path));
                Assert.Equal(1, scanner.Calls);
            }
            else
            {
                if (scenario == "locked") await Assert.ThrowsAsync<MalwareScanUnavailableException>(() => reader.ReadVerifiedAsync(source, default));
                else await Assert.ThrowsAsync<DocumentSourceUnavailableException>(() => reader.ReadVerifiedAsync(source, default));
                Assert.Equal(0, scanner.Calls); Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private sealed class SnapshotScanner(string path, byte[] expected) : IMalwareScanner
    {
        public int Calls { get; private set; }
        public async Task ScanAsync(Stream content, long length, CancellationToken ct)
        {
            Calls++; Assert.False(content.CanWrite);
            using var copy = new MemoryStream(); await content.CopyToAsync(copy, ct);
            Assert.Equal(expected, copy.ToArray());
            await File.WriteAllTextAsync(path, "changed after snapshot", ct);
        }
    }
}
