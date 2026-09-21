using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace UaeDocumentIntelligence.Api.Tests;

internal static class DevelopmentIdentityTestHost
{
    // Existing Phase 1–5 contract fixtures exercise the deliberate local profile.
    // EnterpriseSecurityTests independently exercises the real bearer boundary.
    public static IWebHostBuilder UsePhase5DevelopmentIdentity(this IWebHostBuilder builder) =>
        builder.UseEnvironment("Testing").UseSetting("Security:Mode", "Development");
}
