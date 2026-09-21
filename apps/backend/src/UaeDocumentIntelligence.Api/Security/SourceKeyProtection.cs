using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Api.Security;

public sealed class SourceKeyCertificate
{
    public string Path { get; set; } = "";
    public string Password { get; set; } = "";
}
public sealed class SourceKeyProtectionOptions
{
    public string Mode { get; set; } = "Locked";
    public string ApplicationName { get; set; } = "";
    public string KeyDirectory { get; set; } = "";
    public string CertificatePath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";
    public SourceKeyCertificate[] PreviousCertificates { get; set; } = [];
    public int KeyLifetimeDays { get; set; } = 90;

    public static SourceKeyProtectionOptions Read(IConfiguration configuration, string environment)
    {
        var options = configuration.GetSection("SourceKeyProtection").Get<SourceKeyProtectionOptions>() ?? new();
        var local = environment is "Development" or "Testing";
        var configured = configuration["SourceKeyProtection:Mode"];
        options.Mode = configured == "Certificate" ? "Certificate" : local && (configured is null or "Development") ? "Development" : "Locked";
        return options;
    }
    public void Validate(IConfiguration configuration)
    {
        if (Mode != "Certificate" || !Regex.IsMatch(ApplicationName ?? "", "\\A[A-Za-z0-9][A-Za-z0-9._-]{2,99}\\z") ||
            !System.IO.Path.IsPathFullyQualified(KeyDirectory) || !System.IO.Path.IsPathFullyQualified(CertificatePath) ||
            KeyLifetimeDays is < 7 or > 365 || PreviousCertificates is null || PreviousCertificates.Length > 8)
            throw new InvalidOperationException("Source key protection configuration is incomplete.");
        foreach (var other in new[] { configuration["Storage:UploadDirectory"], configuration["Storage:QuarantineDirectory"], configuration["DocumentDeletion:JournalDirectory"] })
            if (!string.IsNullOrWhiteSpace(other) && (Within(KeyDirectory, other) || Within(other, KeyDirectory) || Within(CertificatePath, other)))
                throw new InvalidOperationException("Source key material must be separate from content and deletion storage.");
        foreach (var certificate in PreviousCertificates.Prepend(new SourceKeyCertificate { Path = CertificatePath }))
        {
            if (certificate is null || !System.IO.Path.IsPathFullyQualified(certificate.Path) || Within(certificate.Path, KeyDirectory))
                throw new InvalidOperationException("Wrapping certificates must be outside the shared key directory.");
            foreach (var other in new[] { configuration["Storage:UploadDirectory"], configuration["Storage:QuarantineDirectory"], configuration["DocumentDeletion:JournalDirectory"] })
                if (!string.IsNullOrWhiteSpace(other) && Within(certificate.Path, other)) throw new InvalidOperationException();
        }
    }
    private static bool Within(string a, string b)
    {
        var left = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(a));
        var right = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(b));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return left.Equals(right, comparison) || left.StartsWith(right + System.IO.Path.DirectorySeparatorChar, comparison);
    }
}

