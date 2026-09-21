using System.Diagnostics;
using System.Text;
using System.Text.Json;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class CitedIndexAssemblyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-07T00:00:00Z");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Actual_python_output_roundtrips_into_pinned_domain_index_with_stable_spans()
    {
        var (doc, run, review) = Fixture();
        var indexId = Guid.NewGuid();
        var profile = new ChunkingProfile(64, 8);
        var snapshot = CitedIndexAssembly.CreateSnapshot(indexId, doc, review, run, profile);
        var first = await PythonChunks(snapshot);
        var second = await PythonChunks(snapshot);
        Assert.Equal(JsonSerializer.Serialize(first, Json), JsonSerializer.Serialize(second, Json));
        var index = CitedIndexAssembly.Assemble(indexId, doc, review, run, profile, first, Now);
        var replay = CitedIndexAssembly.Assemble(indexId, doc, review, run, profile, second, Now);
        Assert.Equal(DocumentIndexStatus.Ready, index.Status);
        Assert.Equal(index.Chunks.SelectMany(x => x.Spans).Select(x => x.Id),
            replay.Chunks.SelectMany(x => x.Spans).Select(x => x.Id));
        Assert.True(index.Chunks.Count > 10);
        Assert.DoesNotContain(index.Chunks, x => x.Content.StartsWith("old[", StringComparison.Ordinal));
        var total = Assert.Single(index.Chunks.Where(x => x.Content == "total[0]: 110.00"));
        Assert.Equal("100.00", Assert.Single(total.Spans).Quote);
        var region = Assert.Single(index.Chunks.Where(x => x.Content == "region[0]: 100.00"));
        Assert.Empty(region.Spans); // region remains reachable through the pinned review action
        Assert.NotNull(region.ReviewActionId);
        var regionProposal = Assert.Single(first.Chunks.Where(x => x.FieldKey == "region"));
        Assert.Single(regionProposal.EvidenceIds);
        Assert.Empty(Assert.Single(index.Chunks.Where(x => x.Content == "terms[0]: Net 30")).Spans);
        Assert.DoesNotContain(index.Chunks.SelectMany(x => x.Spans), x => x.PageNumber == 2);
    }

    [Theory]
    [InlineData("organization")]
    [InlineData("document")]
    [InlineData("review")]
    [InlineData("run")]
    [InlineData("index")]
    [InlineData("profile")]
    [InlineData("profile_name")]
    [InlineData("profile_version")]
    [InlineData("schema")]
    [InlineData("source_hash")]
    public void Foreign_or_stale_source_metadata_is_rejected(string mutation)
    {
        var (doc, run, review) = Fixture();
        var id = Guid.NewGuid();
        var profile = new ChunkingProfile();
        var result = Proposal(CitedIndexAssembly.CreateSnapshot(id, doc, review, run, profile));
        result = mutation switch
        {
            "organization" => result with { OrganizationId = Guid.NewGuid() },
            "document" => result with { DocumentId = Guid.NewGuid() },
            "review" => result with { ReviewSessionId = Guid.NewGuid() },
            "run" => result with { ExtractionRunId = Guid.NewGuid() },
            "index" => result with { IndexId = Guid.NewGuid() },
            "profile" => result with { Profile = new(1024, 0) },
            "profile_name" => result with { Profile = profile with { Name = "untrusted" } },
            "profile_version" => result with { Profile = profile with { Version = "2" } },
            "schema" => result with { SchemaVersion = "2" },
            _ => result with { DocumentSha256 = new string('b', 64) }
        };
        Assert.Throws<ArgumentException>(() => CitedIndexAssembly.Assemble(id, doc, review, run, profile, result, Now));
    }

    [Theory]
    [InlineData("content")]
    [InlineData("hash")]
    [InlineData("chunk_id")]
    [InlineData("ordinal")]
    [InlineData("kind")]
    [InlineData("page")]
    [InlineData("gap")]
    [InlineData("utf8")]
    [InlineData("missing_page")]
    [InlineData("missing_fact")]
    [InlineData("review_action")]
    [InlineData("evidence_id")]
    [InlineData("invented_fact_page")]
    [InlineData("duplicate_fact")]
    public void Tampered_chunk_proposals_cannot_publish_an_index(string mutation)
    {
        var (doc, run, review) = Fixture();
        var id = Guid.NewGuid();
        var profile = new ChunkingProfile();
        var result = Proposal(CitedIndexAssembly.CreateSnapshot(id, doc, review, run, profile));
        var chunks = result.Chunks.ToList();
        var factIndex = chunks.FindIndex(x => x.FieldKey == "total");
        switch (mutation)
        {
            case "content": chunks[0] = chunks[0] with { Content = "Forged total 999.00" }; break;
            case "hash": chunks[0] = chunks[0] with { ContentSha256 = new string('a', 64) }; break;
            case "chunk_id": chunks[0] = chunks[0] with { Id = Guid.NewGuid() }; break;
            case "ordinal": chunks[0] = chunks[0] with { Ordinal = 1 }; break;
            case "kind": chunks[0] = chunks[0] with { Kind = "Summary" }; break;
            case "page": chunks[0] = chunks[0] with { PageNumber = 999 }; break;
            case "gap": chunks[0] = chunks[0] with { ByteStart = 2 }; break;
            case "utf8": chunks[0] = chunks[0] with { ByteEnd = 1 }; break;
            case "missing_page": chunks.RemoveAt(0); Renumber(chunks, id); break;
            case "missing_fact": chunks.RemoveAt(factIndex); Renumber(chunks, id); break;
            case "review_action": chunks[factIndex] = chunks[factIndex] with { ReviewActionId = Guid.NewGuid() }; break;
            case "evidence_id": chunks[factIndex] = chunks[factIndex] with { EvidenceIds = [Guid.NewGuid()] }; break;
            case "invented_fact_page": chunks[factIndex] = chunks[factIndex] with { PageNumber = 1 }; break;
            case "duplicate_fact": chunks.Add(chunks[factIndex]); Renumber(chunks, id); break;
        }
        Assert.Throws<ArgumentException>(() => CitedIndexAssembly.Assemble(id, doc, review, run,
            profile, result with { Chunks = chunks }, Now));
        // Failed assembly has not modified the authoritative extraction/review.
        Assert.Equal("100.00", run.Fields.Single(x => x.FieldKey == "total").ValueText);
        Assert.Equal(ReviewStatus.Completed, review.Status);
    }

    [Fact]
    public void Snapshot_selects_latest_action_and_preserves_region_only_evidence_identity()
    {
        var (doc, run, review) = Fixture();
        var snapshot = CitedIndexAssembly.CreateSnapshot(Guid.NewGuid(), doc, review, run, new());
        Assert.Equal("110.00", snapshot.Facts.Single(x => x.FieldKey == "total").Value);
        var region = Assert.Single(snapshot.Facts.Single(x => x.FieldKey == "region").Evidence);
        Assert.Null(region.Quote);
        Assert.Null(region.ByteStart);
        Assert.Equal(run.Fields.Single(x => x.FieldKey == "region").Evidence.Single().Id, region.Id);
    }

    private static void Renumber(List<ChunkProposal> chunks, Guid indexId)
    {
        for (var ordinal = 0; ordinal < chunks.Count; ordinal++)
            chunks[ordinal] = chunks[ordinal] with { Ordinal = ordinal,
                Id = CitedChunkIdentity.Chunk(indexId, ordinal,
                    Enum.Parse<DocumentChunkKind>(chunks[ordinal].Kind), chunks[ordinal].ContentSha256) };
    }

    private static ChunkingResult Proposal(ChunkingSnapshot snapshot)
    {
        var chunks = new List<ChunkProposal>();
        foreach (var page in snapshot.Pages.Where(x => !string.IsNullOrWhiteSpace(x.CanonicalText)))
            chunks.Add(new(Guid.Empty, chunks.Count, "PageText", page.CanonicalText, Hash(page.CanonicalText),
                page.PageNumber, 0, Encoding.UTF8.GetByteCount(page.CanonicalText), null, null, null, []));
        foreach (var fact in snapshot.Facts.Where(x => x.Action != "Cleared"))
        {
            var content = $"{fact.FieldKey}[{fact.OccurrenceIndex}]: {fact.Value}";
            chunks.Add(new(Guid.Empty, chunks.Count, "ReviewedFact", content, Hash(content),
                null, null, null, fact.FieldKey, fact.OccurrenceIndex, fact.ReviewActionId,
                fact.Evidence.Select(x => x.Id).ToArray()));
        }
        Renumber(chunks, snapshot.IndexId);
        return new("1.0", snapshot.IndexId, snapshot.OrganizationId, snapshot.DocumentId,
            snapshot.ExtractionRunId, snapshot.ReviewSessionId, snapshot.DocumentSha256, snapshot.Profile, chunks);
    }

    private static string Hash(string text) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static async Task<ChunkingResult> PythonChunks(ChunkingSnapshot snapshot)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "apps", "api", "app")))
            root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("PHASE5_PYTHON") ?? "python")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true, StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-X");
        start.ArgumentList.Add("utf8");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import json,sys; sys.path.insert(0,sys.argv[1]); from app.cited_chunking import chunk_document; print(json.dumps(chunk_document(json.load(sys.stdin)), ensure_ascii=True))");
        start.ArgumentList.Add(Path.Combine(root!.FullName, "apps", "api"));
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(snapshot, Json));
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
        }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, await stderr);
        return JsonSerializer.Deserialize<ChunkingResult>(await stdout, Json)!;
    }

    private static (Document, ExtractionRun, ReviewSession) Fixture()
    {
        var doc = new Document(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf",
            Guid.NewGuid().ToString(), "application/pdf", 100, new string('a', 64), Now);
        var run = doc.QueueExtraction(Guid.NewGuid(), 1, "local", "1", "2.0", "test", Now);
        run.Start(Now);
        run.AddPage(Guid.NewGuid(), 1, "é Total: 100.00\n" + string.Concat(Enumerable.Repeat("A100 | 2 | AED 50.00\n", 40)), 0);
        run.AddPage(Guid.NewGuid(), 2, "\n\n", 0);
        run.AddPage(Guid.NewGuid(), 3, "TRN 100123456700003", 0);
        var total = run.AddField(Guid.NewGuid(), "total", 0, FieldValueType.Decimal, "100.00", 1, Now);
        run.AddEvidence(total, Guid.NewGuid(), 1, "100.00", 10, 16);
        var region = run.AddField(Guid.NewGuid(), "region", 0, FieldValueType.Decimal, "100.00", 1, Now);
        run.AddEvidence(region, Guid.NewGuid(), 1, null, null, null, "[[0,0],[1,0],[1,1],[0,1]]", "normalized");
        var old = run.AddField(Guid.NewGuid(), "old", 0, FieldValueType.String, "obsolete", 1, Now);
        run.AddEvidence(old, Guid.NewGuid(), 3, "TRN", 0, 3);
        run.RecordProviderMetrics(providerDurationMs: 1, pageCount: 3, null, null, null);
        run.Succeed("invoice", "en", Now, "test", "1");
        doc.RecordExtractionSuccess(run, Now);
        var review = doc.OpenReview(Guid.NewGuid(), doc.CreatedByActorId, Now);
        review.AddAction(Guid.NewGuid(), doc.CreatedByActorId, 1, ReviewActionType.Confirmed,
            "total", 0, total.Id, null, null, null, Now);
        review.AddAction(Guid.NewGuid(), doc.CreatedByActorId, 2, ReviewActionType.Corrected,
            "total", 0, total.Id, FieldValueType.Decimal, "110.00", "Checked", Now);
        review.AddAction(Guid.NewGuid(), doc.CreatedByActorId, 3, ReviewActionType.Confirmed,
            "region", 0, region.Id, null, null, null, Now);
        review.AddAction(Guid.NewGuid(), doc.CreatedByActorId, 4, ReviewActionType.Cleared,
            "old", 0, old.Id, null, null, "Removed", Now);
        review.AddAction(Guid.NewGuid(), doc.CreatedByActorId, 5, ReviewActionType.Added,
            "terms", 0, null, FieldValueType.String, "Net 30", "Added", Now);
        review.Complete(run.Fields, Now);
        return (doc, run, review);
    }
}
