using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class StorageSafetyTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("policy")]
    [InlineData("actor")]
    [InlineData("organization")]
    [InlineData("unbound")]
    public async Task Enforced_admission_rejects_missing_or_mismatched_receipt_and_identity_before_database_access(string change)
    {
        var organization = Guid.NewGuid(); var actor = Guid.NewGuid(); var hash = new string('a', 64);
        var document = new Document(Guid.NewGuid(), organization, actor, "file.pdf", "source.pdf", "application/pdf", 4, hash, DateTimeOffset.UtcNow);
        var audit = new SecurityAuditContext();
        if (change != "unbound") audit.Bind(change == "organization" ? Guid.NewGuid() : organization,
            change == "actor" ? Guid.NewGuid() : actor, "Human", "test-admission");
        var receipt = new FileScanReceipt(change == "hash" ? new string('b', 64) : hash,
            change == "size" ? 5 : 4, change == "policy" ? "untrusted" : "clamav-instream-v1");
        // Deliberately no database provider: rejection must precede database access.
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var store = new UploadQueueStore(db, Options.Create(new StorageOptions { SecurityMode = "Enforced" }), audit);
        await Assert.ThrowsAsync<MalwareScanUnavailableException>(() => change == "missing"
            ? store.AddAsync(document, Guid.NewGuid(), Guid.NewGuid(), default)
            : store.AddScannedAsync(document, Guid.NewGuid(), Guid.NewGuid(), receipt, default));
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("cancel")]
    [InlineData("oversized-reply")]
    [InlineData("disconnect")]
    public async Task Transport_failure_never_produces_a_clean_receipt(string scenario)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var caller = new CancellationTokenSource();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(bound.Token);
            await using var stream = peer.GetStream();
            await stream.ReadExactlyAsync(new byte[22], bound.Token);
            if (scenario == "cancel") caller.Cancel();
            if (scenario == "oversized-reply") await stream.WriteAsync(new byte[4097], bound.Token);
            if (scenario is "timeout" or "cancel") await done.Task.WaitAsync(bound.Token);
        });
        var scanner = new ClamAvScanner(Options.Create(new MalwareScanOptions
        { Mode = "ClamAv", Port = ((IPEndPoint)listener.LocalEndpoint).Port, TimeoutSeconds = 1 }));
        using var input = new MemoryStream("body"u8.ToArray());
        try
        {
            if (scenario == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync(input, 4, caller.Token));
            else await Assert.ThrowsAsync<MalwareScanUnavailableException>(() => scanner.ScanAsync(input, 4, default));
        }
        finally { done.TrySetResult(); await server.WaitAsync(bound.Token); }
    }

    [Fact]
    public async Task Destination_created_during_scan_is_preserved_and_stage_is_removed()
    {
        using var files = new Files(); var organization = Guid.NewGuid(); var document = Guid.NewGuid();
        var path = Path.Combine(files.Uploads, "organizations", organization.ToString("N"), "documents", document.ToString("N"), "source.pdf");
        var scanner = new Scanner(_ => File.WriteAllTextAsync(path, "original"));
        var storage = new LocalDocumentStorage(Options.Create(files.Options()), scanner);
        using var input = new MemoryStream("body"u8.ToArray());
        await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(input, "invoice.pdf", 4, organization, document, default));
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.GetFiles(files.Quarantine));
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("infected")]
    [InlineData("unavailable")]
    public async Task Staging_blocks_promotion_until_clean_and_failed_scans_leave_no_source(string outcome)
    {
        using var files = new Files();
        var scanner = new Scanner(async stream =>
        {
            Assert.Empty(Directory.GetFiles(files.Uploads, "*", SearchOption.AllDirectories));
            Assert.Single(Directory.GetFiles(files.Quarantine, "*.upload"));
            Assert.Single(Directory.GetFiles(files.Quarantine, "*.lease"));
            Assert.Equal("test-content", await new StreamReader(stream, leaveOpen: true).ReadToEndAsync());
            if (outcome == "infected") throw new MalwareDetectedException();
            if (outcome == "unavailable") throw new MalwareScanUnavailableException();
        });
        var storage = new LocalDocumentStorage(Options.Create(files.Options()), scanner);
        using var input = new MemoryStream("test-content"u8.ToArray());
        if (outcome == "clean")
        {
            var saved = await storage.SaveAsync(input, "invoice.pdf", input.Length, Guid.NewGuid(), Guid.NewGuid(), default);
            Assert.Equal("test-content", await File.ReadAllTextAsync(saved.AbsolutePath));
            Assert.Equal(saved.Sha256, saved.Scan!.Sha256);
            Assert.Equal("clamav-instream-v1", saved.Scan.Policy);
        }
        else
        {
            if (outcome == "infected")
                await Assert.ThrowsAsync<MalwareDetectedException>(() => storage.SaveAsync(input, "invoice.pdf", input.Length, Guid.NewGuid(), Guid.NewGuid(), default));
            else
                await Assert.ThrowsAsync<MalwareScanUnavailableException>(() => storage.SaveAsync(input, "invoice.pdf", input.Length, Guid.NewGuid(), Guid.NewGuid(), default));
            Assert.Empty(Directory.GetFiles(files.Uploads, "*", SearchOption.AllDirectories));
        }
        Assert.Equal(1, scanner.Calls);
        Assert.Empty(Directory.GetFiles(files.Quarantine));
    }

    [Fact]
    public async Task Existing_source_is_never_overwritten_or_deleted_by_a_failed_second_save()
    {
        using var files = new Files();
        var storage = new LocalDocumentStorage(Options.Create(files.Options()), new Scanner(_ => Task.CompletedTask));
        var organization = Guid.NewGuid(); var document = Guid.NewGuid();
        using var first = new MemoryStream("original"u8.ToArray());
        var saved = await storage.SaveAsync(first, "invoice.pdf", first.Length, organization, document, default);
        using var second = new MemoryStream("replacement"u8.ToArray());
        await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(second, "invoice.pdf", second.Length, organization, document, default));
        Assert.Equal("original", await File.ReadAllTextAsync(saved.AbsolutePath));
    }

    [Fact]
    public async Task Oversized_stream_stops_after_one_byte_beyond_declared_size_without_scanning()
    {
        using var files = new Files();
        var scanner = new Scanner(_ => throw new Exception("Must not scan"));
        var storage = new LocalDocumentStorage(Options.Create(files.Options()), scanner);
        using var input = new MemoryStream(new byte[1000000]);
        await Assert.ThrowsAsync<DocumentTooLargeException>(() => storage.SaveAsync(input, "invoice.pdf", 4, Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.Equal(5, input.Position); Assert.Equal(0, scanner.Calls);
        Assert.Empty(Directory.GetFiles(files.Quarantine));
        Assert.Empty(Directory.GetFiles(files.Uploads, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Scanner_cannot_change_bytes_between_the_hash_and_promotion()
    {
        using var files = new Files();
        var scanner = new Scanner(async stream => { stream.Position = 0; await stream.WriteAsync("evil"u8.ToArray()); });
        var storage = new LocalDocumentStorage(Options.Create(files.Options()), scanner);
        using var input = new MemoryStream("good"u8.ToArray());
        await Assert.ThrowsAsync<MalwareScanUnavailableException>(() => storage.SaveAsync(input, "invoice.pdf", 4, Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.Empty(Directory.GetFiles(files.Quarantine));
    }

    [Theory]
    [InlineData("Locked")]
    [InlineData("typo")]
    public async Task Locked_storage_rejects_before_reading(string mode)
    {
        using var files = new Files(); var config = files.Options(); config.SecurityMode = mode;
        var storage = new LocalDocumentStorage(Options.Create(config));
        using var input = new MemoryStream("body"u8.ToArray());
        await Assert.ThrowsAsync<MalwareScanUnavailableException>(() => storage.SaveAsync(input, "invoice.pdf", 4, Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.Equal(0, input.Position);
    }

    [Theory]
    [InlineData("stream: OK\0", "clean")]
    [InlineData("stream: Test-Signature FOUND\0", "infected")]
    [InlineData("stream: skipped ERROR\0", "unavailable")]
    [InlineData("stream: OK\0stream: bad FOUND\0", "unavailable")]
    [InlineData("stream: OK", "unavailable")]
    [InlineData("OK\0", "unavailable")]
    [InlineData("stream: OK\n", "unavailable")]
    public async Task Clamd_transport_streams_exact_bytes_and_requires_one_complete_clean_result(string response, string expected)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var payload = Encoding.UTF8.GetBytes("test only, no real malware: " + new string('x', 70000));
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(); await using var network = peer.GetStream();
            var command = new byte[10]; await network.ReadExactlyAsync(command);
            Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(command));
            using var collected = new MemoryStream(); var header = new byte[4];
            while (true)
            {
                await network.ReadExactlyAsync(header); var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                if (length == 0) break;
                Assert.InRange(length, 1u, 65536u);
                var chunk = new byte[length]; await network.ReadExactlyAsync(chunk); collected.Write(chunk);
            }
            Assert.Equal(payload, collected.ToArray());
            foreach (var value in Encoding.ASCII.GetBytes(response)) await network.WriteAsync(new[] { value });
        });
        var scanner = new ClamAvScanner(Options.Create(new MalwareScanOptions { Mode = "ClamAv", Port = ((IPEndPoint)listener.LocalEndpoint).Port }));
        using var input = new MemoryStream(payload);
        if (expected == "clean") await scanner.ScanAsync(input, payload.Length, default);
        else if (expected == "infected") await Assert.ThrowsAsync<MalwareDetectedException>(() => scanner.ScanAsync(input, payload.Length, default));
        else await Assert.ThrowsAsync<MalwareScanUnavailableException>(() => scanner.ScanAsync(input, payload.Length, default));
        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("192.0.2.1", "ClamAv")]
    [InlineData("localhost", "ClamAv")]
    [InlineData("127.0.0.1", "Locked")]
    public async Task Remote_or_unconfigured_scanners_do_not_read_content(string address, string mode)
    {
        var scanner = new ClamAvScanner(Options.Create(new MalwareScanOptions { Mode = mode, Address = address }));
        using var input = new MemoryStream("body"u8.ToArray());
        await Assert.ThrowsAsync<MalwareScanUnavailableException>(() => scanner.ScanAsync(input, 4, default));
        Assert.Equal(0, input.Position);
    }

    private sealed class Scanner(Func<Stream, Task> inspect) : IMalwareScanner
    {
        public int Calls { get; private set; }
        public Task ScanAsync(Stream content, long length, CancellationToken ct) { Calls++; return inspect(content); }
    }
    private sealed class Files : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "p6-storage-" + Guid.NewGuid().ToString("N"));
        public string Uploads => Path.Combine(_root, "uploads");
        public string Quarantine => Path.Combine(_root, "quarantine");
        public StorageOptions Options() => new() { SecurityMode = "Enforced", UploadDirectory = Uploads, QuarantineDirectory = Quarantine };
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}
