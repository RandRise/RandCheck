using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

/// <summary>
/// The pgvector table is migration-owned and accessed with parameterized SQL.
/// Cast real[] parameters to vector explicitly, keeping provider types out of the domain/EF model.
/// No caller-provided vector, source text, cost or organization is accepted by this port.
/// </summary>
internal sealed partial class CitedEmbeddingStore(DbContextOptions<AppDbContext> options, IExecutionContext execution,
    SecurityAuditContext? audit = null)
    : ICitedEmbeddingStore, ICitedSemanticRetrievalStore
{
    public Task<EmbeddingStorageResult> StoreChunkOutcomeAsync(Guid operationId, CancellationToken cancellationToken) =>
        WithOutcome(operationId, "chunk", async (db, tx, accepted, reservation, artifact, ct) =>
        {
            var inserted = 0;
            var chunks = await db.DocumentChunks.AsNoTracking().Where(x => x.OrganizationId == execution.OrganizationId &&
                accepted.IndexIds.Contains(x.IndexId) && accepted.Inputs.Select(i => i.Id).Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
            if (chunks.Count != accepted.Inputs.Length) throw Error("embedding_source_changed");
            for (var i = 0; i < accepted.Inputs.Length; i++)
            {
                var input = accepted.Inputs[i];
                var chunk = chunks[input.Id];
                if (chunk.Content != input.Text || chunk.ContentSha256 != input.Sha256 ||
                    CitedEmbeddingStorage.Digest(Encoding.UTF8.GetBytes(chunk.Content)) != input.Sha256)
                    throw Error("embedding_source_changed");
                await using var command = Command(db, tx, """
                    INSERT INTO cited_chunk_embeddings
                      (organization_id, index_id, chunk_id, input_sha256, provider, model, model_version,
                       dimension, distance, reservation_id, outcome_artifact_id, response_sha256, embedding)
                    VALUES (@org, @index, @chunk, @hash, 'openai', @model, @model, 1536, 'cosine',
                            @reservation, @artifact, @response, CAST(@values AS vector(1536)))
                    ON CONFLICT (organization_id, chunk_id, provider, model, model_version, dimension, distance)
                    DO NOTHING
                    """);
                command.Parameters.AddWithValue("org", execution.OrganizationId);
                command.Parameters.AddWithValue("index", chunk.IndexId);
                command.Parameters.AddWithValue("chunk", chunk.Id);
                command.Parameters.AddWithValue("hash", input.Sha256);
                command.Parameters.AddWithValue("model", CitedEmbeddingStorage.Model);
                command.Parameters.AddWithValue("reservation", reservation.Id);
                command.Parameters.AddWithValue("artifact", artifact.Id);
                command.Parameters.AddWithValue("response", accepted.ResponseSha256);
                command.Parameters.AddWithValue("values", accepted.Vectors[i]);
                var changed = await command.ExecuteNonQueryAsync(ct);
                if (changed == 0)
                {
                    // Reuse is allowed only for byte-identical float32 values and the same source/profile.
                    // A second operation with different output never overwrites the canonical vector.
                    await using var check = Command(db, tx, """
                        SELECT input_sha256=@hash AND index_id=@index AND embedding=CAST(@values AS vector(1536))
                        FROM cited_chunk_embeddings WHERE organization_id=@org AND chunk_id=@chunk
                          AND provider='openai' AND model=@model AND model_version=@model
                          AND dimension=1536 AND distance='cosine'
                        """);
                    foreach (var name in new[] { "org", "index", "chunk", "hash", "model", "values" })
                        check.Parameters.AddWithValue(name, command.Parameters[name].Value!);
                    if (await check.ExecuteScalarAsync(ct) is not true) throw Error("embedding_storage_conflict");
                }
                inserted += changed;
            }
            return new EmbeddingStorageResult(inserted, accepted.Inputs.Length - inserted);
        }, cancellationToken);

    public Task<IReadOnlyList<StoredEmbeddingHit>> SearchQueryOutcomeAsync(Guid operationId, int topK, CancellationToken cancellationToken)
    {
        if (topK is < 1 or > 20) throw Error("invalid_embedding_search");
        return WithOutcome<IReadOnlyList<StoredEmbeddingHit>>(operationId, "query",
            (db, tx, accepted, _, _, ct) => SearchAcceptedAsync(db, tx, accepted, topK, ct), cancellationToken);
    }

    private static async Task<IReadOnlyList<StoredEmbeddingHit>> SearchAcceptedAsync(AppDbContext db,
        IDbContextTransaction tx, AcceptedEmbeddingOutcome accepted, int topK, CancellationToken ct)
    {
            // An incomplete embedded corpus is not a valid vector evaluation. Do not silently omit chunks.
            await using var coverage = Command(db, tx, """
                SELECT count(*) FROM document_chunks c
                LEFT JOIN cited_chunk_embeddings e ON e.organization_id=c.organization_id AND e.index_id=c.index_id
                  AND e.chunk_id=c.id AND e.input_sha256=c.content_sha256 AND e.provider='openai'
                  AND e.model=@model AND e.model_version=@model AND e.dimension=1536 AND e.distance='cosine'
                WHERE c.organization_id=@org AND c.index_id=ANY(@ids) AND e.chunk_id IS NULL
                """);
            AddScope(coverage, accepted);
            if ((long)(await coverage.ExecuteScalarAsync(ct))! != 0) throw Error("embedding_corpus_incomplete");
            await using var search = Command(db, tx, """
                WITH eligible AS MATERIALIZED (
                    SELECT e.* FROM cited_chunk_embeddings e
                    JOIN document_chunks c ON c.organization_id=e.organization_id AND c.index_id=e.index_id
                      AND c.id=e.chunk_id AND c.content_sha256=e.input_sha256
                    WHERE e.organization_id=@org AND e.index_id=ANY(@ids) AND e.provider='openai'
                      AND e.model=@model AND e.model_version=@model AND e.dimension=1536 AND e.distance='cosine'
                )
                SELECT index_id, chunk_id, 1 - (embedding <=> CAST(@values AS vector(1536))) AS similarity
                FROM eligible ORDER BY embedding <=> CAST(@values AS vector(1536)), chunk_id LIMIT @limit
                """);
            AddScope(search, accepted);
            search.Parameters.AddWithValue("values", accepted.Vectors[0]);
            search.Parameters.AddWithValue("limit", topK);
            var result = new List<StoredEmbeddingHit>();
            await using var reader = await search.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var score = reader.GetDouble(2);
                if (!double.IsFinite(score)) throw Error("embedding_storage_corrupt", 503);
                result.Add(new(reader.GetGuid(0), reader.GetGuid(1), score));
            }
            return result;
    }

    private async Task<T> WithOutcome<T>(Guid operationId, string purpose,
        Func<AppDbContext, IDbContextTransaction, AcceptedEmbeddingOutcome, CitedSpendReservation, CitedGenerationArtifact, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty) throw Error("invalid_embedding_operation");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var ct = deadline.Token;
        // The isolated transaction must retain the request's audit identity when it
        // persists semantic retrieval history. Development contexts remain unbound.
        if (audit?.IsBound == true && (audit.OrganizationId != execution.OrganizationId || audit.ActorId != execution.ActorId))
            throw Error("embedding_scope_mismatch");
        await using var db = new AppDbContext(options, audit);
        if (!db.Database.IsNpgsql()) throw Error("embedding_requires_postgresql", 503);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Same lock order as spending; no ledger mutation or network inside this transaction.
            await using (var limits = Command(db, tx, "SET LOCAL lock_timeout = '5s'; SET LOCAL statement_timeout = '15s'"))
                await limits.ExecuteNonQueryAsync(ct);
            var budget = await db.CitedPilotBudgets.FromSqlRaw("SELECT * FROM cited_pilot_budgets WHERE id=1 FOR SHARE")
                .AsNoTracking().SingleAsync(ct);
            if (budget.RequiresReconciliation) throw Error("budget_reconciliation_required");
            if (!await db.Actors.FromSqlInterpolated($"SELECT * FROM actors WHERE organization_id={execution.OrganizationId} AND id={execution.ActorId} AND is_active FOR SHARE")
                    .AsNoTracking().AnyAsync(ct)) throw Error("embedding_operation_not_found", 404);
            var reservation = await db.CitedSpendReservations.AsNoTracking().SingleOrDefaultAsync(x =>
                x.OrganizationId == execution.OrganizationId && x.ActorId == execution.ActorId && x.OperationId == operationId, ct)
                ?? throw Error("embedding_operation_not_found", 404);
            if (reservation.Workload != CitedSpendWorkload.Embedding || reservation.State != CitedSpendState.Settled ||
                reservation.Provider != "openai" || reservation.Model != CitedEmbeddingStorage.Model ||
                reservation.ModelVersion != CitedEmbeddingStorage.Model || reservation.MaximumOutputTokens != 0)
                throw Error("embedding_operation_not_settled");
            var artifacts = await db.CitedGenerationArtifacts.AsNoTracking().Where(x =>
                x.OrganizationId == execution.OrganizationId && x.ActorId == execution.ActorId && x.ReservationId == reservation.Id)
                .ToArrayAsync(ct);
            var preparation = artifacts.SingleOrDefault(x => x.Kind == CitedGenerationArtifactKind.Preparation);
            var outcome = artifacts.SingleOrDefault(x => x.Kind == CitedGenerationArtifactKind.Outcome);
            if (preparation is null || outcome is null || preparation.PayloadSha256 != reservation.RequestSha256 ||
                artifacts.Any(x => CitedEmbeddingStorage.Digest(x.PayloadUtf8) != x.PayloadSha256))
                throw Error("embedding_artifact_corrupt", 503);
            var accepted = CitedEmbeddingStorage.Inspect(preparation.PayloadUtf8, outcome.PayloadUtf8);
            if (accepted.OrganizationId != execution.OrganizationId || accepted.ActorId != execution.ActorId ||
                accepted.OperationId != operationId || accepted.Purpose != purpose || accepted.InputTokens != reservation.MaximumInputTokens)
                throw Error("embedding_scope_mismatch");
            var rate = JsonSerializer.Deserialize<CitedGenerationRate>(reservation.RateSnapshotJson)
                ?? throw Error("embedding_settlement_mismatch");
            if (rate.Provider != reservation.Provider || rate.Model != reservation.Model || rate.ModelVersion != reservation.ModelVersion ||
                rate.RateVersion != reservation.RateVersion || rate.MaximumOutputUsdPerMillion != 0 || rate.MaximumInputUsdPerMillion <= 0 ||
                reservation.ActualUsd != decimal.Ceiling(accepted.InputTokens * rate.MaximumInputUsdPerMillion) / 1_000_000m)
                throw Error("embedding_settlement_mismatch");
            // Lock all selected immutable revisions through storage/search. Retirement must wait.
            await using (var sources = Command(db, tx, """
                SELECT i.id FROM cited_document_indexes i
                JOIN documents d ON d.organization_id=i.organization_id AND d.id=i.document_id AND d.sha256=i.document_sha256
                JOIN review_sessions r ON r.organization_id=i.organization_id AND r.id=i.review_session_id
                  AND r.document_id=i.document_id AND r.extraction_run_id=i.extraction_run_id AND r.status='Completed'
                WHERE i.organization_id=@org AND i.id=ANY(@ids) AND i.status='Ready'
                ORDER BY i.id FOR SHARE OF i, d, r
                """))
            {
                sources.Parameters.AddWithValue("org", execution.OrganizationId);
                sources.Parameters.AddWithValue("ids", accepted.IndexIds);
                await using var reader = await sources.ExecuteReaderAsync(ct);
                var count = 0;
                while (await reader.ReadAsync(ct)) count++;
                if (count != accepted.IndexIds.Length) throw Error("embedding_source_ineligible");
            }
            var result = await action(db, tx, accepted, reservation, outcome, ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception e) when (e is NpgsqlException or DbUpdateException or InvalidOperationException or JsonException or OverflowException)
        {
            throw Error("embedding_store_unavailable", 503); // Do not echo provider bytes/SQL parameters into errors.
        }
    }

    private static NpgsqlCommand Command(AppDbContext db, IDbContextTransaction tx, string sql) =>
        new(sql, (NpgsqlConnection)db.Database.GetDbConnection(), (NpgsqlTransaction)tx.GetDbTransaction()) { CommandTimeout = 15 };
    private static void AddScope(NpgsqlCommand command, AcceptedEmbeddingOutcome accepted)
    {
        command.Parameters.AddWithValue("org", accepted.OrganizationId);
        command.Parameters.AddWithValue("ids", accepted.IndexIds);
        command.Parameters.AddWithValue("model", CitedEmbeddingStorage.Model);
    }
    private static CitedWorkflowException Error(string code, int status = 409) =>
        new(code, "The embedding storage operation could not be completed.", status);
}
