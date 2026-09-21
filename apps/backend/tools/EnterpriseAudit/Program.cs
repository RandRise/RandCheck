using System.Text.Json;
using UaeDocumentIntelligence.Infrastructure.Persistence;

if (!(args.Length == 2 && args[0] == "export") && !(args.Length == 3 && args[0] == "verify"))
{
    Console.Error.WriteLine("Usage: EnterpriseAudit export <private-plan.json> | verify <archive-directory> <trusted-manifest-sha256>. Export requires PHASE6_AUDIT_CONNECTION.");
    return 2;
}
try
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    AuditExportReceipt receipt;
    if (args[0] == "verify") receipt = await AuditArchive.VerifyAsync(args[1], args[2], deadline.Token);
    else
    {
        var command = AuditArchive.Parse<ExportCommand>(await AuditArchive.ReadBoundedAsync(Path.GetFullPath(args[1]), 16384, deadline.Token));
        var request = new AuditExportRequest(command.ExpectedDatabase, command.OrganizationId, command.FromUtc, command.BeforeUtc, command.MaxRows);
        await using var snapshot = await AuditExportSnapshot.OpenAsync(
            Environment.GetEnvironmentVariable("PHASE6_AUDIT_CONNECTION") ?? throw new InvalidOperationException(), request, deadline.Token);
        receipt = await AuditArchive.WriteAsync(command.OutputDirectory, snapshot.Scope, snapshot.ReadAsync(deadline.Token), deadline.Token);
    }
    Console.WriteLine(JsonSerializer.Serialize(receipt, AuditArchive.JsonOptions));
    return 0;
}
catch
{
    Console.Error.WriteLine("Audit operation failed. Check the private input, database permissions, bounds, archive and trusted receipt. No credentials or audit payloads are logged.");
    return 1;
}

internal sealed record ExportCommand(string ExpectedDatabase, Guid OrganizationId, DateTimeOffset FromUtc,
    DateTimeOffset BeforeUtc, string OutputDirectory, int MaxRows = 10000);