// Isolated provider for source grants: framework crypto and rotation, without framework
// logging providers that could emit key paths or certificate diagnostics to API logs.
public sealed class SourceKeyProtection : IDataProtectionProvider, IDisposable
{
    private readonly SourceKeyProtectionOptions options;
    private readonly IDataProtectionProvider development;
    private readonly Lazy<KeyServices> keys;
    public SourceKeyProtection(IConfiguration configuration, string environment, IDataProtectionProvider development)
    {
        this.development = development;
        try { options = SourceKeyProtectionOptions.Read(configuration, environment); }
        catch { options = new SourceKeyProtectionOptions(); }
        keys = new Lazy<KeyServices>(() => Load(options, configuration), LazyThreadSafetyMode.ExecutionAndPublication);
    }
    public IDataProtector CreateProtector(string purpose) => new Guarded(this, [purpose]);
    private IDataProtector Protector(string[] purposes)
    {
        if (options.Mode == "Development") return development.CreateProtector(purposes);
        try
        {
            if (options.Mode != "Certificate") throw new InvalidOperationException();
            var loaded = keys.Value;
            loaded.Repository.GetAllElements();
            if (DateTime.UtcNow < loaded.Certificates[0].NotBefore.ToUniversalTime() || DateTime.UtcNow >= loaded.Certificates[0].NotAfter.ToUniversalTime())
                throw new InvalidOperationException();
            return loaded.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(purposes);
        }
        catch { throw new CryptographicException("Source key protection is unavailable."); }
    }
    private sealed class Guarded(SourceKeyProtection owner, string[] purposes) : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => new Guarded(owner, [.. purposes, purpose]);
        public byte[] Protect(byte[] plaintext) { try { return owner.Protector(purposes).Protect(plaintext); } catch { throw new CryptographicException("Source key protection is unavailable."); } }
        public byte[] Unprotect(byte[] protectedData) { try { return owner.Protector(purposes).Unprotect(protectedData); } catch { throw new CryptographicException("Source key protection is unavailable."); } }
    }
    public static void VerifyResources(SourceKeyProtectionOptions options, IConfiguration configuration)
    {
        using var loaded = Load(options, configuration);
        // Inspect and decrypt existing descriptors without generating or writing a key.
        foreach (var key in loaded.Services.GetRequiredService<IKeyManager>().GetAllKeys()) _ = key.Descriptor;
    }
    private static KeyServices Load(SourceKeyProtectionOptions options, IConfiguration configuration)
    {
        options.Validate(configuration);
        var certificates = new List<X509Certificate2>();
        ServiceProvider? built = null;
        try
        {
            foreach (var item in options.PreviousCertificates.Prepend(new SourceKeyCertificate { Path = options.CertificatePath, Password = options.CertificatePassword }))
            {
                RequireSafePath(item.Path, false);
                if (new FileInfo(item.Path).Length is < 1 or > 1048576) throw new InvalidOperationException();
                var certificate = new X509Certificate2(item.Path, item.Password, X509KeyStorageFlags.EphemeralKeySet);
                certificates.Add(certificate);
                using var rsa = certificate.GetRSAPrivateKey();
                if (rsa is null || rsa.KeySize < 2048) throw new InvalidOperationException();
            }
            var current = certificates[0];
            if (DateTime.UtcNow < current.NotBefore.ToUniversalTime() || DateTime.UtcNow >= current.NotAfter.ToUniversalTime() ||
                certificates.Select(c => c.Thumbprint).Distinct().Count() != certificates.Count) throw new InvalidOperationException();
            var repository = new EncryptedKeyRepository(options.KeyDirectory);
            _ = repository.GetAllElements();
            var services = new ServiceCollection();
            services.AddLogging(); // No providers: suppress framework path/certificate diagnostics.
            services.AddDataProtection().SetApplicationName(options.ApplicationName).SetDefaultKeyLifetime(TimeSpan.FromDays(options.KeyLifetimeDays))
                .ProtectKeysWithCertificate(current).UnprotectKeysWithAnyCertificate(certificates.ToArray());
            services.Configure<KeyManagementOptions>(o => o.XmlRepository = repository);
            built = services.BuildServiceProvider();
            foreach (var key in built.GetRequiredService<IKeyManager>().GetAllKeys()) _ = key.Descriptor;
            return new(built, repository, certificates.ToArray());
        }
        catch { built?.Dispose(); foreach (var certificate in certificates) certificate.Dispose(); throw new CryptographicException("Source key protection is unavailable."); }
    }
    public static void RequireSafePath(string path, bool directory)
    {
        if (!Path.IsPathFullyQualified(path) || (directory ? !Directory.Exists(path) : !File.Exists(path))) throw new InvalidOperationException();
        for (FileSystemInfo? item = directory ? new DirectoryInfo(path) : new FileInfo(path); item is not null;
            item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent)
            if (item.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException();
    }
    private sealed record KeyServices(ServiceProvider Services, EncryptedKeyRepository Repository, X509Certificate2[] Certificates) : IDisposable
    { public void Dispose() { Services.Dispose(); foreach (var certificate in Certificates) certificate.Dispose(); } }
    public void Dispose() { if (keys.IsValueCreated) keys.Value.Dispose(); }
}

internal sealed class EncryptedKeyRepository(string directory) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        SourceKeyProtection.RequireSafePath(directory, true);
        var entries = Directory.EnumerateFileSystemEntries(directory).Take(101).ToArray();
        if (entries.Length > 100) throw new CryptographicException("Source key repository limit exceeded.");
        var result = new List<XElement>();
        foreach (var path in entries)
        {
            SourceKeyProtection.RequireSafePath(path, false);
            var name = Path.GetFileName(path);
            if (name.EndsWith(".tmp", StringComparison.Ordinal) && Guid.TryParse(Path.GetFileNameWithoutExtension(name), out _)) continue;
            if (!name.EndsWith(".xml", StringComparison.Ordinal) || new FileInfo(path).Length is < 1 or > 65536) throw new CryptographicException();
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            var element = XElement.Load(reader); Validate(element); result.Add(element);
        }
        return result;
    }
    public void StoreElement(XElement element, string friendlyName)
    {
        _ = GetAllElements(); Validate(element);
        new FileSystemXmlRepository(new DirectoryInfo(directory), NullLoggerFactory.Instance).StoreElement(element, friendlyName);
    }
    private static void Validate(XElement element)
    {
        if (element.DescendantsAndSelf().Any(x => x.Name.LocalName == "masterKey")) throw new CryptographicException();
        if (element.Name == "revocation") return;
        var secret = element.Descendants(XName.Get("encryptedSecret", "http://schemas.asp.net/2015/03/dataProtection")).SingleOrDefault();
        if (element.Name != "key" || secret is null ||
            !(secret.Attribute("decryptorType")?.Value.StartsWith("Microsoft.AspNetCore.DataProtection.XmlEncryption.EncryptedXmlDecryptor,", StringComparison.Ordinal) ?? false) ||
            !secret.Descendants().Any(x => x.Name.LocalName == "CipherValue" && !string.IsNullOrWhiteSpace(x.Value))) throw new CryptographicException();
    }
}

public static class SourceKeyRegistration
{
    public static IServiceCollection AddSourceKeyProtection(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton(sp => new SourceKeyProtection(configuration, environment.EnvironmentName, sp.GetRequiredService<IDataProtectionProvider>()));
        services.AddScoped(sp => new SourceAccessTokens(sp.GetRequiredService<SourceKeyProtection>(), sp.GetRequiredService<IClock>()));
        return services;
    }
}
