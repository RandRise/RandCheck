using Microsoft.Extensions.Logging;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

/// <summary>
/// EF parameter redaction does not redact server exception messages. Preserve
/// error events, but never forward their formatter, structured state, or exception.
/// This is limited to EF logging configured through AddInfrastructure.
/// </summary>
internal sealed class DatabasePrivacyLoggerFactory(ILoggerFactory applicationLogs) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => new PrivacyLogger(applicationLogs.CreateLogger(categoryName));
    public void AddProvider(ILoggerProvider provider) => applicationLogs.AddProvider(provider);
    public void Dispose() { } // The DI container owns the application logger factory.

    private sealed class PrivacyLogger(ILogger logger) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (exception is not null || level >= LogLevel.Error)
            {
                // Keep the existing category/event ID for correlation and alerting.
                // Do not inspect server Message, Detail, SQL, identifiers, or Data.
                logger.Log(level, id, "Database operation diagnostic withheld for privacy; event {DatabaseEventId}", id.Id);
                return;
            }
            logger.Log(level, id, state, null, formatter);
        }
    }
}
