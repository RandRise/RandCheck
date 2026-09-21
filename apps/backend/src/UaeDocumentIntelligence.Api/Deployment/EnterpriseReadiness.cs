using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Api.Deployment;

public sealed record ReadinessInput(string Environment, JsonElement Backend,
    Dictionary<string, string?> Browser, string ExpectedDatabase, string? RecoveryControlFile = null);
public sealed record ReadinessCheck(string Id, string Status, string Detail);
public sealed record ReadinessReport(int Schema, string Status, bool ConfigurationValid, bool DeploymentVerified,
    IReadOnlyList<ReadinessCheck> Checks);

// Diagnostic only: never registers services, starts a host or changes configuration.
// Report fields are fixed strings; supplied configuration and exceptions are never echoed.
public static class EnterpriseReadiness
{
    public static IConfiguration Configuration(ReadinessInput input) => new ConfigurationBuilder()
        .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(input.Backend.GetRawText()))).Build();

    public static ReadinessReport Inspect(ReadinessInput input)
    {
        var config = Configuration(input); var checks = new List<ReadinessCheck>();
        void Check(string id, Func<bool> predicate, string detail)
        {
            bool passed; try { passed = predicate(); } catch { passed = false; }
            checks.Add(new(id, passed ? "Pass" : "Blocked", detail));
        }
        string? Browser(string key) => input.Browser.GetValueOrDefault(key);
        var environment = new ReadinessEnvironment(input.Environment);
        EnterpriseSecurityOptions Security() => config.GetSection("Security").Get<EnterpriseSecurityOptions>() ?? new();
        Check("environment", () => input.Environment is "Production" or "Staging", "Evaluate an explicit Production or Staging configuration.");
        Check("identity.api", () => { var s = Security(); s.Validate(environment); return s.Mode == "Entra"; },
            "API Entra mode requires a tenant, audience, delegated scope and allowed client IDs.");
        Check("identity.browser", () => {
            var s = Security(); return Browser("AUTH_MODE") == "Entra" && Guid.TryParse(Browser("NEXT_PUBLIC_ENTRA_TENANT_ID"), out var tenant) &&
                tenant != Guid.Empty && tenant == s.TenantId && Guid.TryParse(Browser("NEXT_PUBLIC_ENTRA_CLIENT_ID"), out var client) &&
                client != Guid.Empty && s.AllowedClientIds.Contains(client) && s.RequiredScope == "DocumentIntelligence.Access" &&
                string.Equals(Browser("NEXT_PUBLIC_ENTRA_SCOPE"), $"api://{s.Audience:D}/DocumentIntelligence.Access", StringComparison.OrdinalIgnoreCase);
        }, "Browser Entra tenant/client/scope must match the API and its supported browser scope.");
        Check("transport.origins", () => HttpsOrigin(Browser("NEXT_PUBLIC_API_URL")) && CanonicalOrigin(Browser("WEB_ORIGIN")),
            "Declare HTTPS API and browser origins without credentials, paths, query strings or fragments.");
        Check("transport.cors-hosts", () => {
            var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
            var hosts = (config["AllowedHosts"] ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return origins.Length > 0 && origins.All(CanonicalOrigin) && origins.Contains(Browser("WEB_ORIGIN"), StringComparer.OrdinalIgnoreCase) &&
                hosts.Length > 0 && hosts.All(h => !h.Contains('*')) && Uri.TryCreate(Browser("NEXT_PUBLIC_API_URL"), UriKind.Absolute, out var api) &&
                hosts.Contains(api.Host, StringComparer.OrdinalIgnoreCase);
        }, "Use explicit HTTPS CORS origins and an API hostname allowlist; include the intended browser/API hosts.");
        Check("database.api-profile", () => config.GetValue<bool>("ApiDatabase:Enabled") && ValidConnection(config, input.ExpectedDatabase, false),
            "Enable the restricted API gate with its matching dedicated login, exact database, private diagnostics and verified remote TLS.");
        Check("database.worker-profile", () => config["ExtractionWorker:SecurityMode"] == "Enforced" && ValidConnection(config, input.ExpectedDatabase, true),
            "Configure a distinct restricted worker login on the same database with Enforced worker security, even if this API host runs no worker.");
        Check("storage.profile", () => {
            var s = config.GetSection("Storage").Get<StorageOptions>() ?? new();
            return s.SecurityMode == "Enforced" && s.MaxUploadBytes is > 0 and <= 10485760 &&
                Path.IsPathFullyQualified(s.UploadDirectory) && Path.IsPathFullyQualified(s.QuarantineDirectory) &&
                !SamePath(s.UploadDirectory, s.QuarantineDirectory) && SamePath(Path.GetDirectoryName(Normalize(s.UploadDirectory))!, Path.GetDirectoryName(Normalize(s.QuarantineDirectory))!);
        }, "Enforced storage requires absolute, separate sibling source/quarantine roots and a bounded upload size.");
        Check("scanner.profile", () => {
            var s = config.GetSection("MalwareScan").Get<MalwareScanOptions>() ?? new();
            return s.Mode == "ClamAv" && IPAddress.TryParse(s.Address, out var ip) && IPAddress.IsLoopback(ip) &&
                s.Port is >= 1 and <= 65535 && s.TimeoutSeconds is >= 1 and <= 60;
        }, "Configure the supported loopback ClamAV scanner with bounded port and timeout values.");
        Check("service.transport", () => config["AiService:AuthenticationMode"] == "ServiceKeys" && HttpsOrigin(config["AiService:BaseUrl"]) &&
            string.IsNullOrEmpty(config["AiService:WorkerToken"]), "Use ServiceKeys over HTTPS and remove the legacy shared worker token.");
        Check("service.credentials", () => new[] { "ExtractionCredential", "CitedCredential", "AnswerCredential" }.All(name => {
            var s = config.GetSection("AiService:" + name).Get<PrivateServiceCredential>() ?? new();
            return System.Text.RegularExpressions.Regex.IsMatch(s.KeyId, "\\A[A-Za-z0-9_-]{1,64}\\z") &&
                System.Text.RegularExpressions.Regex.IsMatch(s.Token, "\\A[A-Za-z0-9_-]{43,128}\\z");
        }), "Configure the operation credentials for extraction, cited retrieval and answer paths; validity at the receiver still needs verification.");
        Check("request-limits", () => RequestLimitOptions.Read(config, environment).Enabled, "Keep bounded request/body/concurrency admission enabled in enterprise profiles.");
        Check("source-keys.profile", () => { SourceKeyProtectionOptions.Read(config, input.Environment).Validate(config); return true; },
            "Configure certificate-protected source keys with an explicit application name, separate absolute key/certificate paths and bounded rotation settings.");
        Check("development.disabled", () => !config.GetValue<bool>("DevelopmentData:Seed") && !config.GetValue<bool>("StorageMaintenance:Enabled"),
            "Disable development seeding and the legacy orphan maintenance service.");
        Check("recovery.profile", () => {
            if (!config.GetValue<bool>("DocumentDeletion:Enabled")) return true;
            var directory = config["DocumentDeletion:JournalDirectory"] ?? "";
            return Path.IsPathFullyQualified(directory) && Guid.TryParse(config["DocumentDeletion:JournalId"], out var id) && id != Guid.Empty &&
                new[] { config["Storage:UploadDirectory"], config["Storage:QuarantineDirectory"] }.All(p => p is not null && !Overlap(directory, p)) &&
                input.RecoveryControlFile is not null && Path.IsPathFullyQualified(input.RecoveryControlFile) &&
                new[] { directory, config["Storage:UploadDirectory"]!, config["Storage:QuarantineDirectory"]! }.All(p => !Within(input.RecoveryControlFile, p));
        }, "If deletion is enabled, pin a separate journal identity and an independent absolute control-file path.");
        foreach (var item in new[] {
            ("live.identity-tls", "Verify real Entra registration, consent, callback/logout URLs, TLS termination and revocation in the deployed browser/API."),
            ("live.database", "Verify actual API/worker grants, current capacity-policy coverage and System worker actors; configuration alone supplies no catalog evidence."),
            ("live.storage", "Verify existing source/quarantine roots plus API/worker filesystem ACLs, encryption, physical capacity and shared-mount behavior."),
            ("live.scanner-service", "Verify clean and malware rejection, scanner outage behavior, Python HTTPS/service verifier scopes, expiry, rotation and protected telemetry."),
            ("live.keys", "Verify source-key directory and certificate ACLs, protected credential delivery, replica application isolation, rotation and cross-instance source grants on the deployed filesystem."),
            ("live.recovery", "Verify the current independent schema 2 controls/journal, holds, backup expiry and an isolated restore drill."),
            ("live.runtime-edge", "Verify supported patched runtimes, secret delivery, ingress/proxy behavior, distributed limits, monitoring and operational audit retention.") })
            checks.Add(new(item.Item1, "VerificationRequired", item.Item2));
        return Report(checks);
    }
    public static ReadinessReport Report(IReadOnlyList<ReadinessCheck> checks) => new(1,
        checks.Any(c => c.Status == "Blocked") ? "Blocked" : "VerificationRequired",
        !checks.Any(c => c.Status == "Blocked" && !c.Id.StartsWith("observed.", StringComparison.Ordinal)), false, checks);

    public static bool ValidConnection(IConfiguration config, string expectedDatabase, bool worker)
    {
        try
        {
            var role = config[worker ? "ExtractionWorker:DatabaseRole" : "ApiDatabase:DatabaseRole"] ?? "";
            if (worker) _ = WorkerDatabasePolicy.Subject(role); else _ = ApiDatabasePolicy.Subject(role);
            var parsed = new NpgsqlConnectionStringBuilder(config.GetConnectionString(worker ? "WorkerDatabase" : "Database"));
            var api = new NpgsqlConnectionStringBuilder(config.GetConnectionString("Database"));
            var loopback = parsed.Host == "localhost" || IPAddress.TryParse(parsed.Host, out var ip) && IPAddress.IsLoopback(ip);
            return !string.IsNullOrWhiteSpace(expectedDatabase) && !expectedDatabase.StartsWith("phase5_", StringComparison.OrdinalIgnoreCase) &&
                parsed.Database == expectedDatabase && !string.IsNullOrWhiteSpace(parsed.Host) && parsed.Username == role &&
                !parsed.IncludeErrorDetail && !parsed.LogParameters && string.IsNullOrEmpty(parsed.Options) &&
                (loopback || parsed.SslMode == SslMode.VerifyFull) && (!worker || api.Host == parsed.Host && api.Port == parsed.Port &&
                    api.Database == parsed.Database && api.Username != parsed.Username);
        }
        catch { return false; }
    }
    private static bool HttpsOrigin(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        uri.AbsolutePath == "/" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
    private static bool CanonicalOrigin(string? value) => HttpsOrigin(value) &&
        string.Equals(new Uri(value!).GetLeftPart(UriPartial.Authority), value, StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool SamePath(string a, string b) => string.Equals(Normalize(a), Normalize(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static bool Within(string a, string b) => SamePath(a, b) || Normalize(a).StartsWith(Normalize(b) + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static bool Overlap(string a, string b) => Within(a, b) || Within(b, a);
    private sealed class ReadinessEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "EnterpriseReadiness";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
