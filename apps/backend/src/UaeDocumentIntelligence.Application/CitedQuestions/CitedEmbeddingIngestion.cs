using System.Text.Json;
using UaeDocumentIntelligence.Domain.Model;
using static UaeDocumentIntelligence.Application.CitedQuestions.CitedEmbeddingStorage;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record EmbeddingOperation(Guid OperationId, string Purpose, Guid[] IndexIds, Guid[] ChunkIds, string? Question = null)
{
    public override string ToString() => "Embedding operation (content omitted)";
}
public sealed record EmbeddingSource(byte[] Utf8, int ExistingCount, int InputCount)
{
    public override string ToString() => "Embedding source (content omitted)";
}
public interface ICitedEmbeddingSources
{
    Task<IAsyncDisposable> AcquireAsync(CancellationToken ct);
    Task<EmbeddingSource> LoadAsync(EmbeddingOperation operation, CancellationToken ct);
}
public interface ICitedEmbeddingClient
{
    Task<byte[]> PrepareAsync(byte[] source, CancellationToken ct);
    Task<byte[]> ProposeAsync(byte[] dispatch, CancellationToken ct);
}
public sealed record CitedEmbeddingSettings(bool Enabled = false, CitedGenerationRate? Rate = null);
public sealed record EmbeddingIngestionResult(Guid OperationId, Guid? ReservationId, bool Replayed,
    EmbeddingStorageResult? Storage, IReadOnlyList<StoredEmbeddingHit>? Hits);
public sealed record EmbeddingAccounting(decimal? Cost, string? Evidence, bool RequiresReconciliation);

public static class CitedEmbeddingProtocol
{
    public static byte[] Dispatch(byte[] source, byte[] receipt)
    {
        try
        {
            using var p = Parse(receipt, 8192);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { requestUtf8 = source, preparation = p.RootElement });
            _ = ReadPreparation(bytes);
            return bytes;
        }
        catch { throw Failure("invalid_embedding_preparation"); }
    }

    public static PreparedEmbeddingSource Prepared(byte[] dispatch)
    {
        try { return ReadPreparation(dispatch); }
        catch { throw Failure("invalid_embedding_preparation"); }
    }

    // Independent raw usage verification, separate from vector reusability.
    // Malformed vectors still cost money; unknown usage must retain its hold.
    public static EmbeddingAccounting Accounting(byte[] dispatch, byte[] outcome, CitedGenerationRate rate)
    {
        try
        {
            var prepared = ReadPreparation(dispatch);
            Require(rate.Provider == "openai" && rate.Model == Model && rate.ModelVersion == Model &&
                rate.MaximumInputUsdPerMillion > 0 && rate.MaximumOutputUsdPerMillion == 0);
            using var envelope = Parse(outcome, 524288);
            var o = envelope.RootElement;
            Exact(o, "schemaVersion sourceSha256 bodySha256 bindingSha256 model counterVersion wireVersion measuredInputTokens inputTokens outputTokens accountingKnown outcome requiresReconciliation providerRequestId httpStatus responseSha256 responseUtf8 publicationAllowed");
            CheckModel(o.GetProperty("model"));
            foreach (var field in new[] { "schemaVersion", "sourceSha256", "bodySha256", "bindingSha256", "counterVersion", "wireVersion" })
                Require(o.GetProperty(field).GetString() == prepared.Receipt.GetProperty(field).GetString());
            Require(o.GetProperty("measuredInputTokens").GetInt32() == prepared.InputTokens &&
                o.GetProperty("outputTokens").GetInt32() == 0 && !o.GetProperty("publicationAllowed").GetBoolean());
            if (o.GetProperty("responseUtf8").ValueKind == JsonValueKind.Null) return new(null, null, true);
            var rawBytes = Convert.FromBase64String(o.GetProperty("responseUtf8").GetString()!);
            Require(Digest(rawBytes) == o.GetProperty("responseSha256").GetString());
            var requestId = o.GetProperty("providerRequestId").GetString();
            Require(requestId is not null && requestId.StartsWith("req_", StringComparison.Ordinal) &&
                requestId.Length is > 4 and <= 200 && requestId.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'));
            using var raw = Parse(rawBytes, 262144);
            var r = raw.RootElement;
            Exact(r, "object model data usage");
            Require(r.GetProperty("model").GetString() == Model && r.GetProperty("object").GetString() == "list" &&
                o.GetProperty("httpStatus").GetInt32() == 200);
            var usage = r.GetProperty("usage");
            Exact(usage, "prompt_tokens total_tokens");
            var tokens = usage.GetProperty("prompt_tokens").GetInt32();
            Require(tokens > 0 && usage.GetProperty("total_tokens").GetInt32() == tokens);
            var mismatch = tokens != prepared.InputTokens || !o.GetProperty("accountingKnown").GetBoolean() ||
                o.GetProperty("inputTokens").ValueKind != JsonValueKind.Number || o.GetProperty("inputTokens").GetInt32() != tokens;
            var cost = decimal.Ceiling(tokens * rate.MaximumInputUsdPerMillion) / 1_000_000m;
            return new(cost, "embedding-response:" + Digest(rawBytes), mismatch || o.GetProperty("requiresReconciliation").GetBoolean());
        }
        catch { return new(null, null, true); } // Never label an unbound or unrecognized result free.
    }

    internal static CitedWorkflowException Failure(string code) => new(code, "The private embedding operation could not be completed.");
}

