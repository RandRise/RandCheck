using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Api.Security;

public sealed record SourceAccessPayload(Guid OrganizationId, Guid ActorId, Guid MembershipVersion,
    Guid DocumentId, string Sha256, long SizeBytes, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc);

public sealed class SourceAccessTokens(IDataProtectionProvider protection, IClock clock)
{
    private readonly IDataProtector _protector = protection.CreateProtector("UaeDocumentIntelligence.SourceAccess.v1");
    public (string Token, DateTimeOffset ExpiresAtUtc) Issue(DocumentSource source, Guid actorId, Guid membershipVersion)
    {
        var now = clock.UtcNow;
        var payload = new SourceAccessPayload(source.OrganizationId, actorId, membershipVersion,
            source.DocumentId, source.Sha256, source.SizeBytes, now, now.AddMinutes(5));
        return (_protector.Protect(JsonSerializer.Serialize(payload)), payload.ExpiresAtUtc);
    }

    public SourceAccessPayload? Read(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 4096) return null;
        try
        {
            var payload = JsonSerializer.Deserialize<SourceAccessPayload>(_protector.Unprotect(token));
            return payload is not null && IsCurrent(payload) ? payload : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException or FormatException or ArgumentException)
        { return null; }
    }

    public bool IsCurrent(SourceAccessPayload payload) => payload.IssuedAtUtc <= clock.UtcNow &&
        payload.ExpiresAtUtc > clock.UtcNow && payload.ExpiresAtUtc - payload.IssuedAtUtc == TimeSpan.FromMinutes(5);
}
