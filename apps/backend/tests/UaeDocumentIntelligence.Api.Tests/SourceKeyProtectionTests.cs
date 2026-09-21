using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed class SourceKeyProtectionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "p6-source-keys-" + Guid.NewGuid().ToString("N"));
    public SourceKeyProtectionTests() { Directory.CreateDirectory(Keys); }
    private string Keys => Path.Combine(root, "keys");
    private string Certificate(string name, bool expired = false, bool publicOnly = false)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Disposable source key test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(expired ? -1 : 7));
        var path = Path.Combine(root, name + ".pfx");
        File.WriteAllBytes(path, cert.Export(publicOnly ? X509ContentType.Cert : X509ContentType.Pfx, publicOnly ? null : "fixture-password"));
        return path;
    }
    private IConfiguration Config(string certificate, string app = "fixture-source-grants", string? previous = null)
    {
        var values = new Dictionary<string, string?> {
            ["SourceKeyProtection:Mode"] = "Certificate", ["SourceKeyProtection:KeyDirectory"] = Keys,
            ["SourceKeyProtection:ApplicationName"] = app, ["SourceKeyProtection:CertificatePath"] = certificate,
            ["SourceKeyProtection:CertificatePassword"] = "fixture-password"
        };
        if (previous is not null) { values["SourceKeyProtection:PreviousCertificates:0:Path"] = previous; values["SourceKeyProtection:PreviousCertificates:0:Password"] = "fixture-password"; }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
    private static SourceKeyProtection Provider(IConfiguration config) => new(config, "Production", new EphemeralDataProtectionProvider());
    [Fact]
    public void Encrypted_keys_survive_restart_and_share_grants_only_with_the_same_application()
    {
        var certificate = Certificate("current"); var config = Config(certificate); var clock = new Clock();
        var source = new DocumentSource(Guid.NewGuid(), Guid.NewGuid(), "source.txt", new string('a', 64), 10);
        string grant;
        using (var first = Provider(config)) grant = new SourceAccessTokens(first, clock).Issue(source, Guid.NewGuid(), Guid.NewGuid()).Token;
        var xml = XDocument.Load(Assert.Single(Directory.GetFiles(Keys, "*.xml")));
        Assert.NotEmpty(xml.Descendants(XName.Get("encryptedSecret", "http://schemas.asp.net/2015/03/dataProtection"))); Assert.Empty(xml.Descendants("masterKey"));
        using var second = Provider(config);
        Assert.Equal(source.DocumentId, new SourceAccessTokens(second, clock).Read(grant)!.DocumentId);
        using var isolated = Provider(Config(certificate, "different-application"));
        Assert.Null(new SourceAccessTokens(isolated, clock).Read(grant));
        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        Assert.Null(new SourceAccessTokens(second, clock).Read(grant));
    }
    [Fact]
    public void Certificate_rotation_keeps_old_grants_and_encrypts_new_keys_to_the_new_certificate()
    {
        var oldPath = Certificate("old"); var newPath = Certificate("new");
        string oldToken;
        using (var old = Provider(Config(oldPath))) oldToken = old.CreateProtector("test-purpose").Protect("old grant");
        var rotatedConfig = Config(newPath, previous: oldPath);
        using (var rotated = Provider(rotatedConfig)) Assert.Equal("old grant", rotated.CreateProtector("test-purpose").Unprotect(oldToken));
        // Force a normal framework key rollover without waiting 90 days.
        using var newCertificate = new X509Certificate2(newPath, "fixture-password", X509KeyStorageFlags.EphemeralKeySet);
        using var oldCertificate = new X509Certificate2(oldPath, "fixture-password", X509KeyStorageFlags.EphemeralKeySet);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Keys)).SetApplicationName("fixture-source-grants")
            .ProtectKeysWithCertificate(newCertificate).UnprotectKeysWithAnyCertificate(newCertificate, oldCertificate);
        using (var rotation = services.BuildServiceProvider()) rotation.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));
        using var restarted = Provider(rotatedConfig);
        Assert.Equal("old grant", restarted.CreateProtector("test-purpose").Unprotect(oldToken));
        var token = restarted.CreateProtector("test-purpose").Protect("new grant");
        using var another = Provider(rotatedConfig); Assert.Equal("new grant", another.CreateProtector("test-purpose").Unprotect(token));
        Assert.Equal(2, Directory.GetFiles(Keys, "*.xml").Length);
        using var missingOld = Provider(Config(newPath));
        Assert.Throws<CryptographicException>(() => missingOld.CreateProtector("test-purpose").Protect("must not replace unreadable history"));
    }
    [Theory]
    [InlineData("expired")]
    [InlineData("public-only")]
    [InlineData("missing-certificate")]
    [InlineData("missing-directory")]
    [InlineData("wrong-password")]
    [InlineData("plaintext")]
    [InlineData("malformed")]
    [InlineData("dtd")]
    [InlineData("oversized")]
    [InlineData("duplicate-certificate")]
    public void Unusable_key_resources_fail_closed_without_key_or_path_diagnostics(string scenario)
    {
        var certificate = Certificate("current", scenario == "expired", scenario == "public-only");
        var config = Config(certificate);
        if (scenario == "missing-certificate") File.Delete(certificate);
        if (scenario == "missing-directory") Directory.Delete(Keys);
        if (scenario == "wrong-password") config["SourceKeyProtection:CertificatePassword"] = "PRIVATE wrong password";
        if (scenario == "plaintext") File.WriteAllText(Path.Combine(Keys, "key-unsafe.xml"), "<key><descriptor><masterKey>PRIVATE</masterKey></descriptor></key>");
        if (scenario == "malformed") File.WriteAllText(Path.Combine(Keys, "key-broken.xml"), "<broken>");
        if (scenario == "dtd") File.WriteAllText(Path.Combine(Keys, "key-unsafe.xml"), "<!DOCTYPE key [<!ENTITY unsafe SYSTEM 'file:///PRIVATE'>]><key>&unsafe;</key>");
        if (scenario == "oversized") File.WriteAllText(Path.Combine(Keys, "key-unsafe.xml"), new string('X', 65537));
        if (scenario == "duplicate-certificate") { config["SourceKeyProtection:PreviousCertificates:0:Path"] = certificate; config["SourceKeyProtection:PreviousCertificates:0:Password"] = "fixture-password"; }
        using var provider = Provider(config);
        var error = Assert.Throws<CryptographicException>(() => provider.CreateProtector("purpose").Protect("PRIVATE payload"));
        Assert.DoesNotContain(root, error.ToString()); Assert.DoesNotContain("PRIVATE", error.ToString());
        Assert.False(Directory.Exists(Keys) && Directory.GetFiles(Keys).Any(p => !p.EndsWith("unsafe.xml") && !p.EndsWith("broken.xml")));
    }
    [Fact]
    public void Resource_verification_is_read_only_and_missing_live_directory_does_not_fall_back_to_cached_keys()
    {
        var config = Config(Certificate("current")); var options = SourceKeyProtectionOptions.Read(config, "Production");
        SourceKeyProtection.VerifyResources(options, config); Assert.Empty(Directory.GetFiles(Keys));
        using var provider = Provider(config); provider.CreateProtector("purpose").Protect("test");
        var files = Directory.GetFiles(Keys).ToDictionary(p => p, File.ReadAllText);
        SourceKeyProtection.VerifyResources(options, config);
        Assert.All(files, f => Assert.Equal(f.Value, File.ReadAllText(f.Key)));
        // Rename inside this disposable root; simulate losing the configured mount.
        Directory.Move(Keys, Path.Combine(root, "offline-keys"));
        Assert.Throws<CryptographicException>(() => provider.CreateProtector("purpose").Protect("test"));
        Assert.False(Directory.Exists(Keys));
    }
    [Theory]
    [InlineData("Production", null, "Locked")]
    [InlineData("Staging", "Development", "Locked")]
    [InlineData("Testing", null, "Development")]
    [InlineData("Development", "Certificate", "Certificate")]
    public void Only_local_profiles_allow_default_key_protection(string environment, string? mode, string expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SourceKeyProtection:Mode"] = mode }).Build();
        Assert.Equal(expected, SourceKeyProtectionOptions.Read(config, environment).Mode);
        if (expected == "Locked")
        {
            using var provider = new SourceKeyProtection(config, environment, new EphemeralDataProtectionProvider());
            Assert.Throws<CryptographicException>(() => provider.CreateProtector("purpose").Protect("test"));
        }
    }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow; }
    [Fact]
    public void Malformed_binding_defers_a_safe_refusal_until_grant_protection()
    {
        var config = Config(Certificate("current")); config["SourceKeyProtection:KeyLifetimeDays"] = "PRIVATE invalid integer";
        using var provider = Provider(config);
        var error = Assert.Throws<CryptographicException>(() => provider.CreateProtector("purpose").Protect("test"));
        Assert.DoesNotContain("PRIVATE", error.ToString()); Assert.Empty(Directory.GetFiles(Keys));
    }
    [Theory]
    [InlineData("relative")]
    [InlineData("name")]
    [InlineData("lifetime")]
    [InlineData("content-overlap")]
    [InlineData("certificate-in-ring")]
    public void Unsafe_configuration_is_rejected_before_creating_keys(string scenario)
    {
        var config = Config(Certificate("current"));
        if (scenario == "relative") config["SourceKeyProtection:KeyDirectory"] = "relative-keys";
        if (scenario == "name") config["SourceKeyProtection:ApplicationName"] = "shared app\n";
        if (scenario == "lifetime") config["SourceKeyProtection:KeyLifetimeDays"] = "1";
        if (scenario == "content-overlap") config["Storage:UploadDirectory"] = root;
        if (scenario == "certificate-in-ring") config["SourceKeyProtection:CertificatePath"] = Path.Combine(Keys, "private.pfx");
        Assert.Throws<InvalidOperationException>(() => SourceKeyProtectionOptions.Read(config, "Production").Validate(config));
        Assert.Empty(Directory.GetFiles(Keys));
    }
    public void Dispose()
    {
        var path = Path.GetFullPath(root);
        if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(path).StartsWith("p6-source-keys-")) throw new InvalidOperationException();
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