/// <summary>Private pilot coordinator. No route or live configuration is enabled by default.</summary>
public sealed class CitedEmbeddingCoordinator(ICitedEmbeddingSources sources, ICitedEmbeddingClient client,
    ICitedGenerationLedger ledger, ICitedGenerationJournal journal, ICitedEmbeddingStore storage, CitedEmbeddingSettings settings)
{
    public async Task<EmbeddingIngestionResult> RunAsync(EmbeddingOperation operation, CancellationToken ct)
    {
        if (!settings.Enabled || settings.Rate is not { } rate || rate.Provider != "openai" ||
            rate.Model != Model || rate.ModelVersion != Model || rate.MaximumOutputUsdPerMillion != 0)
            throw CitedEmbeddingProtocol.Failure("embedding_disabled");
        if (operation is null || operation.IndexIds is null || operation.ChunkIds is null)
            throw CitedEmbeddingProtocol.Failure("invalid_embedding_operation");
        // Freeze mutable caller arrays before the first await.
        operation = operation with { IndexIds = operation.IndexIds.ToArray(), ChunkIds = operation.ChunkIds.ToArray() };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var token = deadline.Token;
        await ledger.ValidateCallerAsync(token);
        // Serialize embedding ingestion across processes, including different operation IDs.
        // No budget/source row lock is held during HTTP; a process crash releases this advisory lease.
        await using var lease = await sources.AcquireAsync(token);
        var source = await sources.LoadAsync(operation, token);
        var saved = await journal.GetSavedAsync(operation.OperationId, token);
        byte[] dispatch;
        if (saved is not null)
        {
            if (saved.Spend.Workload != CitedSpendWorkload.Embedding || saved.PreparationUtf8 is null)
                throw CitedEmbeddingProtocol.Failure("reservation_key_conflict");
            dispatch = saved.PreparationUtf8;
            var prior = CitedEmbeddingProtocol.Prepared(dispatch);
            if (!prior.SourceUtf8.AsSpan().SequenceEqual(source.Utf8)) throw CitedEmbeddingProtocol.Failure("embedding_saved_source_changed");
            if (saved.Spend.State != CitedSpendState.Reserved)
            {
                if (saved.Spend.State != CitedSpendState.Settled || saved.OutcomeUtf8 is null)
                    throw CitedEmbeddingProtocol.Failure("embedding_already_dispatched");
                return await Reuse(operation, saved.Spend.Id, true, token);
            }
        }
        else
        {
            if (operation.Purpose == "chunk" && source.ExistingCount > 0)
            {
                if (source.ExistingCount != source.InputCount) throw CitedEmbeddingProtocol.Failure("embedding_batch_partially_stored");
                return new(operation.OperationId, null, true, new(0, source.InputCount), null);
            }
            dispatch = CitedEmbeddingProtocol.Dispatch(source.Utf8, await client.PrepareAsync(source.Utf8, token));
        }
        var prepared = CitedEmbeddingProtocol.Prepared(dispatch);
        var current = await sources.LoadAsync(operation, token);
        if (!current.Utf8.AsSpan().SequenceEqual(source.Utf8)) throw CitedEmbeddingProtocol.Failure("embedding_source_changed");
        var reservation = await journal.ReservePreparedAsync(new(operation.OperationId, Digest(dispatch), rate,
            prepared.InputTokens, 0, CitedSpendWorkload.Embedding), dispatch, true, token);
        // Recheck source/caller after admission, immediately before committed dispatch.
        try { _ = await sources.LoadAsync(operation, token); }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ledger.CancelBeforeDispatchAsync(reservation.Id, cleanup.Token);
            throw;
        }
        if (!await ledger.TryMarkDispatchedAsync(reservation.Id, true, token))
            throw CitedEmbeddingProtocol.Failure("embedding_already_dispatched");
        var recorded = false;
        try
        {
            var outcome = await client.ProposeAsync(dispatch, token);
            var accounting = CitedEmbeddingProtocol.Accounting(dispatch, outcome, rate);
            // Cancellation after provider output must not intentionally discard known usage.
            using var persist = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await journal.RecordOutcomeAsync(reservation.Id, outcome, accounting.Cost, accounting.Evidence,
                accounting.RequiresReconciliation, persist.Token);
            recorded = true;
            if (accounting.Cost is null || accounting.RequiresReconciliation)
                throw CitedEmbeddingProtocol.Failure("embedding_outcome_uncertain");
            return await Reuse(operation, reservation.Id, false, token);
        }
        catch
        {
            if (!recorded)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await ledger.MarkUncertainAsync(reservation.Id, cleanup.Token); }
                catch { /* A committed dispatch still retains its hold; never retry HTTP or release it. */ }
            }
            throw;
        }
    }

    private async Task<EmbeddingIngestionResult> Reuse(EmbeddingOperation operation, Guid reservationId, bool replayed, CancellationToken ct) =>
        operation.Purpose == "chunk"
            ? new(operation.OperationId, reservationId, replayed, await storage.StoreChunkOutcomeAsync(operation.OperationId, ct), null)
            : new(operation.OperationId, reservationId, replayed, null, await storage.SearchQueryOutcomeAsync(operation.OperationId, 5, ct));
}
