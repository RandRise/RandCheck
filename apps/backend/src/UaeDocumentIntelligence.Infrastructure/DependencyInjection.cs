using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Queries;
using UaeDocumentIntelligence.Application.Documents.Review;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        services.AddDbContext<AppDbContext>(options =>
        {
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
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentStorage, LocalDocumentStorage>();
        services.AddSingleton<IExecutionContext, DevelopmentExecutionContext>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<UploadDocumentHandler>();
        services.AddScoped<DocumentQueries>();
        services.AddScoped<MetricsQuery>();
        services.AddScoped<GetReviewWorkspaceHandler>();
        services.AddScoped<StartReviewHandler>();
        services.AddScoped<RecordReviewActionHandler>();
        services.AddScoped<CompleteReviewHandler>();
        services.AddHttpClient<IExtractionClient, PythonExtractionClient>(client =>
            client.BaseAddress = new Uri(configuration["AiService:BaseUrl"] ?? "http://localhost:8001"));
        return services;
    }

    public static async Task SeedDevelopmentDataAsync(this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await DevelopmentDataSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), cancellationToken);
    }
}
