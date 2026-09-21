using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Infrastructure.Services;

public sealed class ExtractionWorkerOptions
{
    public bool Enabled { get; set; }
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public string SecurityMode { get; set; } = "Development";
    public string DatabaseRole { get; set; } = string.Empty;

    public static string ResolveSecurityMode(string? configured, string environment)
    {
        var local = environment is "Development" or "Testing";
        var mode = string.IsNullOrEmpty(configured) ? (local ? "Development" : "Locked") : configured;
        return mode == "Enforced" || mode == "Development" && local ? mode : "Locked";
    }
}

internal sealed class ExtractionWorker(
    IServiceScopeFactory scopes,
    IOptions<ExtractionWorkerOptions> options,
    ILogger<ExtractionWorker> logger) : BackgroundService
{
    internal static readonly ActivitySource Activities = new("UaeDocumentIntelligence.ExtractionWorker");
    internal static readonly Meter Meter = new("UaeDocumentIntelligence.ExtractionWorker");
    private static readonly Counter<long> Claims = Meter.CreateCounter<long>("extraction.jobs.claimed");
    private static readonly Counter<long> Completed = Meter.CreateCounter<long>("extraction.jobs.completed");
    private static readonly Counter<long> Retried = Meter.CreateCounter<long>("extraction.jobs.retried");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("extraction.jobs.failed");
    private static readonly Counter<long> LeaseLost = Meter.CreateCounter<long>("extraction.jobs.lease_lost");
    private readonly string _owner = BuildOwner();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        await EnsurePostgresAsync(stoppingToken);
        logger.LogInformation("Extraction worker started with durable PostgreSQL queue");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TerminalizeExpiredAsync(stoppingToken);
                var claim = await ClaimAsync(stoppingToken);
                if (claim is null)
                {
                    await Task.Delay(Math.Max(100, options.Value.PollIntervalMilliseconds), stoppingToken);
                    continue;
                }
                await ProcessAsync(claim, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                logger.LogError("Extraction worker loop failed; polling will continue. Database/provider diagnostics withheld.");
                await Task.Delay(Math.Max(100, options.Value.PollIntervalMilliseconds), stoppingToken);
            }
        }
    }

    private async Task ProcessAsync(ClaimedExtractionJob claim, CancellationToken stoppingToken)
    {
        using var activity = Activities.StartActivity("extraction.attempt", ActivityKind.Internal);
        activity?.SetTag("extraction.attempt", claim.Attempt);
        activity?.SetTag("extraction.provider", claim.ProviderAlias);
        activity?.SetTag("extraction.queue_delay_ms",
            Math.Max(0, (long)(DateTimeOffset.UtcNow - claim.CreatedAtUtc).TotalMilliseconds));
        Claims.Add(1, new KeyValuePair<string, object?>("provider", claim.ProviderAlias));

        using var lostLease = new CancellationTokenSource();
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, lostLease.Token);
        attempt.CancelAfter(ExtractionJobQueue.AttemptDuration);
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = HeartbeatLoopAsync(claim, lostLease, heartbeatStop.Token);
        try
        {
            ExtractionResult result;
            await using (var providerScope = scopes.CreateAsyncScope())
            {
                if (options.Value.SecurityMode == "Enforced")
                    await providerScope.ServiceProvider.GetRequiredService<ExtractionFileSafety>().VerifyAsync(claim, attempt.Token);
                var client = providerScope.ServiceProvider.GetRequiredService<IExtractionClient>();
                var request = new WorkerExtractionRequest(claim.SchemaVersion, claim.JobId,
                    claim.DocumentId, claim.ExtractionRunId, claim.StorageKey, claim.Sha256, claim.Bytes,
                    claim.DeclaredContentType, claim.ProviderAlias, claim.Attempt, claim.DeadlineUtc,
                    claim.CorrelationId);
                result = await client.ExtractAsync(request, attempt.Token);
            }
            heartbeatStop.Cancel();
            await IgnoreHeartbeatCancellationAsync(heartbeat);
            if (lostLease.IsCancellationRequested) return;
            if (await CompleteAsync(claim, result, stoppingToken))
            {
                Completed.Add(1, new KeyValuePair<string, object?>("outcome", result.Outcome.ToString()));
                logger.LogInformation("Extraction job completed with {Outcome} on attempt {Attempt}",
                    result.Outcome, claim.Attempt);
            }
            else RecordLeaseLoss(claim.Attempt);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown is not a business cancellation. The lease expires and another worker reclaims it.
        }
        catch (OperationCanceledException) when (lostLease.IsCancellationRequested)
        {
            RecordLeaseLoss(claim.Attempt);
        }
        catch (OperationCanceledException)
        {
            await RecordFailureAsync(claim, "provider_timeout",
                "The extraction provider did not finish within this attempt.", true, stoppingToken);
        }
        catch (ExtractionProviderException exception)
        {
            await RecordFailureAsync(claim, exception.Code, exception.SafeSummary,
                exception.Retryable, stoppingToken);
        }
        catch (Exception)
        {
            logger.LogWarning("Extraction attempt failed with an internal safe classification");
            await RecordFailureAsync(claim, "internal_extraction_error",
                "The extraction provider could not process this document.", false, stoppingToken);
        }
        finally
        {
            heartbeatStop.Cancel();
            await IgnoreHeartbeatCancellationAsync(heartbeat);
        }
    }

    private async Task HeartbeatLoopAsync(ClaimedExtractionJob claim,
        CancellationTokenSource lostLease, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await using var session = await WorkerQueueSession.OpenAsync(scope.ServiceProvider, cancellationToken);
                var queue = session.Queue;
                if (!await queue.HeartbeatAsync(claim.JobId, claim.LeaseOwner, claim.LeaseToken,
                        cancellationToken))
                {
                    lostLease.Cancel();
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                logger.LogWarning("Extraction lease heartbeat failed; authority was not extended");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task RecordFailureAsync(ClaimedExtractionJob claim, string code,
        string summary, bool retryable, CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await using var session = await WorkerQueueSession.OpenAsync(scope.ServiceProvider, stoppingToken);
        var queue = session.Queue;
        var recorded = await queue.RecordFailureAsync(claim, code, summary, retryable, stoppingToken);
        if (!recorded)
        {
            RecordLeaseLoss(claim.Attempt);
            return;
        }
        if (retryable && claim.Attempt < 3)
        {
            Retried.Add(1, new KeyValuePair<string, object?>("code", code));
            logger.LogInformation("Extraction job scheduled for retry after attempt {Attempt} with {Code}",
                claim.Attempt, code);
        }
        else
        {
            Failed.Add(1, new KeyValuePair<string, object?>("code", code));
            logger.LogInformation("Extraction job terminalized after attempt {Attempt} with {Code}",
                claim.Attempt, code);
        }
    }

    private async Task<ClaimedExtractionJob?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await using var session = await WorkerQueueSession.OpenAsync(scope.ServiceProvider, cancellationToken);
        return await session.Queue
            .ClaimAsync(_owner, cancellationToken);
    }

    private async Task<bool> CompleteAsync(ClaimedExtractionJob claim, ExtractionResult result,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await using var session = await WorkerQueueSession.OpenAsync(scope.ServiceProvider, cancellationToken);
        return await session.Queue
            .CompleteAsync(claim, result, cancellationToken);
    }

    private async Task TerminalizeExpiredAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        await using var session = await WorkerQueueSession.OpenAsync(scope.ServiceProvider, cancellationToken);
        var queue = session.Queue;
        while (await queue.TerminalizeOneExpiredAsync(cancellationToken))
            Failed.Add(1, new KeyValuePair<string, object?>("code", "worker_lease_expired"));
    }

    private async Task EnsurePostgresAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (options.Value.SecurityMode == "Enforced")
        {
            var ai = scope.ServiceProvider.GetRequiredService<IOptions<AiServiceOptions>>().Value;
            if (ai.AuthenticationMode != "ServiceKeys" || !Uri.TryCreate(ai.BaseUrl, UriKind.Absolute, out var origin))
                throw new InvalidOperationException("Enforced workers require the private extraction service credential and HTTPS transport before claiming work.");
            using var http = new HttpClient { BaseAddress = origin };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/extractions");
            if (!PrivateServiceCredentials.TryApply(request, http, ai))
                throw new InvalidOperationException("Enforced workers require the private extraction service credential and HTTPS transport before claiming work.");
            if (scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.SecurityMode != "Enforced" ||
                scope.ServiceProvider.GetRequiredService<IOptions<MalwareScanOptions>>().Value.Mode != "ClamAv")
                throw new InvalidOperationException("Enforced workers require protected storage and malware screening.");
        }
        await using var session = await WorkerQueueSession.OpenAsync(scope.ServiceProvider, cancellationToken);
        var db = session.Database;
        if (!db.Database.IsNpgsql())
            throw new InvalidOperationException("ExtractionWorker:Enabled requires PostgreSQL.");
        await db.Database.CanConnectAsync(cancellationToken);
    }

    private void RecordLeaseLoss(int attempt)
    {
        LeaseLost.Add(1);
        logger.LogWarning("Extraction worker lost lease authority on attempt {Attempt}; result was discarded",
            attempt);
    }

    private static async Task IgnoreHeartbeatCancellationAsync(Task heartbeat)
    {
        try { await heartbeat; }
        catch (OperationCanceledException) { }
    }

    private static string BuildOwner()
    {
        var machine = new string(Environment.MachineName.Where(char.IsLetterOrDigit).Take(40).ToArray());
        if (string.IsNullOrEmpty(machine)) machine = "worker";
        return $"{machine}-{Guid.NewGuid():N}";
    }
}
