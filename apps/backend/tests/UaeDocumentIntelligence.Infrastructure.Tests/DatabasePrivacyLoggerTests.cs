using Microsoft.Extensions.Logging;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class DatabasePrivacyLoggerTests
{
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void Exception_events_discard_original_state_formatter_and_exception(LogLevel level)
    {
        using var target = new CaptureFactory();
        using var privacy = new DatabasePrivacyLoggerFactory(target);
        var id = new EventId(10100, "QueryIterationFailed");
        privacy.CreateLogger("database-test").Log(level, id, new UnreadableState(),
            new InvalidOperationException("private-original-error"), (_, _) => throw new Exception("Original formatter must not run"));
        Assert.Equal("database-test", target.Category);
        Assert.Equal(level, target.Level);
        Assert.Equal(id, target.Event);
        Assert.Null(target.Error);
        Assert.Contains("10100", target.Message);
        Assert.DoesNotContain("private", target.Message);
        var fields = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(target.State);
        Assert.DoesNotContain(fields, x => x.Value is Exception or UnreadableState);
    }

    [Theory]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void Error_events_without_exception_also_discard_structured_diagnostics(LogLevel level)
    {
        using var target = new CaptureFactory();
        using var privacy = new DatabasePrivacyLoggerFactory(target);
        privacy.CreateLogger("database-test").Log(level, new EventId(20102), new UnreadableState(), null,
            (_, _) => throw new Exception("Original formatter must not run"));
        Assert.Equal(level, target.Level);
        Assert.Contains("20102", target.Message);
        Assert.Null(target.Error);
    }

    [Fact]
    public void Nonexception_information_keeps_normal_redacted_command_telemetry()
    {
        using var target = new CaptureFactory();
        using var privacy = new DatabasePrivacyLoggerFactory(target);
        var state = new { Command = "SELECT @p", Parameter = "?" };
        privacy.CreateLogger("database-test").Log(LogLevel.Information, new EventId(20101), state, null,
            (value, _) => value.Command);
        Assert.Same(state, target.State);
        Assert.Equal("SELECT @p", target.Message);
    }

    [Fact]
    public void Wrapper_does_not_dispose_container_owned_logger_factory()
    {
        using var target = new CaptureFactory();
        new DatabasePrivacyLoggerFactory(target).Dispose();
        Assert.False(target.Disposed);
    }

    private sealed class UnreadableState
    {
        public override string ToString() => throw new Exception("Original state must not be formatted");
    }

    private sealed class CaptureFactory : ILoggerFactory, ILogger
    {
        public string? Category, Message;
        public object? State;
        public EventId Event;
        public LogLevel Level;
        public Exception? Error;
        public bool Disposed;
        public ILogger CreateLogger(string categoryName) { Category = categoryName; return this; }
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() => Disposed = true;
        public bool IsEnabled(LogLevel level) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { Level = level; Event = id; State = state; Error = error; Message = formatter(state, error); }
    }
}
