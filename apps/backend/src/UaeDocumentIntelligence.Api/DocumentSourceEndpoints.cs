using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Api;

public static class DocumentSourceEndpoints
{
    public sealed record AccessGrant(string Url, string AccessToken, DateTimeOffset ExpiresAtUtc);

    public static void MapDocumentSourceEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapPost("/api/documents/{documentId:guid}/source-access", (Guid documentId, HttpContext http, DocumentSourceAccess access, CancellationToken ct) =>
            access.IssueAsync(documentId, http, ct));
        api.MapGet("/api/documents/{documentId:guid}/source", (Guid documentId, HttpContext http, DocumentSourceAccess access, CancellationToken ct) =>
            access.DownloadAsync(documentId, http, ct));
    }
}

public sealed class DocumentSourceAccess(AppDbContext db, IExecutionContext identity, SourceAccessTokens tokens,
    IDocumentSourceReader reader, SecurityAuditContext audit, ISecurityAuditSink sink,
    IOptions<StorageOptions> storage, EnterpriseSecurityOptions security, IHostEnvironment environment)
{
    private bool Enabled(HttpContext http) => security.Mode == "Entra" && audit.IsBound &&
        storage.Value.SecurityMode == "Enforced" &&
        (http.Request.IsHttps || environment.IsDevelopment() || environment.IsEnvironment("Testing"));

    private Task<Guid?> MembershipAsync(CancellationToken ct) =>
        (from m in db.OrganizationMemberships.AsNoTracking()
         join a in db.Actors.AsNoTracking() on new { m.OrganizationId, Id = m.ActorId } equals new { a.OrganizationId, a.Id }
         where m.OrganizationId == identity.OrganizationId && m.ActorId == identity.ActorId && m.IsActive &&
               a.IsActive && a.Kind == ActorKind.Human &&
               (m.Role == OrganizationAccessRole.Staff || m.Role == OrganizationAccessRole.Admin)
         select (Guid?)m.Version).SingleOrDefaultAsync(ct);

    private Task<DocumentSource?> SourceAsync(Guid id, CancellationToken ct) => db.Documents.AsNoTracking()
        .Where(d => d.Id == id && d.OrganizationId == identity.OrganizationId)
        .Select(d => new DocumentSource(d.OrganizationId, d.Id, d.StorageKey, d.Sha256, d.SizeBytes)).SingleOrDefaultAsync(ct);

    public async Task<IResult> IssueAsync(Guid id, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        try
        {
            if (!Enabled(http)) return Unavailable();
            var version = await MembershipAsync(ct);
            var source = await SourceAsync(id, ct);
            if (source is null || version is null) return Results.NotFound();
            var grant = tokens.Issue(source, identity.ActorId, version.Value);
            await sink.WriteAsync(audit.Event("document.source-access-issued", "document", id, "Succeeded", "five-minute-grant"), ct);
            return Results.Ok(new DocumentSourceEndpoints.AccessGrant($"/api/documents/{id:D}/source", grant.Token, grant.ExpiresAtUtc));
        }
        catch (OperationCanceledException) { throw; }
        catch { return Unavailable(); }
    }

    public async Task<IResult> DownloadAsync(Guid id, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        try
        {
            if (!Enabled(http)) return await DenyAsync(id, "source_access_unavailable", 503, ct);
            var header = http.Request.Headers["X-Document-Access"];
            var grant = header.Count == 1 ? tokens.Read(header[0]) : null;
            if (grant is null || grant.OrganizationId != identity.OrganizationId || grant.ActorId != identity.ActorId || grant.DocumentId != id)
                return await DenyAsync(id, "invalid_grant", 404, ct);
            if (await MembershipAsync(ct) != grant.MembershipVersion) return await DenyAsync(id, "membership_changed", 404, ct);
            var source = await SourceAsync(id, ct);
            if (source is null || source.Sha256 != grant.Sha256 || source.SizeBytes != grant.SizeBytes)
                return await DenyAsync(id, "source_changed", 404, ct);
            var bytes = await reader.ReadVerifiedAsync(source, ct);
            // A scan can take time: enforce expiry and current membership again before releasing the snapshot.
            if (!tokens.IsCurrent(grant) || await MembershipAsync(ct) != grant.MembershipVersion)
                return await DenyAsync(id, "grant_no_longer_current", 404, ct);
            await sink.WriteAsync(audit.Event("document.source-download-authorized", "document", id, "Succeeded", "scan-clean",
                after: $"sha256={source.Sha256};bytes={source.SizeBytes}"), ct);
            // Attachment-only, generated safe filename, no range/conditional response bypass.
            var extension = Path.GetExtension(source.StorageKey).ToLowerInvariant();
            if (extension is not (".pdf" or ".png" or ".jpg" or ".jpeg" or ".txt")) extension = ".bin";
            return Results.File(bytes, "application/octet-stream", $"source-{id:N}{extension}", enableRangeProcessing: false);
        }
        catch (OperationCanceledException) { throw; }
        catch (MalwareDetectedException) { return await SafeDenyAsync(id, "malware_detected", 422, ct); }
        catch (MalwareScanUnavailableException) { return await SafeDenyAsync(id, "malware_scan_unavailable", 503, ct); }
        catch (DocumentSourceUnavailableException) { return await SafeDenyAsync(id, "source_unavailable", 404, ct); }
        catch { return Unavailable(); }
    }

    private async Task<IResult> SafeDenyAsync(Guid id, string reason, int status, CancellationToken ct)
    {
        try { return await DenyAsync(id, reason, status, ct); }
        catch (OperationCanceledException) { throw; }
        catch { return Unavailable(); }
    }
    private async Task<IResult> DenyAsync(Guid id, string reason, int status, CancellationToken ct)
    {
        await sink.WriteAsync(audit.Event("document.source-access-denied", "document", id, "Denied", reason), ct);
        return Results.Problem(statusCode: status, title: "Source file access could not be completed");
    }
    private static IResult Unavailable() => Results.Problem(statusCode: 503, title: "Source file access is unavailable");
}
