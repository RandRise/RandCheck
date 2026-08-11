using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Infrastructure;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class LocalDocumentStorageTests
{
    [Fact]
    public async Task Cancelled_stream_copy_deletes_exact_partial_file_and_preserves_existing_files()
    {
        using var cancellation = new CancellationTokenSource();
        await VerifyPartialFileCleanupAsync(
            () => new InterruptingStream(cancellation, failure: null),
            stream => Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                stream.Storage.SaveAsync(stream.Content, "invoice.pdf", 8, cancellation.Token)));
    }

    [Fact]
    public async Task Throwing_stream_copy_deletes_exact_partial_file_and_preserves_existing_files()
    {
        await VerifyPartialFileCleanupAsync(
            () => new InterruptingStream(cancellation: null, new IOException("Synthetic read failure.")),
            stream => Assert.ThrowsAsync<IOException>(() =>
                stream.Storage.SaveAsync(stream.Content, "invoice.pdf", 8, CancellationToken.None)));
    }

    private static async Task VerifyPartialFileCleanupAsync(Func<Stream> streamFactory,
        Func<StorageTestScope, Task> assertion)
    {
        var directory = CreateTestDirectory();
        var sentinel = Path.Combine(directory, "pre-existing.keep");
        await File.WriteAllTextAsync(sentinel, "preserve");
        try
        {
            using var provider = CreateServiceProvider(directory);
            using var scope = provider.CreateScope();
            await using var content = streamFactory();
            var testScope = new StorageTestScope(
                scope.ServiceProvider.GetRequiredService<IDocumentStorage>(), content);

            await assertion(testScope);

            Assert.Equal(new[] { sentinel }, Directory.GetFiles(directory));
            Assert.Equal("preserve", await File.ReadAllTextAsync(sentinel));
        }
        finally
        {
            DeleteTestDirectory(directory);
        }
    }

    private static ServiceProvider CreateServiceProvider(string directory)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Data Source=:memory:",
            ["Storage:UploadDirectory"] = directory,
            ["Storage:MaxUploadBytes"] = "1024",
            ["AiService:BaseUrl"] = "http://localhost:8001"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "UaeDocumentIntelligence.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.GetFullPath(directory);
    }

    private static void DeleteTestDirectory(string directory)
    {
        var permittedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "UaeDocumentIntelligence.Tests"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(directory);
        if (!target.StartsWith(permittedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to remove an unexpected test directory.");
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
    }

    private sealed record StorageTestScope(IDocumentStorage Storage, Stream Content);

    private sealed class InterruptingStream(CancellationTokenSource? cancellation, Exception? failure) : Stream
    {
        private bool _returnedFirstChunk;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (!_returnedFirstChunk)
            {
                _returnedFirstChunk = true;
                new byte[] { 1, 2, 3, 4 }.CopyTo(buffer);
                return ValueTask.FromResult(4);
            }

            if (cancellation is not null)
            {
                cancellation.Cancel();
                return ValueTask.FromCanceled<int>(cancellationToken);
            }

            return ValueTask.FromException<int>(failure!);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
