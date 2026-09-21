namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record CitedCaseSource(Guid DocumentId, string Filename, Guid ReviewSessionId,
    Guid ExtractionRunId, int RevisionNumber);
public sealed record CitedCaseSnapshot(Guid Id, string Name, Guid Version, IReadOnlyList<CitedCaseSource> Sources);
public sealed record CitedCasePinnedSource(Guid DocumentId, string Filename, Guid ReviewSessionId,
    Guid ExtractionRunId, int RevisionNumber, Guid IndexId);
public sealed record CitedCaseSourcesView(Guid Id, string Name, Guid Version, IReadOnlyList<CitedCasePinnedSource> Sources);
public interface ICitedCaseSourceStore
{
    Task<CitedCaseSnapshot> ReadAsync(Guid id, CancellationToken ct);
}

public sealed class CitedCaseSourcesHandler(ICitedCaseSourceStore store, IndexReviewedDocumentHandler indexing)
{
    public async Task<CitedCaseSourcesView> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) throw new ArgumentException("Select a reconciliation case.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var ct = deadline.Token;
        var snapshot = await store.ReadAsync(id, ct);
        if (snapshot.Sources.Count is < 1 or > 3 || snapshot.Sources.Select(s => s.DocumentId).Distinct().Count() != snapshot.Sources.Count)
            throw new CitedWorkflowException("case_sources_unavailable", "The case needs distinct completed document reviews.");
        var pinned = new List<CitedCasePinnedSource>();
        foreach (var source in snapshot.Sources)
        {
            var index = await indexing.HandleAsync(source.DocumentId, source.ReviewSessionId, ct);
            pinned.Add(new(source.DocumentId, source.Filename, source.ReviewSessionId, source.ExtractionRunId,
                source.RevisionNumber, index.Id));
        }
        var current = await store.ReadAsync(id, ct);
        if (current.Version != snapshot.Version || !current.Sources.SequenceEqual(snapshot.Sources))
            throw new CitedWorkflowException("case_changed", "The case changed while loading. Load its sources again.");
        return new(snapshot.Id, snapshot.Name, snapshot.Version, pinned.AsReadOnly());
    }
}
