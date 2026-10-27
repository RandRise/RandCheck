using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    /// <summary>
    /// Explicit Windows browser probe, outside the ordinary xUnit selection. Reuses
    /// real authentication/repositories and the reserved disposable test database.
    /// The only identity substitution is offline JWT signing metadata; no Entra call.
    /// </summary>
    private static async Task<int> RunAdminBrowserAcceptanceAsync(string root, string output)
    {
        root = Path.GetFullPath(root);
        output = Path.GetFullPath(output);
        var expectedPrefix = Path.Combine(root, "outputs") + Path.DirectorySeparatorChar;
        if (!OperatingSystem.IsWindows() || !output.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(output) || !File.Exists(Path.Combine(root, "tools/admin-integration/server.mjs"))) return 2;
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION") ?? "");
        if (connection.Host != "127.0.0.1" || connection.Database != "postgres") return 2;

        Directory.CreateDirectory(output);
        // A directly executed test assembly lacks dotnet test's content-root setup.
        // Keep the explicit factory manifest inside this new private output folder.
        await File.WriteAllTextAsync(Path.Combine(output, "MvcTestingAppManifest.json"), JsonSerializer.Serialize(
            new Dictionary<string, string> { [typeof(global::Program).Assembly.FullName!] = Path.Combine(root, "apps/backend/src/UaeDocumentIntelligence.Api") }));
        var previousDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(output);
        var database = new PostgresDatabaseFixture();
        try
        {
            var runner = new PostgresPersistenceTests(database);
            await runner.InitializeAsync();
            await runner.RunAdminBrowserAsync(root, output);
            Console.WriteLine("PASS: recorded Admin browser assertions against offline identity and the real local API.");
            return 0;
        }
        catch (Exception error)
        {
            // Diagnostics stay in the private output, not a token-bearing console dump.
            await File.WriteAllTextAsync(Path.Combine(output, "failure.txt"), error.ToString());
            Console.Error.WriteLine("Admin browser acceptance failed; inspect its private output.");
            return 1;
        }
        finally { Directory.SetCurrentDirectory(previousDirectory); await database.DisposeAsync(); }
    }

    private async Task RunAdminBrowserAsync(string root, string output)
    {
        using var factory = new LiveBrowserFactory();
        using var fixture = await SourceFixtureAsync(securityFactory: factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Cors:Origins:0", "http://127.0.0.1:3338")
                .UseContentRoot(Path.Combine(root, "apps/backend/src/UaeDocumentIntelligence.Api"))));
        var configPath = Path.Combine(output, "private-browser-config.json");
        var instance = Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
        {
            api = factory.Address,
            instance,
            token = fixture.Admin.DefaultRequestHeaders.Authorization!.Parameter,
            staffToken = fixture.User.DefaultRequestHeaders.Authorization!.Parameter,
            tenantId = SecurityTenant, clientId = SecurityClient,
            scope = $"api://{SecurityAudience}/DocumentIntelligence.Access",
            documentId = fixture.DocumentId, memberId = fixture.Member.Id,
            expectedSource = SourceFixture.Content,
        }));
        var node = Path.Combine(root, ".node/node.exe");
        Process StartNode(string script, params string[] args)
        {
            var start = new ProcessStartInfo(node) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, script));
            foreach (var arg in args) start.ArgumentList.Add(arg);
            return Process.Start(start) ?? throw new InvalidOperationException("Cannot start owned browser probe.");
        }

        using var web = StartNode("tools/admin-integration/server.mjs", configPath);
        var webOut = web.StandardOutput.ReadToEndAsync();
        var webError = web.StandardError.ReadToEndAsync();
        try
        {
            using var readiness = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var ready = false;
            for (var attempt = 0; attempt < 60; attempt++)
            {
                if (web.HasExited) throw new InvalidOperationException("Owned browser fixture did not start.");
                try { ready = await readiness.GetStringAsync("http://127.0.0.1:3338/__admin_acceptance_owner") == instance; }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                if (ready) break;
                await Task.Delay(500);
            }
            if (!ready) throw new TimeoutException("Owned browser fixture startup timed out.");
            using var browser = StartNode("tools/verification/admin-api-browser.mjs", configPath, output);
            var stdout = browser.StandardOutput.ReadToEndAsync();
            var stderr = browser.StandardError.ReadToEndAsync();
            try
            {
                await browser.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(5));
                await File.WriteAllTextAsync(Path.Combine(output, "browser.log"), await stdout + await stderr);
                if (browser.ExitCode != 0 || web.HasExited) throw new InvalidOperationException("Browser assertions failed or owned frontend exited.");
            }
            finally { if (!browser.HasExited) browser.Kill(entireProcessTree: true); }
        }
        finally
        {
            if (!web.HasExited) web.Kill(entireProcessTree: true);
            await web.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await File.WriteAllTextAsync(Path.Combine(output, "web.log"), await webOut + await webError);
        }
    }

    /// <summary>The factory retains its TestServer for fixture setup and starts a
    /// second, real loopback Kestrel host with identical test configuration. Workers
    /// and maintenance remain disabled in SecurityHost. Both hosts are disposed.</summary>
    private sealed class LiveBrowserFactory : WebApplicationFactory<global::Program>
    {
        private IHost? _live;
        public string Address { get; private set; } = "";

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var test = builder.Build();
            try
            {
                builder.ConfigureWebHost(web => web.UseKestrel().UseUrls("http://127.0.0.1:0"));
                _live = builder.Build();
                _live.Start();
                Address = _live.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                test.Start();
                return test;
            }
            catch { test.Dispose(); _live?.Dispose(); _live = null; throw; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _live?.Dispose(); _live = null; }
            base.Dispose(disposing);
        }
    }
}
