using Microsoft.AspNetCore.DataProtection;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed class SourceAccessTokenTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("tampered")]
    [InlineData("expired")]
    [InlineData("before-issue")]
    [InlineData("different-key")]
    [InlineData("wrong-purpose")]
    [InlineData("oversized")]
    [InlineData("missing")]
    public void Grant_requires_intact_protection_and_exact_five_minute_window(string scenario)
    {
        var protection = new EphemeralDataProtectionProvider(); var clock = new SourceClock();
        var tokens = new SourceAccessTokens(protection, clock);
        var source = new DocumentSource(Guid.NewGuid(), Guid.NewGuid(), "PRIVATE PATH", new string('a', 64), 4);
        var actor = Guid.NewGuid(); var version = Guid.NewGuid();
        var issued = tokens.Issue(source, actor, version); var value = issued.Token;
        Assert.Equal(clock.UtcNow.AddMinutes(5), issued.ExpiresAtUtc);
        Assert.DoesNotContain("PRIVATE", value);
        if (scenario == "tampered") value = value[..30] + (value[30] == 'a' ? 'b' : 'a') + value[31..];
        if (scenario == "expired") clock.UtcNow = issued.ExpiresAtUtc;
        if (scenario == "before-issue") clock.UtcNow = clock.UtcNow.AddSeconds(-1);
        if (scenario == "different-key") tokens = new SourceAccessTokens(new EphemeralDataProtectionProvider(), clock);
        if (scenario == "wrong-purpose") value = protection.CreateProtector("unrelated").Protect("{}");
        if (scenario == "oversized") value = new string('a', 4097);
        if (scenario == "missing") value = "";
        var read = tokens.Read(value);
        if (scenario != "valid") Assert.Null(read);
        else
        {
            Assert.NotNull(read); Assert.Equal(source.DocumentId, read.DocumentId);
            Assert.Equal(source.OrganizationId, read.OrganizationId); Assert.Equal(actor, read.ActorId);
            Assert.Equal(version, read.MembershipVersion); Assert.Equal(source.Sha256, read.Sha256);
        }
    }
    private sealed class SourceClock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow; }
}
