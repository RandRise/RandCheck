using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Application.Documents.Processing;

// Intent identity is independent from the request's tracing/correlation identity.
public sealed record ProcessingIntent(Guid ActorId, string Key, string PayloadHash)
{
    public static string? ValidateKey(string? key)
    {
        if (key is null) return null;
        if (!Regex.IsMatch(key, "\\A[A-Za-z0-9._:-]{1,100}\\z"))
            throw new ProcessingIntentException("invalid_operation_key", "An operation key must contain 1 to 100 safe characters.", false);
        return key;
    }

    public static string Hash(params object[] parts) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(parts))).ToLowerInvariant();
}

public sealed class ProcessingIntentException(string code, string detail, bool conflict = true) : Exception(detail)
{
    public string Code { get; } = code;
    public bool Conflict { get; } = conflict;
}
