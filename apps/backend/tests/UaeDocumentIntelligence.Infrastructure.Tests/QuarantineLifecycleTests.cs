using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class QuarantineLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "p6-quarantine-" + Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("Enforced")]
    [InlineData("Locked")]
    public async Task Legacy_cleaner_never_runs_for_enterprise_storage(string mode)
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new StorageMaintenanceService(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new StorageOptions { SecurityMode = mode, UploadDirectory = root }),
            Options.Create(new StorageMaintenanceOptions { Enabled = true }), NullLogger<StorageMaintenanceService>.Instance);
        Assert.Equal(0, await service.CleanupAsync(default)); // No database registration: must reject before discovery.
    }
    [Fact]
    public async Task Active_upload_lease_prevents_cleanup_and_success_removes_staging_and_lease()
    {
        var org = Guid.NewGuid(); var doc = Guid.NewGuid(); var scanner = new BlockingScanner();
        var quarantine = Path.Combine(root, "quarantine");
        var storage = new LocalDocumentStorage(Options.Create(new StorageOptions { SecurityMode = "Enforced",
            UploadDirectory = Path.Combine(root, "uploads"), QuarantineDirectory = quarantine }), scanner);
        var task = storage.SaveAsync(new MemoryStream([1, 2, 3]), "input.pdf", 3, org, doc, default);
        await scanner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var candidate = Assert.Single(QuarantineFiles.Candidates(quarantine, org));
            Assert.Equal(doc, candidate.DocumentId);
            Assert.Null(QuarantineFiles.TryLease(quarantine, candidate, DateTimeOffset.UtcNow.AddDays(1)));
            Assert.Empty(QuarantineFiles.Candidates(quarantine, Guid.NewGuid()));
        }
        finally { scanner.Continue.TrySetResult(); }
        var saved = await task;
        Assert.True(File.Exists(saved.AbsolutePath)); Assert.Empty(Directory.GetFiles(quarantine));
    }
    [Theory]
    [InlineData("young-file")]
    [InlineData("young-lease")]
    [InlineData("missing-lease")]
    [InlineData("old")]
    public void Only_old_paired_uploads_can_be_claimed(string scenario)
    {
        Directory.CreateDirectory(root); var org = Guid.NewGuid();
        var key = QuarantineFiles.Key(org, Guid.NewGuid(), Guid.NewGuid());
        var file = Path.Combine(root, key); var leaseFile = Path.Combine(root, QuarantineFiles.LeaseKey(key));
        File.WriteAllBytes(file, [1]); File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-2));
        File.WriteAllBytes(Path.Combine(root, Guid.NewGuid().ToString("N") + ".upload"), [2]);
        if (scenario != "missing-lease") { File.WriteAllBytes(leaseFile, []); File.SetLastWriteTimeUtc(leaseFile, DateTime.UtcNow.AddDays(-2)); }
        if (scenario == "young-file") File.SetLastWriteTimeUtc(file, DateTime.UtcNow);
        if (scenario == "young-lease") File.SetLastWriteTimeUtc(leaseFile, DateTime.UtcNow);
        var candidate = Assert.Single(QuarantineFiles.Candidates(root, org));
        using var lease = QuarantineFiles.TryLease(root, candidate, DateTimeOffset.UtcNow.AddHours(-1));
        Assert.Equal(scenario == "old", lease is not null);
        Assert.True(File.Exists(file));
    }
    [Theory]
    [InlineData(0, 1)]
    [InlineData(36501, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 87601)]
    public void Policy_rejects_unbounded_or_immediate_expiry(int days, int hours) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrganizationRetentionPolicy(Guid.NewGuid(), days, hours, DateTimeOffset.UtcNow));
    private sealed class BlockingScanner : IMalwareScanner
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task ScanAsync(Stream content, long length, CancellationToken ct) { Started.SetResult(); await Continue.Task.WaitAsync(ct); }
    }
    public void Dispose()
    {
        var target = Path.GetFullPath(root);
        if (!target.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("p6-quarantine-"))
            throw new InvalidOperationException("Unexpected test path.");
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }
}
