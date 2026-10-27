using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class CitedServiceRegistrationTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Database"] = "Data Source=:memory:",
        ["AiService:BaseUrl"] = "http://127.0.0.1:1",
        ["CitedGeneration:Enabled"] = "true",
        ["CitedEmbedding:Enabled"] = "true",
    }).Build();

    [Fact]
    public void Core_graph_resolves_public_question_workflows_without_optional_pilot_services()
    {
        using var services = new ServiceCollection().AddInfrastructure(Configuration()).BuildServiceProvider();
        using var scope = services.CreateScope();
        var core = scope.ServiceProvider;
        Assert.NotNull(core.GetRequiredService<IndexReviewedDocumentHandler>());
        Assert.NotNull(core.GetRequiredService<RetrieveEvidenceHandler>());
        Assert.NotNull(core.GetRequiredService<CitedQuestionHandler>()); // Includes read-only saved results.
        Assert.NotNull(core.GetRequiredService<CitedReviewedQuestionHandler>());
        Assert.NotNull(core.GetRequiredService<CitedSemanticQuestionHandler>());
        Assert.NotNull(core.GetRequiredService<CitedWorkspaceHandler>());
        Assert.NotNull(core.GetRequiredService<CitedCaseSourcesHandler>());
        Assert.NotNull(core.GetRequiredService<ICitedSemanticRetrievalStore>());
        Assert.Null(core.GetService<CitedGenerationCoordinator>());
        Assert.Null(core.GetService<CitedEmbeddingCoordinator>());
        Assert.Null(core.GetService<ICitedAnswerClient>());
        Assert.Null(core.GetService<ICitedEmbeddingClient>());
        Assert.Null(core.GetService<ICitedGenerationLedger>());
        Assert.Null(core.GetService<ICitedPilotUnknownHold>());
        Assert.Null(core.GetService<AssessCitedRecovery>());
    }

    [Fact]
    public async Task Explicit_pilot_graph_keeps_generation_disabled_and_recovery_denied()
    {
        var config = Configuration();
        using var services = new ServiceCollection().AddInfrastructure(config).AddCitedPilotInfrastructure(config).BuildServiceProvider();
        using var scope = services.CreateScope();
        var pilot = scope.ServiceProvider;
        Assert.NotNull(pilot.GetRequiredService<CitedGenerationCoordinator>());
        Assert.NotNull(pilot.GetRequiredService<CitedEmbeddingCoordinator>());
        Assert.NotNull(pilot.GetRequiredService<ICitedPilotUnknownHold>());
        Assert.NotNull(pilot.GetRequiredService<AssessCitedRecovery>());
        Assert.NotNull(pilot.GetRequiredService<ICitedRecoveryExecutor>());
        Assert.False(pilot.GetRequiredService<CitedGenerationSettings>().Enabled);
        Assert.False(pilot.GetRequiredService<CitedEmbeddingSettings>().Enabled);
        Assert.False(await pilot.GetRequiredService<ICitedRecoveryAuthorization>().CanAssessAsync(Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.False(await pilot.GetRequiredService<ICitedRecoveryExecutionAuthorization>().CanExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), default));
    }
}
