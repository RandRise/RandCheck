using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Infrastructure.Persistence;

if (args.Length != 2 || args[0] is not ("bootstrap" or "worker-role" or "worker-actor" or "api-role" or "verify-api-role" or "capacity-policy" or "capacity-status"))
{
    Console.Error.WriteLine("Usage: EnterpriseAdmin bootstrap|worker-role|worker-actor|api-role|verify-api-role|capacity-policy|capacity-status <provisioning.json>; set PHASE6_ADMIN_CONNECTION privately.");
    return 2;
}
try
{
    var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PHASE6_ADMIN_CONNECTION")
        ?? throw new InvalidOperationException("PHASE6_ADMIN_CONNECTION is required."));
    if (string.IsNullOrWhiteSpace(connection.Database) || connection.Database.StartsWith("phase5_", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Bootstrap refuses retained Phase 5 databases.");
    var input = await File.ReadAllTextAsync(args[1]);
    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    var audit = new SecurityAuditContext();
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options, audit);
    if (args[0] is "capacity-policy" or "capacity-status")
    {
        var capacity = JsonSerializer.Deserialize<CapacityProvisioning>(input, jsonOptions) ?? throw new InvalidOperationException();
        if (string.IsNullOrWhiteSpace(capacity.ExpectedDatabase) || capacity.ExpectedDatabase != connection.Database ||
            connection.IncludeErrorDetail || connection.LogParameters) throw new InvalidOperationException();
        CapacityUsage usage;
        if (args[0] == "capacity-policy") usage = await OrganizationCapacity.ConfigureAsync(db, capacity.OrganizationId,
            capacity.MaxActiveJobs ?? throw new InvalidOperationException(), capacity.MaxStoredBytes ?? throw new InvalidOperationException(), capacity.ExpectedVersion, default);
        else
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await OrganizationCapacity.LockAsync(db, capacity.OrganizationId, default);
            usage = await OrganizationCapacity.ReadAsync(db, capacity.OrganizationId, default);
            await tx.CommitAsync();
        }
        Console.WriteLine(JsonSerializer.Serialize(usage)); return 0;
    }
    if (args[0] is "api-role" or "verify-api-role")
    {
        var apiCommand = JsonSerializer.Deserialize<ApiRoleProvisioning>(input, jsonOptions) ?? throw new InvalidOperationException();
        if (string.IsNullOrWhiteSpace(apiCommand.ExpectedDatabase) || apiCommand.ExpectedDatabase != connection.Database ||
            connection.IncludeErrorDetail || connection.LogParameters) throw new InvalidOperationException();
        if (args[0] == "api-role") await ApiDatabasePolicy.ConfigureAsync(db, apiCommand.DatabaseRole, default);
        else await ApiDatabasePolicy.VerifyAsync(db, apiCommand.DatabaseRole, false, default);
        Console.WriteLine("API database grants verified against the explicit allowlist."); return 0;
    }
    if (args[0] == "worker-role")
    {
        var worker = JsonSerializer.Deserialize<WorkerRoleProvisioning>(input, jsonOptions) ?? throw new InvalidOperationException();
        await WorkerDatabasePolicy.ConfigureAsync(db, worker.DatabaseRole, CancellationToken.None);
        Console.WriteLine("Worker database grants configured and verified.");
        return 0;
    }
    if (args[0] == "worker-actor")
    {
        var worker = JsonSerializer.Deserialize<WorkerActorCommand>(input, jsonOptions) ?? throw new InvalidOperationException();
        var actorId = await WorkerActorProvisioning.ProvisionAsync(db, worker.OrganizationId, worker.DatabaseRole, CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(new { ActorId = actorId }));
        return 0;
    }
    var command = JsonSerializer.Deserialize<Provisioning>(input, jsonOptions) ?? throw new InvalidOperationException("Invalid provisioning input.");
    // Explicitly no migration, seed, server startup or paid-provider registration.
    var service = new EnterpriseMembershipService(db, new NoRequestIdentity(), audit);
    var result = await service.BootstrapAsync(command.Slug, command.Name, command.TenantId, command.ObjectId,
        command.AdministratorName, Guid.NewGuid().ToString("N"), CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(result));
    return 0;
}
catch (Exception)
{
    Console.Error.WriteLine("Provisioning failed. No credentials or identity payloads are logged. Check the input, migrated database and uniqueness constraints.");
    return 1;
}

internal sealed record Provisioning(string Slug, string Name, Guid TenantId, Guid ObjectId, string AdministratorName);
internal sealed record WorkerRoleProvisioning(string DatabaseRole);
internal sealed record ApiRoleProvisioning(string DatabaseRole, string ExpectedDatabase);
internal sealed record WorkerActorCommand(Guid OrganizationId, string DatabaseRole);
internal sealed record CapacityProvisioning(string ExpectedDatabase, Guid OrganizationId, int? MaxActiveJobs, long? MaxStoredBytes, Guid? ExpectedVersion);
internal sealed class NoRequestIdentity : IExecutionContext
{
    public Guid OrganizationId => throw new InvalidOperationException("No HTTP identity in the provisioning tool.");
    public Guid ActorId => throw new InvalidOperationException("No HTTP identity in the provisioning tool.");
}
