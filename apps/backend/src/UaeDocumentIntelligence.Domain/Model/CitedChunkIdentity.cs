using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace UaeDocumentIntelligence.Domain.Model;

/// <summary>Contract 1.0: first 128 SHA-256 bits, rendered as a GUID (not UUIDv4).</summary>
public static class CitedChunkIdentity
{
    public static Guid Chunk(Guid indexId, int ordinal, DocumentChunkKind kind, string contentSha256) =>
        HashId(FormattableString.Invariant($"chunk:{indexId:N}:{ordinal}:{kind}:{contentSha256}"));

    public static Guid Span(Guid chunkId, int pageNumber, int start, int end, Guid? evidenceId) =>
        HashId(string.Create(CultureInfo.InvariantCulture,
            $"span:{chunkId:N}:{pageNumber}:{start}:{end}:{evidenceId?.ToString("N") ?? "-"}"));

    private static Guid HashId(string name) => Guid.ParseExact(
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..32], "N");
}
