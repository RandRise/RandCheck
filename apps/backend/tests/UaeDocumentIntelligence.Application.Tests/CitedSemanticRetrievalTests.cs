using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Application.Tests;

public class CitedSemanticRetrievalTests
{
    [Fact]
    public void Nonredundant_selection_keeps_page_context_without_changing_scores()
    {
        var page = Guid.NewGuid(); var fact = Guid.NewGuid(); var next = Guid.NewGuid();
        var org = Guid.NewGuid(); var index = Guid.NewGuid();
        var evidence = new Dictionary<Guid, SemanticEvidence>
        {
            [page] = new(page, org, index, "PageText", "Name: workshop", false, [new(1,0,14,"Name: workshop")]),
            [fact] = new(fact, org, index, "ReviewedFact", "description[2]: workshop", true, [new(1,6,14,"workshop")]),
            [next] = new(next, org, index, "PageText", "Price: 1200", false, [new(2,0,11,"Price: 1200")])
        };
        SemanticCandidate[] ranked = [new(page,1,.5,.03),new(fact,1,.4,.02),new(next,1,.3,.01)];
        var selected = SemanticRetrievalPolicy.SelectNonredundant(ranked,evidence,2);
        Assert.Equal(new[] { ranked[0], ranked[2] }, selected);
        // A lower-ranked parent cannot remove evidence from an earlier selected fact.
        Assert.Equal(new[] { ranked[1], ranked[0] }, SemanticRetrievalPolicy.SelectNonredundant(
            [ranked[1],ranked[0],ranked[2]],evidence,2));
        foreach (var changed in new[] {
            evidence[fact] with { IsConfirmed = false },
            evidence[fact] with { Content = "description[2]: corrected value" },
            evidence[fact] with { IndexId = Guid.NewGuid() },
            evidence[fact] with { OrganizationId = Guid.NewGuid() },
            evidence[fact] with { Spans = [] },
            evidence[fact] with { Spans = [new(2,6,14,"workshop")] },
            evidence[fact] with { Spans = [new(1,5,13,"workshop")] }
        })
        {
            var variant = new Dictionary<Guid, SemanticEvidence>(evidence) { [fact] = changed };
            Assert.Equal(new[] { ranked[0],ranked[1] }, SemanticRetrievalPolicy.SelectNonredundant(ranked,variant,2));
        }
    }

    [Fact]
    public void Nonredundant_selection_uses_utf8_offsets_and_rejects_missing_metadata()
    {
        var page = Guid.NewGuid(); var fact = Guid.NewGuid(); var org = Guid.NewGuid(); var index = Guid.NewGuid();
        var evidence = new Dictionary<Guid, SemanticEvidence> {
            [page] = new(page,org,index,"PageText","é: café",false,[new(1,0,9,"é: café")]),
            [fact] = new(fact,org,index,"ReviewedFact","name[1]: café",true,[new(1,4,9,"café")])
        };
        SemanticCandidate[] ranked = [new(page,1,.5,.03),new(fact,1,.4,.02)];
        Assert.Single(SemanticRetrievalPolicy.SelectNonredundant(ranked,evidence,2));
        evidence.Remove(fact);
        Assert.Throws<CitedWorkflowException>(() => SemanticRetrievalPolicy.SelectNonredundant(ranked,evidence,2));
    }
    [Fact]
    public void Fusion_rewards_agreement_and_preserves_component_scores()
    {
        var a = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var b = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var c = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var result = SemanticRetrievalPolicy.Fuse([(a, 100), (b, .1)],
            [new(Guid.NewGuid(), c, .99), new(Guid.NewGuid(), b, .01)], 3);
        Assert.Equal(new[] { b, a, c }, result.Select(x => x.ChunkId));
        Assert.Equal(2d / 62, result[0].FusedScore);
        Assert.Equal(.1, result[0].LexicalScore);
        Assert.Equal(.01, result[0].VectorScore);
        Assert.Null(result[1].VectorScore);
        Assert.Null(result[2].LexicalScore);
    }

    [Fact]
    public void Fusion_has_canonical_ties_bounded_output_and_allows_empty_leg()
    {
        var a = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var b = Guid.Parse("20000000-0000-0000-0000-000000000000");
        Assert.Equal(a, Assert.Single(SemanticRetrievalPolicy.Fuse([(b, .2)], [new(Guid.NewGuid(), a, .3)], 1)).ChunkId);
        Assert.Equal(b, Assert.Single(SemanticRetrievalPolicy.Fuse([], [new(Guid.NewGuid(), b, -.2)], 5)).ChunkId);
        Assert.Empty(SemanticRetrievalPolicy.Fuse([], [], 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Fusion_rejects_invalid_bound(int limit) =>
        Assert.Throws<CitedWorkflowException>(() => SemanticRetrievalPolicy.Fuse([], [], limit));

    [Fact]
    public void Fusion_rejects_duplicate_and_nonfinite_candidates()
    {
        var id = Guid.NewGuid();
        Assert.Throws<CitedWorkflowException>(() => SemanticRetrievalPolicy.Fuse([(id, 1), (id, 2)], [], 5));
        Assert.Throws<CitedWorkflowException>(() => SemanticRetrievalPolicy.Fuse([(id, double.NaN)], [], 5));
        Assert.Throws<CitedWorkflowException>(() => SemanticRetrievalPolicy.Fuse([], [new(id, id, double.PositiveInfinity)], 5));
    }
}
