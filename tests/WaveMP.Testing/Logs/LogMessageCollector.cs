using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace WaveMP.Testing.Logs;

/// <summary>
/// Collects every log entry of every level with its exception, so tests can check that a secret never reaches the log.
/// Register it with a filter of its own (<c>AddFilter&lt;LogMessageCollector&gt;(null, LogLevel.Trace)</c>) to see the
/// entries below the configured level as well.
/// </summary>
public sealed class LogMessageCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    /// <summary>
    /// Level, category, message and exception of every entry.
    /// </summary>
    public IReadOnlyCollection<string> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(categoryName, _entries);
    }

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue($"{logLevel} {category}: {formatter(state, exception)} {exception}");
        }
    }
}
