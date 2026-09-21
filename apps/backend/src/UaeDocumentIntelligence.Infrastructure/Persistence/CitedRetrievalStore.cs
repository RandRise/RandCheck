using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedRetrievalStore(AppDbContext db) : ICitedRetrievalStore
{
    public Task<CitedDocumentIndex?> GetIndexHeaderAsync(Guid organizationId, Guid indexId, CancellationToken ct) =>
        db.CitedDocumentIndexes.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == indexId, ct);

    public Task<Document?> GetSourceAsync(Guid organizationId, Guid documentId, CancellationToken ct) =>
        db.Documents.AsSplitQuery().Include(x => x.ExtractionRuns).ThenInclude(x => x.Pages)
            .Include(x => x.ExtractionRuns).ThenInclude(x => x.Fields).ThenInclude(x => x.Evidence)
            .Include(x => x.ReviewSessions).ThenInclude(x => x.Actions)
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == documentId, ct);

    public Task<CitedDocumentIndex?> FindIndexAsync(Guid organizationId, Guid reviewId, ChunkingProfile profile,
        CancellationToken ct) => db.CitedDocumentIndexes.AsNoTracking().Include(x => x.Chunks).ThenInclude(x => x.Spans)
        .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.ReviewSessionId == reviewId &&
            x.ProfileName == profile.Name && x.ProfileVersion == profile.Version, ct);

    public Task<CitedRetrievalRun?> FindRequestAsync(Guid organizationId, Guid actorId, string key,
        CancellationToken ct) => db.CitedRetrievalRuns.Include(x => x.Sources).Include(x => x.Hits)
        .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.ActorId == actorId && x.RequestKey == key, ct);

    public async Task<IReadOnlyList<DocumentChunk>> GetHitChunksAsync(CitedRetrievalRun run, CancellationToken ct)
    {
        var ids = run.Hits.Select(x => x.ChunkId).ToArray();
        return await db.DocumentChunks.Include(x => x.Spans)
            .Where(x => x.OrganizationId == run.OrganizationId && ids.Contains(x.Id)).ToArrayAsync(ct);
    }

    public async Task ExecuteLexicalAsync(CitedRetrievalRun run, LexicalPlan plan, CancellationToken ct)
    {
        plan.Validate(run.Question, plan.TopK);
        if (!db.Database.IsNpgsql())
            throw new CitedWorkflowException("postgresql_required", "Lexical retrieval requires PostgreSQL.", 503);
        var timer = Stopwatch.StartNew();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var nativeTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        var indexIds = run.Sources.Select(x => x.IndexId).ToArray();
        // Lock eligibility through hit persistence; a concurrent retirement waits
        // for this bounded retrieval transaction rather than producing mixed state.
        await using (var locks = new NpgsqlCommand("""
            SELECT id FROM cited_document_indexes
            WHERE organization_id=@org AND id=ANY(@ids) AND status='Ready'
            ORDER BY id FOR SHARE
            """, connection, nativeTransaction))
        {
            locks.CommandTimeout = 15;
            locks.Parameters.AddWithValue("org", run.OrganizationId);
            locks.Parameters.AddWithValue("ids", indexIds);
            await using var reader = await locks.ExecuteReaderAsync(ct);
            var count = 0;
            while (await reader.ReadAsync(ct)) count++;
            if (count != indexIds.Length)
                throw new CitedWorkflowException("index_ineligible", "The corpus changed eligibility before retrieval.");
        }
        var ranked = new List<(Guid Id, double Score)>();
        await using (var query = new NpgsqlCommand("""
            SELECT c.id, ts_rank_cd(to_tsvector('english'::regconfig, c.content), q.query)::double precision AS score
            FROM document_chunks c
            CROSS JOIN websearch_to_tsquery('english'::regconfig, @query) AS q(query)
            WHERE c.organization_id=@org AND c.index_id=ANY(@ids)
              AND to_tsvector('english'::regconfig, c.content) @@ q.query
            ORDER BY score DESC, c.id ASC LIMIT @limit
            """, connection, nativeTransaction))
        {
            query.CommandTimeout = 15;
            query.Parameters.AddWithValue("query", plan.SearchText);
            query.Parameters.AddWithValue("org", run.OrganizationId);
            query.Parameters.AddWithValue("ids", indexIds);
            query.Parameters.AddWithValue("limit", plan.TopK);
            await using var reader = await query.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) ranked.Add((reader.GetGuid(0), reader.GetDouble(1)));
        }
        var hitIds = ranked.Select(x => x.Id).ToArray();
        var chunks = await db.DocumentChunks.Where(x => x.OrganizationId == run.OrganizationId && hitIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        for (var rank = 0; rank < ranked.Count; rank++)
            run.AddHit(Guid.NewGuid(), chunks[ranked[rank].Id], rank + 1, ranked[rank].Score, null, null);
        run.MarkRetrieved(timer.ElapsedMilliseconds);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task FailAsync(Guid organizationId, Guid runId, string code, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var persisted = await db.CitedRetrievalRuns.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == runId, ct);
        if (persisted?.Status == CitedRetrievalStatus.Running)
        {
            persisted.Fail(code, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
        }
    }
}
