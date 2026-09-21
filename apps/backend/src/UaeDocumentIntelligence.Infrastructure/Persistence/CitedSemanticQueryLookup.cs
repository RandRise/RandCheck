using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed partial class CitedEmbeddingStore
{
    public async Task<Guid> FindQueryAsync(string question, IReadOnlyList<Guid> indexIds, string requestKey, CancellationToken ct)
    {
        await using var db = new AppDbContext(options);
        if (!await db.Actors.AsNoTracking().AnyAsync(a => a.OrganizationId == execution.OrganizationId &&
                a.Id == execution.ActorId && a.IsActive, ct)) throw Error("embedding_operation_not_found", 404);
        var existing = await db.CitedRetrievalRuns.AsNoTracking().Include(r => r.Sources).SingleOrDefaultAsync(r =>
            r.OrganizationId == execution.OrganizationId && r.ActorId == execution.ActorId && r.RequestKey == requestKey, ct);
        if (existing is not null)
        {
            if (existing.Question != question || !existing.Sources.Select(s => s.IndexId).Order().SequenceEqual(indexIds.Order()) ||
                existing.PolicyVersion is not (SemanticRetrievalPolicy.VectorVersion or SemanticRetrievalPolicy.HybridVersion or SemanticRetrievalPolicy.NonredundantVersion))
                throw Error("request_key_reused");
            using var policy = JsonDocument.Parse(existing.PolicyJson);
            return policy.RootElement.GetProperty("queryOperationId").GetGuid();
        }
        // Bounded development lookup. The canonical retrieval transaction revalidates the
        // selected outcome, accounting, actor and all source eligibility before using it.
        var records = await db.CitedSpendReservations.AsNoTracking().Where(r => r.OrganizationId == execution.OrganizationId &&
            r.ActorId == execution.ActorId && r.Workload == CitedSpendWorkload.Embedding && r.State == CitedSpendState.Settled &&
            r.Model == CitedEmbeddingStorage.Model).OrderByDescending(r => r.CreatedAtUtc).Take(200).ToArrayAsync(ct);
        foreach (var record in records)
        {
            var artifacts = await db.CitedGenerationArtifacts.AsNoTracking().Where(a => a.OrganizationId == execution.OrganizationId &&
                a.ActorId == execution.ActorId && a.ReservationId == record.Id).ToArrayAsync(ct);
            var prep = artifacts.SingleOrDefault(a => a.Kind == CitedGenerationArtifactKind.Preparation);
            var outcome = artifacts.SingleOrDefault(a => a.Kind == CitedGenerationArtifactKind.Outcome);
            if (prep is null || outcome is null) continue;
            AcceptedEmbeddingOutcome accepted;
            try { accepted = CitedEmbeddingStorage.Inspect(prep.PayloadUtf8, outcome.PayloadUtf8); }
            catch (CitedWorkflowException) { continue; }
            if (accepted.Purpose == "query" && accepted.OrganizationId == execution.OrganizationId &&
                accepted.ActorId == execution.ActorId && accepted.OperationId == record.OperationId &&
                accepted.Inputs.Length == 1 && accepted.Inputs[0].Text == question &&
                accepted.IndexIds.Order().SequenceEqual(indexIds.Order())) return record.OperationId;
        }
        throw new CitedWorkflowException("semantic_query_unavailable",
            "No reusable semantic search is available for this question and selection. Use keyword search or reviewed fields.", 422);
    }
}
