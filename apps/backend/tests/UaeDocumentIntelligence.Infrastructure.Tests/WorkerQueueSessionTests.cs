using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class WorkerQueueSessionTests
{
    [Theory]
    [InlineData("Locked", "https://private.test", "")]
    [InlineData("Development", "http://127.0.0.1", "")]
    [InlineData("ServiceKeys", "https://private.test", "short")]
    [InlineData("ServiceKeys", "http://private.test", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Enforced_worker_rejects_bad_private_transport_before_database_or_claim(string mode, string url, string token)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new AiServiceOptions
        {
            AuthenticationMode = mode, BaseUrl = url, ExtractionCredential = new() { KeyId = "worker-key", Token = token }
        }));
        using var provider = services.BuildServiceProvider();
        var worker = new ExtractionWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ExtractionWorkerOptions { SecurityMode = "Enforced", Enabled = true }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ExtractionWorker>.Instance);
        var method = typeof(ExtractionWorker).GetMethod("EnsurePostgresAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var task = (Task)method.Invoke(worker, new object[] { CancellationToken.None })!;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("Enforced workers require the private extraction service credential and HTTPS transport before claiming work.", error.Message);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("retained")]
    [InlineData("identity")]
    [InlineData("database")]
    [InlineData("insecure")]
    [InlineData("details")]
    [InlineData("parameters")]
    [InlineData("invalid-role")]
    public async Task Enforced_worker_rejects_invalid_configuration_without_api_connection_fallback(string scenario)
    {
        var host = scenario == "insecure" ? "private.database.test" : "127.0.0.1";
        var database = scenario == "retained" ? "phase5_retained" : "worker_test";
        var connection = $"Host={host};Database={database};Username=worker_test;Password=PRIVATE_SECRET";
        if (scenario == "missing") connection = null;
        if (scenario == "identity") connection += ";Username=owner";
        if (scenario == "database") connection += ";Database=other";
        if (scenario == "details") connection += ";Include Error Detail=true";
        if (scenario == "parameters") connection += ";Log Parameters=true";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = $"Host={host};Database={database};Username=api",
            ["ConnectionStrings:WorkerDatabase"] = connection,
            ["ExtractionWorker:SecurityMode"] = "Enforced",
            ["ExtractionWorker:DatabaseRole"] = scenario == "invalid-role" ? "unsafe;role" : "worker_test"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkerQueueSession.OpenAsync(scope.ServiceProvider, default));
        Assert.Null(error.InnerException);
        Assert.Equal("The dedicated worker database connection, schema or permissions are not configured.", error.Message);
        Assert.DoesNotContain("PRIVATE_SECRET", error.ToString());
    }
}
