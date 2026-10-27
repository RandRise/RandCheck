using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure;

public static partial class DependencyInjection
{
    /// <summary>
    /// Explicit registration for standalone pilot tools and synthetic tests only.
    /// Registration keeps generation and ingestion disabled and recovery denied.
    /// The public API and worker do not call this extension.
    /// </summary>
    public static IServiceCollection AddCitedPilotInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICitedEmbeddingStore, CitedEmbeddingStore>();
        services.AddScoped<ICitedEmbeddingSources, CitedEmbeddingSources>();
        services.AddSingleton(new CitedEmbeddingSettings());
        services.AddScoped<CitedEmbeddingCoordinator>();
        services.AddHttpClient<ICitedEmbeddingClient, PythonCitedEmbeddingClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["AiService:BaseUrl"] ?? "http://127.0.0.1:8001");
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(PrivateServiceHandler).RemoveAllLoggers();
        services.AddScoped<CitedGenerationLedger>();
        services.AddScoped<ICitedGenerationLedger>(sp => sp.GetRequiredService<CitedGenerationLedger>());
        services.AddScoped<ICitedGenerationJournal>(sp => sp.GetRequiredService<CitedGenerationLedger>());
        services.AddScoped<ICitedPilotUnknownHold>(sp => sp.GetRequiredService<CitedGenerationLedger>());
        services.AddSingleton<ICitedRecoveryAuthorization, DenyCitedRecoveryAuthorization>();
        services.AddSingleton<ICitedRecoveryEvidenceVerifier, UnavailableCitedRecoveryEvidenceVerifier>();
        services.AddScoped<ICitedRecoveryReader, CitedRecoveryReader>();
        services.AddScoped<AssessCitedRecovery>();
        services.AddSingleton<ICitedRecoveryExecutionAuthorization, DenyCitedRecoveryExecutionAuthorization>();
        services.AddScoped<ICitedRecoveryExecutor, CitedRecoveryExecutor>();
        services.AddSingleton(new CitedGenerationSettings());
        services.AddScoped<CitedGenerationCoordinator>();
        services.AddHttpClient<ICitedAnswerClient, PythonCitedAiClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["AiService:BaseUrl"] ?? "http://localhost:8001");
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(PrivateServiceHandler).RemoveAllLoggers();
        return services;
    }
}
