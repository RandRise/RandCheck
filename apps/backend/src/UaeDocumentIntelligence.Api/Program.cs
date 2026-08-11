using UaeDocumentIntelligence.Application.Documents.Queries;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Api;
using UaeDocumentIntelligence.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("ETag")));

var app = builder.Build();
app.UseCors();
app.Use(async (context, next) =>
{
    var suppliedRequestId = context.Request.Headers["X-Request-ID"].FirstOrDefault()?.Trim();
    var requestId = !string.IsNullOrEmpty(suppliedRequestId) && suppliedRequestId.Length <= 100
        ? suppliedRequestId
        : Guid.NewGuid().ToString("N");
    context.TraceIdentifier = requestId;
    context.Response.Headers["X-Request-ID"] = requestId;
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    if (app.Configuration.GetValue("DevelopmentData:Seed", false))
    {
        await app.Services.SeedDevelopmentDataAsync();
    }
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "document-intelligence-api" }));

app.MapGet("/api/documents", async (DocumentQueries queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.ListAsync(cancellationToken)));

app.MapGet("/api/documents/{id:guid}", async (Guid id, DocumentQueries queries, CancellationToken cancellationToken) =>
    await queries.GetAsync(id, cancellationToken) is { } document ? Results.Ok(document) : Results.NotFound());

app.MapPost("/api/documents", async (HttpContext context, IFormFile file, UploadDocumentHandler handler,
    CancellationToken cancellationToken) =>
{
    try
    {
        var command = new UploadDocumentCommand(file.FileName, file.ContentType, file.Length,
            file.OpenReadStream(), context.TraceIdentifier);
        var document = await handler.HandleAsync(command, cancellationToken);
        return Results.Created($"/api/documents/{document.Id}", document);
    }
    catch (UnsupportedDocumentTypeException)
    {
        return Results.Problem("Unsupported file type", statusCode: StatusCodes.Status415UnsupportedMediaType);
    }
    catch (DocumentTooLargeException)
    {
        return Results.Problem("File is empty or exceeds the upload limit", statusCode: StatusCodes.Status413PayloadTooLarge);
    }
}).DisableAntiforgery();

app.MapGet("/api/metrics", async (MetricsQuery query, CancellationToken cancellationToken) =>
    Results.Ok(await query.ExecuteAsync(cancellationToken)));

app.MapReviewEndpoints();

app.Run();

public partial class Program;
