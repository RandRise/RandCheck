using UaeDocumentIntelligence.Application.Documents.Queries;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Api;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Services;
using UaeDocumentIntelligence.Api.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.PostConfigure<StorageOptions>(options => options.SecurityMode =
    StorageOptions.ResolveSecurityMode(builder.Configuration["Storage:SecurityMode"], builder.Environment.EnvironmentName));
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = 10485760);
builder.Services.PostConfigure<ExtractionWorkerOptions>(options => options.SecurityMode =
    ExtractionWorkerOptions.ResolveSecurityMode(builder.Configuration["ExtractionWorker:SecurityMode"],
        builder.Environment.EnvironmentName));
builder.Services.PostConfigure<AiServiceOptions>(options => options.AuthenticationMode =
    AiServiceOptions.ResolveAuthenticationMode(builder.Configuration["AiService:AuthenticationMode"],
        builder.Environment.EnvironmentName));
builder.Services.AddEnterpriseSecurity(builder.Configuration, builder.Environment);
builder.Services.PostConfigure<UaeDocumentIntelligence.Infrastructure.Persistence.CapacityOptions>(options =>
    options.RequirePolicy |= !(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")));
var requestLimits = RequestLimitOptions.Read(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(requestLimits);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RequestBudgets>();
builder.Services.AddDataProtection();
builder.Services.AddSourceKeyProtection(builder.Configuration, builder.Environment);
builder.Services.AddScoped<DocumentSourceAccess>();
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("ETag", "Content-Disposition", "Retry-After")));

var app = builder.Build();
app.UseCors();
app.Use(async (context, next) =>
{
    var suppliedRequestId = context.Request.Headers["X-Request-ID"].FirstOrDefault()?.Trim();
    var requestId = !string.IsNullOrEmpty(suppliedRequestId) && suppliedRequestId.Length <= 100 &&
        System.Text.RegularExpressions.Regex.IsMatch(suppliedRequestId, "^[A-Za-z0-9._:-]{1,100}$")
        ? suppliedRequestId
        : Guid.NewGuid().ToString("N");
    context.TraceIdentifier = requestId;
    context.Response.Headers["X-Request-ID"] = requestId;
    await next();
});
app.Use(RequestLimitMiddleware.IngressAsync);
// Keep the existing local profile's response contract. Enterprise admission runs
// before any cited-body buffering, database checks, or authorized workflow work.
var earlyLocalCitedPolicy = !requestLimits.Enabled &&
    app.Services.GetRequiredService<EnterpriseSecurityOptions>().Mode != "Entra";
if (earlyLocalCitedPolicy) app.UseCitedResponsePolicy();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
    await next();
});
// Verify before authentication's membership lookup or any business operation.
// No runtime migration, grant repair, or credential fallback is performed.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<UaeDocumentIntelligence.Infrastructure.Persistence.ApiDatabaseOptions>>().Value.Enabled)
    {
        try { await context.RequestServices.GetRequiredService<UaeDocumentIntelligence.Infrastructure.Persistence.ApiDatabaseGuard>().VerifyAsync(context.RequestAborted); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch
        {
            await Results.Problem(statusCode: 503, title: "api_database_verification_required").ExecuteAsync(context);
            return;
        }
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.Use(RequestLimitMiddleware.AuthorizedAsync);
app.Use(SecurityAuditAdmission.InvokeAsync);
if (!earlyLocalCitedPolicy) app.UseCitedResponsePolicy();
app.Use(RequestBodyLimit.InvokeAsync);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    if (app.Services.GetRequiredService<EnterpriseSecurityOptions>().Mode == "Development" &&
        app.Configuration.GetValue("DevelopmentData:Seed", false))
    {
        await app.Services.SeedDevelopmentDataAsync();
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "document-intelligence-api" })).AllowAnonymous();

var api = app.MapGroup("").AddEndpointFilter<SecurityAuditOutcomeFilter>().AddEndpointFilter<DocumentRecoveryBoundary>();

api.MapGet("/api/documents", async (DocumentQueries queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.ListAsync(cancellationToken)));

api.MapGet("/api/documents/{id:guid}", async (Guid id, DocumentQueries queries, CancellationToken cancellationToken) =>
    await queries.GetAsync(id, cancellationToken) is { } document ? Results.Ok(document) : Results.NotFound());

api.MapPost("/api/documents", async (HttpContext context, IFormFile file, UploadDocumentHandler handler,
    CancellationToken cancellationToken) =>
{
    try
    {
        var command = new UploadDocumentCommand(file.FileName, file.ContentType, file.Length,
            file.OpenReadStream(), context.TraceIdentifier);
        var document = await handler.HandleAsync(command, cancellationToken);
        return Results.Accepted($"/api/documents/{document.Id}", document);
    }
    catch (UnsupportedDocumentTypeException)
    {
        return Results.Problem("Unsupported file type", statusCode: StatusCodes.Status415UnsupportedMediaType);
    }
    catch (UaeDocumentIntelligence.Application.Abstractions.OrganizationCapacityException error)
    {
        return CapacityResponse.Problem(error);
    }
    catch (DocumentTooLargeException)
    {
        return Results.Problem("File is empty or exceeds the upload limit", statusCode: StatusCodes.Status413PayloadTooLarge);
    }
    catch (MalwareDetectedException)
    {
        return Results.Problem("File did not pass malware screening", statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    catch (MalwareScanUnavailableException)
    {
        return Results.Problem("File screening is unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).DisableAntiforgery();

api.MapGet("/api/metrics", async (MetricsQuery query, CancellationToken cancellationToken) =>
    Results.Ok(await query.ExecuteAsync(cancellationToken)));

api.MapReviewEndpoints();
api.MapProcessingEndpoints();
api.MapReconciliationEndpoints();
api.MapCitedRetrievalEndpoints();
api.MapMembershipEndpoints();
api.MapRetentionEndpoints();
api.MapSessionEndpoints();
api.MapDocumentSourceEndpoints();

app.Run();

public partial class Program;
