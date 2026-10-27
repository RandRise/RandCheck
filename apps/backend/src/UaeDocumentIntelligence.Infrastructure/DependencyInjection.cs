using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Queries;
using UaeDocumentIntelligence.Application.Documents.Review;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Application.Documents.Processing;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure;

public static partial class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddLogging();
        services.AddScoped<SecurityAuditContext>();
        services.AddScoped<ISecurityAuditSink, SecurityAuditSink>();
        services.AddScoped<EnterpriseMembershipService>();
        services.Configure<ApiDatabaseOptions>(configuration.GetSection("ApiDatabase"));
        services.Configure<CapacityOptions>(configuration.GetSection("Capacity"));
        services.AddScoped<ApiDatabaseGuard>();
        services.AddScoped<EnterpriseRetentionService>();
        services.Configure<DocumentDeletionOptions>(configuration.GetSection("DocumentDeletion"));
        services.AddScoped<DocumentDeletionJournal>();
        services.AddScoped<DocumentRecoveryGuard>();
        services.AddSingleton<DatabasePrivacyLoggerFactory>();
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseLoggerFactory(provider.GetRequiredService<DatabasePrivacyLoggerFactory>());
            if (connection.StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
                options.UseNpgsql(connection, x =>
                {
                    x.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
            else
                options.UseSqlite(connection, x =>
                    x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<MalwareScanOptions>(configuration.GetSection("MalwareScan"));
        services.AddScoped<IMalwareScanner, ClamAvScanner>();
        services.AddScoped<ExtractionFileSafety>();
        services.Configure<AiServiceOptions>(configuration.GetSection("AiService"));
        services.Configure<ExtractionWorkerOptions>(configuration.GetSection("ExtractionWorker"));
        services.Configure<StorageMaintenanceOptions>(configuration.GetSection("StorageMaintenance"));
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentLibraryRepository, DocumentRepository>();
        services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
        services.AddScoped<IReconciliationReportRepository, ReconciliationReportRepository>();
        services.AddScoped<IReconciliationDraftRequestStore, ReconciliationDraftRequestStore>();
        services.AddScoped<ICitedQuestionRepository, CitedQuestionRepository>();
        // Existing prepared semantic searches belong to the public, provider-free graph.
        services.AddScoped<ICitedSemanticRetrievalStore, CitedEmbeddingStore>();
        services.AddScoped<ICitedRetrievalStore, CitedRetrievalStore>();
        services.AddScoped<IndexReviewedDocumentHandler>();
        services.AddScoped<RetrieveEvidenceHandler>();
        services.AddScoped<CitedQuestionHandler>();
        services.AddScoped<ICitedReviewedQuestionStore, CitedReviewedQuestionStore>();
        services.AddScoped<CitedReviewedQuestionHandler>();
        services.AddScoped<CitedWorkspaceHandler>();
        services.AddScoped<ICitedCaseSourceStore, CitedCaseSourceStore>();
        services.AddScoped<CitedCaseSourcesHandler>();
        services.AddScoped<CitedSemanticQuestionHandler>();
        services.AddScoped<ICitedFactSourceStore, CitedFactSourceStore>();
        services.AddScoped<CitedFactWorkspace>(); // Private, provider-free typed-fact candidate; no public route.
        services.AddScoped<ICitedWorkspaceStore, CitedWorkspaceStore>();
        // .NET 8 header redaction masks formatted text but retains raw structured
        // header values. This client emits its own content-free operation telemetry.
        services.AddHttpClient<ICitedAiClient, PythonCitedAiClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["AiService:BaseUrl"] ?? "http://localhost:8001");
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(PrivateServiceHandler).RemoveAllLoggers();
        services.AddScoped<IUploadQueueStore, UploadQueueStore>();
        services.AddScoped<IExtractionJobQueue, ExtractionJobQueue>();
        services.AddScoped<IDocumentStorage, LocalDocumentStorage>();
        services.AddScoped<IDocumentSourceReader, LocalDocumentSourceReader>();
        services.AddSingleton<IExecutionContext, DevelopmentExecutionContext>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<UploadDocumentHandler>();
        services.AddScoped<GetDocumentProcessingStatusHandler>();
        services.AddScoped<CancelExtractionHandler>();
        services.AddScoped<ReprocessDocumentHandler>();
        services.AddScoped<DocumentQueries>();
        services.AddScoped<DocumentLibraryQueries>();
        services.AddScoped<MetricsQuery>();
        services.AddScoped<GetReviewHistoryHandler>();
        services.AddScoped<GetReviewWorkspaceHandler>();
        services.AddScoped<StartReviewHandler>();
        services.AddScoped<RecordReviewActionHandler>();
        services.AddScoped<CompleteReviewHandler>();
        services.AddScoped<CreateReconciliationCaseHandler>();
        services.AddScoped<GetReconciliationCaseHandler>();
        services.AddScoped<GetReconciliationReportHandler>();
        services.AddScoped<ListReconciliationCasesHandler>();
        services.AddScoped<UpdateReconciliationCaseMetadataHandler>();
        services.AddScoped<LinkReconciliationDocumentHandler>();
        services.AddScoped<SetReconciliationDraftDocumentHandler>();
        services.AddScoped<EvaluateReconciliationCaseHandler>();
        services.AddScoped<ResolveReconciliationFindingHandler>();
        services.AddHttpClient<IExtractionClient, PythonExtractionClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["AiService:BaseUrl"] ?? "http://localhost:8001");
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(PrivateServiceHandler).RemoveAllLoggers();
        services.AddHostedService<ExtractionWorker>();
        services.AddHostedService<StorageMaintenanceService>();
        return services;
    }

    private static HttpMessageHandler PrivateServiceHandler() => new HttpClientHandler
    { AllowAutoRedirect = false, UseProxy = false, UseCookies = false };

    public static async Task SeedDevelopmentDataAsync(this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await DevelopmentDataSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), cancellationToken);
    }
}
