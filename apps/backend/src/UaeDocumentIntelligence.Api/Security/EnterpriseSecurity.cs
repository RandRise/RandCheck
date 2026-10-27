using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Api.Security;

public sealed class EnterpriseSecurityOptions
{
    public string Mode { get; set; } = "Locked";
    public Guid TenantId { get; set; }
    public Guid Audience { get; set; }
    public string RequiredScope { get; set; } = "DocumentIntelligence.Access";
    public Guid[] AllowedClientIds { get; set; } = [];
    public string Issuer => $"https://login.microsoftonline.com/{TenantId:D}/v2.0";

    public void Validate(IHostEnvironment environment)
    {
        if (Mode == "Development" && !(environment.IsDevelopment() || environment.IsEnvironment("Testing")))
            throw new InvalidOperationException("Development identity is forbidden outside Development or Testing.");
        if (Mode is not ("Locked" or "Development" or "Entra"))
            throw new InvalidOperationException("Security:Mode must be Locked, Development or Entra.");
        if (Mode == "Entra" && (TenantId == Guid.Empty || Audience == Guid.Empty ||
            string.IsNullOrWhiteSpace(RequiredScope) || RequiredScope.Any(char.IsWhiteSpace) ||
            AllowedClientIds.Length == 0 || AllowedClientIds.Contains(Guid.Empty)))
            throw new InvalidOperationException("Entra requires tenant, API audience, delegated scope and allowed client IDs.");
    }
}

public static class EnterpriseSecurity
{
    public static IServiceCollection AddEnterpriseSecurity(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = configuration.GetSection("Security").Get<EnterpriseSecurityOptions>() ?? new();
        settings.Validate(environment);
        services.AddSingleton(settings);
        services.AddScoped<RequestIdentity>();
        services.Replace(ServiceDescriptor.Scoped<IExecutionContext>(sp => sp.GetRequiredService<RequestIdentity>()));
        services.AddScoped<IEnterpriseActorStore, EnterpriseActorStore>();
        services.AddScoped<IAuthorizationHandler, EnterpriseAccessHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditedAuthorizationResultHandler>();
        services.AddScoped<SecurityAuditOutcomeFilter>();
        var authentication = services.AddAuthentication("Enterprise");
        if (settings.Mode == "Entra")
            authentication.AddJwtBearer("Enterprise", options =>
            {
                options.Authority = settings.Issuer;
                options.Audience = settings.Audience.ToString("D");
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = settings.Issuer,
                    ValidateAudience = true, ValidAudience = settings.Audience.ToString("D"),
                    ValidateLifetime = true, RequireExpirationTime = true,
                    RequireSignedTokens = true, ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "oid", RoleClaimType = "roles"
                };
            });
        else
            authentication.AddScheme<AuthenticationSchemeOptions, LocalOrLockedAuthenticationHandler>("Enterprise", _ => { });
        services.AddAuthorization(options =>
        {
            var policy = new AuthorizationPolicyBuilder("Enterprise").RequireAuthenticatedUser()
                .AddRequirements(new EnterpriseAccessRequirement()).Build();
            options.DefaultPolicy = policy;
            options.FallbackPolicy = policy;
        });
        return services;
    }
}

public sealed class RequestIdentity : IExecutionContext
{
    private Guid? _organizationId;
    private Guid? _actorId;
    public Guid OrganizationId => _organizationId ?? throw new InvalidOperationException("No authorized request identity.");
    public Guid ActorId => _actorId ?? throw new InvalidOperationException("No authorized request identity.");
    public void Bind(Guid organizationId, Guid actorId)
    {
        if (organizationId == Guid.Empty || actorId == Guid.Empty ||
            (_organizationId.HasValue && (_organizationId != organizationId || _actorId != actorId)))
            throw new InvalidOperationException("Invalid request identity binding.");
        _organizationId = organizationId;
        _actorId = actorId;
    }
}

public sealed record EnterpriseActor(Guid OrganizationId, Guid ActorId,
    OrganizationAccessRole Role = (OrganizationAccessRole)0);
public interface IEnterpriseActorStore
{
    Task<EnterpriseActor?> ResolveAsync(string externalSubject, CancellationToken cancellationToken);
}

public sealed class EnterpriseActorStore(AppDbContext db) : IEnterpriseActorStore
{
    public async Task<EnterpriseActor?> ResolveAsync(string externalSubject, CancellationToken cancellationToken)
    {
        var parts = externalSubject.Split(':');
        if (parts.Length != 3 || parts[0] != "entra" || !Guid.TryParse(parts[1], out var tenant) ||
            !Guid.TryParse(parts[2], out var subject)) return null;
        var memberships = await (from i in db.EnterpriseIdentities.AsNoTracking()
            join m in db.OrganizationMemberships.AsNoTracking() on i.Id equals m.IdentityId
            join a in db.Actors.AsNoTracking() on new { m.OrganizationId, Id = m.ActorId } equals new { a.OrganizationId, a.Id }
            where i.TenantId == tenant && i.ObjectId == subject && m.IsActive
            select new { m.OrganizationId, m.ActorId, m.Role, a.IsActive, a.Kind })
            .Take(2).ToArrayAsync(cancellationToken);
        return memberships.Length == 1 && memberships[0].IsActive && memberships[0].Kind == ActorKind.Human
            ? new(memberships[0].OrganizationId, memberships[0].ActorId, memberships[0].Role) : null;
    }
}

internal sealed class LocalOrLockedAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    EnterpriseSecurityOptions settings) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
        settings.Mode == "Development"
            ? AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim("local-development", "true")], Scheme.Name)), Scheme.Name))
            : AuthenticateResult.NoResult());
}

public sealed class EnterpriseAccessRequirement : IAuthorizationRequirement;

public sealed class EnterpriseAccessHandler(EnterpriseSecurityOptions settings, RequestIdentity identity,
    IEnterpriseActorStore actors, SecurityAuditContext audit) : AuthorizationHandler<EnterpriseAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EnterpriseAccessRequirement requirement)
    {
        if (context.Resource is not HttpContext http || context.User.Identity?.IsAuthenticated != true) return;
        if (settings.Mode == "Development")
        {
            if (EnterprisePermissions.Permission(http) is "ManageMembers" or "Audit" or "SourceRead" or "Retention") return;
            identity.Bind(DevelopmentIdentity.OrganizationId, DevelopmentIdentity.ActorId);
            context.Succeed(requirement);
            return;
        }
        if (settings.Mode != "Entra" || Single(context.User, "ver") != "2.0" ||
            !Guid.TryParse(Single(context.User, "tid"), out var tenant) || tenant != settings.TenantId ||
            !Guid.TryParse(Single(context.User, "oid"), out var subject) || subject == Guid.Empty ||
            !Guid.TryParse(Single(context.User, "azp"), out var client) || !settings.AllowedClientIds.Contains(client) ||
            !(Single(context.User, "scp")?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(settings.RequiredScope) ?? false)) return;
        var actor = await actors.ResolveAsync($"entra:{tenant:D}:{subject:D}", http.RequestAborted);
        if (actor is null) return;
        audit.Bind(actor.OrganizationId, actor.ActorId, "Human", http.TraceIdentifier);
        if (!EnterprisePermissions.Allows(http, actor.Role)) return;
        identity.Bind(actor.OrganizationId, actor.ActorId);
        context.Succeed(requirement);
    }

    private static string? Single(ClaimsPrincipal principal, string type)
    {
        var claims = principal.FindAll(type).Take(2).ToArray();
        return claims.Length == 1 ? claims[0].Value : null;
    }
}
