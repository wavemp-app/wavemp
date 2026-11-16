using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace WaveMP.Service.Tests.IntegrationTests.Fakes;

/// <summary>
/// Collects the log entries of the service from level <see cref="LogLevel.Error"/> on, so tests can check that an
/// expected situation is not logged as an error.
/// </summary>
public sealed class ErrorLogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _errors = new();

    /// <summary>
    /// Category and message of every error since the start or the last <see cref="Clear"/>.
    /// </summary>
    public IReadOnlyCollection<string> Errors => [.. _errors];

    public void Clear()
    {
        _errors.Clear();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(categoryName, _errors);
    }

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<string> errors) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Error;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                errors.Enqueue($"{category}: {formatter(state, exception)}");
            }
        }
    }
}
