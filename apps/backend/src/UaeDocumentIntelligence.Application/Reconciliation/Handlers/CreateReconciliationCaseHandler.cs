using System.Security.Cryptography;
using System.Text.Json;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class CreateReconciliationCaseHandler(IReconciliationRepository repository,
    IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationCaseView> HandleAsync(string name,
        CancellationToken cancellationToken, string? category = null, string? requestKey = null, string? workflowKind = null)
    {
        if (requestKey is not null && (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 100 || requestKey != requestKey.Trim()))
            throw new ReconciliationValidationException("invalid_request_key", "Request key must contain 1 to 100 characters.");
        var key = requestKey?.Trim();
        var parsedCategory = CaseMetadata.Category(category);
        var workflow = CaseMetadata.Workflow(workflowKind);
        var hash = key is null ? null : Convert.ToHexString(SHA256.HashData(
            workflow == ReconciliationWorkflowKind.Legacy
                ? JsonSerializer.SerializeToUtf8Bytes(new
                {
                    name = name?.Trim(),
                    category = parsedCategory.ToString()
                })
                : JsonSerializer.SerializeToUtf8Bytes(new
                {
                    name = name?.Trim(),
                    category = parsedCategory.ToString(),
                    workflowKind = workflow.ToString()
                }))).ToLowerInvariant();
        var item = new ReconciliationCase(Guid.NewGuid(), execution.OrganizationId,
            execution.ActorId, name!, clock.UtcNow, parsedCategory, key, hash, workflow);
        if (key is not null && await ExistingAsync(key, hash!, cancellationToken) is { } existing)
            return existing;
        await repository.AddAsync(item, cancellationToken);
        var outcome = await repository.SaveChangesAsync(cancellationToken);
        if (outcome != ReconciliationSaveOutcome.Saved)
        {
            // A simultaneous request can win the unique key while this transaction rolls back.
            if (key is not null && await ExistingAsync(key, hash!, cancellationToken) is { } winner)
                return winner;
            throw new ReconciliationConflictException("reconciliation_create_conflict",
                "The reconciliation case could not be created.");
        }
        return ReconciliationView.From(item);
    }

    private async Task<ReconciliationCaseView?> ExistingAsync(string key, string hash, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByCreationRequestAsync(execution.OrganizationId, execution.ActorId, key, cancellationToken);
        if (existing is null)
            return null;
        if (existing.CreationPayloadHash != hash)
            throw new ReconciliationConflictException("request_key_reused", "The request key belongs to different case details.");
        return ReconciliationView.From(existing);
    }
}
