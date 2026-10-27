using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Api;
using UaeDocumentIntelligence.Api.Security;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private static SourceKeyProtectionOptions SourceKeys(string root)
    {
        var directory = Path.Combine(root, "keys"); Directory.CreateDirectory(directory);
        var path = Path.Combine(root, "wrapping.pfx");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Disposable source fixture", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, "fixture-password"));
        return new() { Mode = "Certificate", ApplicationName = "fixture-source-grants", KeyDirectory = directory,
            CertificatePath = path, CertificatePassword = "fixture-password" };
    }
    private static IWebHostBuilder WithSourceKeys(IWebHostBuilder builder, SourceKeyProtectionOptions options) => builder
        .UseSetting("SourceKeyProtection:Mode", options.Mode).UseSetting("SourceKeyProtection:ApplicationName", options.ApplicationName)
        .UseSetting("SourceKeyProtection:KeyDirectory", options.KeyDirectory).UseSetting("SourceKeyProtection:CertificatePath", options.CertificatePath)
        .UseSetting("SourceKeyProtection:CertificatePassword", options.CertificatePassword);
    private static HttpClient SourceHttps(WebApplicationFactory<global::Program> host, SourceFixture fixture)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = fixture.User.DefaultRequestHeaders.Authorization; return client;
    }

    [Fact]
    public async Task Phase6_source_certificate_grants_cross_hosts_and_restart_but_preserve_membership_and_application_isolation()
    {
        using var f = await SourceFixtureAsync(); var options = SourceKeys(f.Root);
        DocumentSourceEndpoints.AccessGrant grant;
        using (var first = f.Host.WithWebHostBuilder(b => WithSourceKeys(b.UseEnvironment("Production"), options)))
        using (var client = SourceHttps(first, f))
        {
            var issued = await client.PostAsync($"/api/documents/{f.DocumentId}/source-access", null);
            Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
            grant = (await issued.Content.ReadFromJsonAsync<DocumentSourceEndpoints.AccessGrant>())!;
        }
        using var second = f.Host.WithWebHostBuilder(b => WithSourceKeys(b.UseEnvironment("Production"), options));
        using var user = SourceHttps(second, f);
        using (var request = SourceRequest(grant))
        {
            var response = await user.SendAsync(request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(SourceFixture.Content, await response.Content.ReadAsStringAsync());
        }
        using var isolated = f.Host.WithWebHostBuilder(b => WithSourceKeys(b.UseEnvironment("Production"), options)
            .UseSetting("SourceKeyProtection:ApplicationName", "other-application"));
        using var stranger = SourceHttps(isolated, f);
        using (var request = SourceRequest(grant)) Assert.Equal(HttpStatusCode.NotFound, (await stranger.SendAsync(request)).StatusCode);
        Assert.Equal(1, f.Scanner.Calls);
        Assert.Equal(HttpStatusCode.OK, (await SecurityChange(f.Admin, f.Member, "Staff", false, false)).StatusCode);
        using (var request = SourceRequest(grant)) Assert.Equal(HttpStatusCode.Forbidden, (await user.SendAsync(request)).StatusCode);
        Assert.Equal(1, f.Scanner.Calls);
        await using var db = _database.CreateContext();
        Assert.Equal(1, await db.SecurityAuditEvents.CountAsync(e => e.TargetId == f.DocumentId && e.Action == "document.source-download-authorized"));
    }

    [Theory]
    [InlineData("Locked")]
    [InlineData("Development")]
    [InlineData("Certificate")]
    public async Task Phase6_source_production_refuses_missing_or_unusable_key_configuration(string mode)
    {
        using var f = await SourceFixtureAsync();
        using var host = f.Host.WithWebHostBuilder(b => b.UseEnvironment("Production").UseSetting("SourceKeyProtection:Mode", mode));
        using var user = SourceHttps(host, f);
        var response = await user.PostAsync($"/api/documents/{f.DocumentId}/source-access", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.Equal(0, f.Scanner.Calls);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync());
        await using var db = _database.CreateContext();
        Assert.False(await db.SecurityAuditEvents.AnyAsync(e => e.Action == "document.source-access-issued"));
    }
}
