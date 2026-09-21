using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedEmbeddingSources(DbContextOptions<AppDbContext> options, IExecutionContext execution) : ICitedEmbeddingSources
{
    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken ct)
    {
        var db = new AppDbContext(options);
        try
        {
            if (!db.Database.IsNpgsql()) throw Failure("embedding_requires_postgresql");
            var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='10s'; SELECT pg_advisory_xact_lock(5930145561::bigint)", ct);
                return new Lease(db, tx);
            }
            catch { await tx.DisposeAsync(); throw; }
        }
        catch { await db.DisposeAsync(); throw Failure("embedding_ingestion_busy"); }
    }

    public async Task<EmbeddingSource> LoadAsync(EmbeddingOperation operation, CancellationToken ct)
    {
        if (operation.OperationId == Guid.Empty || operation.Purpose is not ("chunk" or "query") ||
            operation.IndexIds.Length is < 1 or > 20 || operation.IndexIds.Any(x => x == Guid.Empty) ||
            operation.IndexIds.Distinct().Count() != operation.IndexIds.Length ||
            operation.Purpose == "chunk" && (operation.Question is not null || operation.ChunkIds.Length is < 1 or > 4 ||
                operation.ChunkIds.Any(x => x == Guid.Empty) || operation.ChunkIds.Distinct().Count() != operation.ChunkIds.Length) ||
            operation.Purpose == "query" && (operation.ChunkIds.Length != 0 || string.IsNullOrWhiteSpace(operation.Question) ||
                operation.Question.Length > 4000 || operation.Question != operation.Question.Trim()))
            throw Failure("invalid_embedding_operation");
        var indexes = operation.IndexIds.OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
        await using var db = new AppDbContext(options);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='15s'", ct);
            if (!await db.Actors.FromSqlInterpolated($"SELECT * FROM actors WHERE organization_id={execution.OrganizationId} AND id={execution.ActorId} AND is_active FOR SHARE")
                    .AsNoTracking().AnyAsync(ct)) throw Failure("embedding_source_not_found");
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using (var command = new NpgsqlCommand("""
                SELECT i.id FROM cited_document_indexes i
                JOIN documents d ON d.organization_id=i.organization_id AND d.id=i.document_id AND d.sha256=i.document_sha256
                JOIN review_sessions r ON r.organization_id=i.organization_id AND r.id=i.review_session_id
                  AND r.document_id=i.document_id AND r.extraction_run_id=i.extraction_run_id AND r.status='Completed'
                WHERE i.organization_id=@org AND i.id=ANY(@ids) AND i.status='Ready'
                ORDER BY i.id FOR SHARE OF i,d,r
                """, connection, (NpgsqlTransaction)tx.GetDbTransaction()) { CommandTimeout = 15 })
            {
                command.Parameters.AddWithValue("org", execution.OrganizationId);
                command.Parameters.AddWithValue("ids", indexes);
                await using var reader = await command.ExecuteReaderAsync(ct);
                var count = 0;
                while (await reader.ReadAsync(ct)) count++;
                if (count != indexes.Length) throw Failure("embedding_source_ineligible");
            }
            StoredEmbeddingInput[] inputs;
            var existing = 0;
            if (operation.Purpose == "chunk")
            {
                var chunks = await db.DocumentChunks.AsNoTracking().Where(x => x.OrganizationId == execution.OrganizationId &&
                    indexes.Contains(x.IndexId) && operation.ChunkIds.Contains(x.Id)).ToArrayAsync(ct);
                if (chunks.Length != operation.ChunkIds.Length) throw Failure("embedding_source_not_found");
                inputs = chunks.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal)
                    .Select(x => new StoredEmbeddingInput(x.Id, x.Content, x.ContentSha256)).ToArray();
                await using var count = new NpgsqlCommand("""
                    SELECT count(*) FROM cited_chunk_embeddings e JOIN document_chunks c
                      ON c.organization_id=e.organization_id AND c.index_id=e.index_id AND c.id=e.chunk_id
                      AND c.content_sha256=e.input_sha256
                    WHERE e.organization_id=@org AND e.chunk_id=ANY(@chunks) AND e.index_id=ANY(@ids)
                      AND e.provider='openai' AND e.model=@model AND e.model_version=@model
                      AND e.dimension=1536 AND e.distance='cosine'
                    """, connection, (NpgsqlTransaction)tx.GetDbTransaction()) { CommandTimeout = 15 };
                count.Parameters.AddWithValue("org", execution.OrganizationId);
                count.Parameters.AddWithValue("chunks", operation.ChunkIds);
                count.Parameters.AddWithValue("ids", indexes);
                count.Parameters.AddWithValue("model", CitedEmbeddingStorage.Model);
                existing = checked((int)(long)(await count.ExecuteScalarAsync(ct))!);
            }
            else
            {
                await using var coverage = new NpgsqlCommand("""
                    SELECT count(*) FROM document_chunks c LEFT JOIN cited_chunk_embeddings e
                      ON e.organization_id=c.organization_id AND e.index_id=c.index_id AND e.chunk_id=c.id
                      AND e.input_sha256=c.content_sha256 AND e.provider='openai' AND e.model=@model
                      AND e.model_version=@model AND e.dimension=1536 AND e.distance='cosine'
                    WHERE c.organization_id=@org AND c.index_id=ANY(@ids) AND e.chunk_id IS NULL
                    """, connection, (NpgsqlTransaction)tx.GetDbTransaction()) { CommandTimeout = 15 };
                coverage.Parameters.AddWithValue("org", execution.OrganizationId);
                coverage.Parameters.AddWithValue("ids", indexes);
                coverage.Parameters.AddWithValue("model", CitedEmbeddingStorage.Model);
                if ((long)(await coverage.ExecuteScalarAsync(ct))! != 0) throw Failure("embedding_corpus_incomplete");
                inputs = [new(operation.OperationId, operation.Question!, CitedEmbeddingStorage.Digest(Encoding.UTF8.GetBytes(operation.Question!)))];
            }
            if (inputs.Any(x => string.IsNullOrWhiteSpace(x.Text) || Encoding.UTF8.GetByteCount(x.Text) > 8000 ||
                CitedEmbeddingStorage.Digest(Encoding.UTF8.GetBytes(x.Text)) != x.Sha256)) throw Failure("embedding_source_changed");
            var source = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = CitedEmbeddingStorage.Schema,
                context = new { organizationId = execution.OrganizationId, actorId = execution.ActorId,
                    operationId = operation.OperationId, purpose = operation.Purpose, indexIds = indexes },
                model = new { providerAlias = "openai", model = CitedEmbeddingStorage.Model,
                    version = CitedEmbeddingStorage.Model, dimension = 1536, distance = "cosine" },
                inputs = inputs.Select(x => new { inputId = x.Id, text = x.Text, inputSha256 = x.Sha256 })
            });
            if (source.Length > 49152) throw Failure("embedding_source_too_large");
            await tx.CommitAsync(ct);
            return new(source, existing, inputs.Length);
        }
        catch (Exception e) when (e is NpgsqlException or InvalidOperationException)
        { throw Failure("embedding_source_unavailable"); }
    }

    private sealed class Lease(AppDbContext db, IDbContextTransaction tx) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { try { await tx.DisposeAsync(); } finally { await db.DisposeAsync(); } }
    }
    private static CitedWorkflowException Failure(string code) => new(code, "The embedding source could not be loaded.");
}
