using System.Text.Json;
using System.Text.Json.Nodes;
using UaeDocumentIntelligence.Api.Deployment;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed class EnterpriseReadinessTests
{
    internal static ReadinessInput Candidate()
    {
        var root = Path.Combine(Path.GetTempPath(), "readiness-candidate");
        var backend = JsonSerializer.SerializeToElement(new {
            Security = new { Mode = "Entra", TenantId = "00000000-0000-4000-8000-000000000001", Audience = "00000000-0000-4000-8000-000000000002",
                RequiredScope = "DocumentIntelligence.Access", AllowedClientIds = new[] { "00000000-0000-4000-8000-000000000003" } },
            ApiDatabase = new { Enabled = true, DatabaseRole = "enterprise_api" },
            ConnectionStrings = new { Database = "Host=db.invalid;Database=enterprise;Username=enterprise_api;Password=PRIVATE_MARKER;SSL Mode=VerifyFull",
                WorkerDatabase = "Host=db.invalid;Database=enterprise;Username=enterprise_worker;Password=PRIVATE_MARKER;SSL Mode=VerifyFull" },
            ExtractionWorker = new { Enabled = false, SecurityMode = "Enforced", DatabaseRole = "enterprise_worker" },
            Storage = new { SecurityMode = "Enforced", UploadDirectory = Path.Combine(root, "uploads"), QuarantineDirectory = Path.Combine(root, "quarantine") },
            MalwareScan = new { Mode = "ClamAv", Address = "127.0.0.1", Port = 3310, TimeoutSeconds = 30 },
            SourceKeyProtection = new { Mode = "Certificate", ApplicationName = "fixture-source-grants", KeyDirectory = Path.Combine(root, "keys"), CertificatePath = Path.Combine(root, "wrapping.pfx") },
            AiService = new { AuthenticationMode = "ServiceKeys", BaseUrl = "https://service.invalid", ExtractionCredential = new { KeyId = "extract", Token = new string('A', 43) },
                CitedCredential = new { KeyId = "cited", Token = new string('B', 43) }, AnswerCredential = new { KeyId = "answer", Token = new string('C', 43) } },
            Cors = new { Origins = new[] { "https://web.invalid" } }, AllowedHosts = "api.invalid", DocumentDeletion = new { Enabled = false } });
        return new("Production", backend, new() { ["AUTH_MODE"] = "Entra", ["NEXT_PUBLIC_ENTRA_TENANT_ID"] = "00000000-0000-4000-8000-000000000001",
            ["NEXT_PUBLIC_ENTRA_CLIENT_ID"] = "00000000-0000-4000-8000-000000000003", ["NEXT_PUBLIC_ENTRA_SCOPE"] = "api://00000000-0000-4000-8000-000000000002/DocumentIntelligence.Access",
            ["NEXT_PUBLIC_API_URL"] = "https://api.invalid", ["WEB_ORIGIN"] = "https://web.invalid" }, "enterprise");
    }
    [Fact]
    public void Valid_configuration_is_not_a_live_deployment_verification_and_does_not_expose_values()
    {
        var report = EnterpriseReadiness.Inspect(Candidate());
        Assert.True(report.ConfigurationValid); Assert.False(report.DeploymentVerified); Assert.Equal("VerificationRequired", report.Status);
        Assert.Equal(7, report.Checks.Count(c => c.Status == "VerificationRequired"));
        var json = JsonSerializer.Serialize(report);
        foreach (var marker in new[] { "PRIVATE_MARKER", "db.invalid", "web.invalid", new string('A', 43), Path.GetTempPath() }) Assert.DoesNotContain(marker, json);
    }
    [Theory]
    [InlineData("Security", "Mode", "Development", "identity.api")]
    [InlineData("Security", "RequiredScope", "Other.Scope", "identity.browser")]
    [InlineData("ApiDatabase", "DatabaseRole", "wrong_role", "database.api-profile")]
    [InlineData("ExtractionWorker", "SecurityMode", "Development", "database.worker-profile")]
    [InlineData("Storage", "SecurityMode", "Development", "storage.profile")]
    [InlineData("Storage", "UploadDirectory", "relative/uploads", "storage.profile")]
    [InlineData("MalwareScan", "Address", "scanner.invalid", "scanner.profile")]
    [InlineData("AiService", "AuthenticationMode", "Development", "service.transport")]
    [InlineData("AiService", "BaseUrl", "http://service.invalid", "service.transport")]
    [InlineData("AiService", "BaseUrl", "https://service.invalid/private", "service.transport")]
    [InlineData("AiService", "BaseUrl", "https://secret@service.invalid", "service.transport")]
    public void Configuration_mismatches_have_fixed_blockers(string section, string key, string value, string check)
    {
        var input = Candidate(); var backend = JsonNode.Parse(input.Backend.GetRawText())!; backend[section]![key] = value;
        var report = EnterpriseReadiness.Inspect(input with { Backend = JsonSerializer.SerializeToElement(backend) });
        Assert.Equal("Blocked", report.Checks.Single(c => c.Id == check).Status); Assert.False(report.DeploymentVerified);
    }
    [Theory]
    [InlineData("RequestLimits", "Enabled", false, "request-limits")]
    [InlineData("ApiDatabase", "Enabled", false, "database.api-profile")]
    [InlineData("DevelopmentData", "Seed", true, "development.disabled")]
    [InlineData("StorageMaintenance", "Enabled", true, "development.disabled")]
    [InlineData("DocumentDeletion", "Enabled", true, "recovery.profile")]
    public void Enterprise_flags_fail_closed(string section, string key, bool value, string check)
    {
        var input = Candidate(); var backend = JsonNode.Parse(input.Backend.GetRawText())!;
        backend[section] ??= new JsonObject(); backend[section]![key] = value;
        Assert.Equal("Blocked", EnterpriseReadiness.Inspect(input with { Backend = JsonSerializer.SerializeToElement(backend) }).Checks.Single(c => c.Id == check).Status);
    }
    [Theory]
    [InlineData("AUTH_MODE", "Development", "identity.browser")]
    [InlineData("NEXT_PUBLIC_ENTRA_CLIENT_ID", "00000000-0000-4000-8000-000000000004", "identity.browser")]
    [InlineData("NEXT_PUBLIC_ENTRA_SCOPE", "api://00000000-0000-4000-8000-000000000004/DocumentIntelligence.Access", "identity.browser")]
    [InlineData("NEXT_PUBLIC_API_URL", "http://api.invalid", "transport.origins")]
    [InlineData("WEB_ORIGIN", "https://other.invalid", "transport.cors-hosts")]
    [InlineData("WEB_ORIGIN", "https://web.invalid/", "transport.origins")]
    public void Browser_configuration_must_match_api_admission(string key, string value, string check)
    {
        var input = Candidate(); input.Browser[key] = value;
        Assert.Equal("Blocked", EnterpriseReadiness.Inspect(input).Checks.Single(c => c.Id == check).Status);
    }
    [Fact]
    public void Matching_but_noncanonical_cors_origins_do_not_pass_preflight()
    {
        var input = Candidate(); input.Browser["WEB_ORIGIN"] = "https://web.invalid/";
        var backend = JsonNode.Parse(input.Backend.GetRawText())!; backend["Cors"]!["Origins"] = new JsonArray("https://web.invalid/");
        Assert.Equal("Blocked", EnterpriseReadiness.Inspect(input with { Backend = JsonSerializer.SerializeToElement(backend) }).Checks.Single(c => c.Id == "transport.cors-hosts").Status);
    }
    [Theory]
    [InlineData("Host=db.invalid;Database=enterprise;Username=enterprise_api;SSL Mode=Require", "enterprise")]
    [InlineData("Host=db.invalid;Database=enterprise;Username=enterprise_api;SSL Mode=VerifyFull;Include Error Detail=true", "enterprise")]
    [InlineData("Host=db.invalid;Database=enterprise;Username=enterprise_api;SSL Mode=VerifyFull;Options=-c statement_timeout=0", "enterprise")]
    [InlineData("Host=127.0.0.1;Database=phase5_preserved;Username=enterprise_api", "phase5_preserved")]
    [InlineData("Host=127.0.0.1;Database=enterprise;Username=enterprise_api", "wrong_database")]
    public void Connection_profiles_refuse_unsafe_or_misdirected_targets(string connection, string expected)
    {
        var input = Candidate(); var backend = JsonNode.Parse(input.Backend.GetRawText())!; backend["ConnectionStrings"]!["Database"] = connection;
        Assert.False(EnterpriseReadiness.ValidConnection(EnterpriseReadiness.Configuration(input with { Backend = JsonSerializer.SerializeToElement(backend) }), expected, false));
    }
    [Fact]
    public async Task Resource_refusals_are_sanitized_and_never_upgrade_a_blocked_profile()
    {
        var input = Candidate() with { ExpectedDatabase = "phase5_preserved" };
        var report = await ReadinessResources.VerifyAsync(input, default);
        Assert.False(report.DeploymentVerified); Assert.Equal("Blocked", report.Status);
        Assert.Equal("Blocked", report.Checks.Single(c => c.Id == "observed.api-grants").Status);
        Assert.DoesNotContain("PRIVATE_MARKER", JsonSerializer.Serialize(report));
    }
}
