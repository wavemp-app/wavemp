using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Service.Logs;
using WaveMP.Testing.Logs;

namespace WaveMP.Service.Tests.Logs;

[TestFixture]
public class RequestUrlLogFilterTests
{
    private const string Url = "Request starting HTTP/1.1 GET http://localhost/api/v1/hubs/devices?access_token=secret";

    [Test]
    public void RequestLog_RaisedByTheConfiguration_StaysAtWarning()
    {
        LogMessageCollector collector = new();
        using ILoggerFactory factory = CreateFactory(collector, withFilter: true);

        Log(factory);

        collector.Entries.Should().NotContain(entry => entry.Contains("secret", StringComparison.Ordinal));
        collector.Entries.Should().Contain(entry => entry.Contains("request warning", StringComparison.Ordinal));
        collector.Entries.Should().Contain(entry => entry.Contains("routing debug", StringComparison.Ordinal));
        collector.Entries.Should().Contain(entry => entry.Contains("hosting debug", StringComparison.Ordinal));
    }

    [Test]
    public void RequestLog_WithoutTheFilter_WouldLogTheQueryString()
    {
        LogMessageCollector collector = new();
        using ILoggerFactory factory = CreateFactory(collector, withFilter: false);

        Log(factory);

        collector.Entries.Should().Contain(entry => entry.Contains("secret", StringComparison.Ordinal));
    }

    [Test]
    public void RequestLog_DisabledByTheConfiguration_StaysDisabled()
    {
        LogMessageCollector collector = new();
        using ILoggerFactory factory = LoggerFactory.Create(builder =>
        {
            builder.AddFilter(RequestUrlLogFilter.Category, LogLevel.None);
            builder.AddProvider(collector);
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IPostConfigureOptions<LoggerFilterOptions>, RequestUrlLogFilter>());
        });

        factory.CreateLogger(RequestUrlLogFilter.Category).LogError("request error");

        collector.Entries.Should().BeEmpty();
    }

    private static ILoggerFactory CreateFactory(LogMessageCollector collector, bool withFilter)
    {
        return LoggerFactory.Create(builder =>
        {
            // Everything raised to Trace, for all providers and for the collecting provider itself, including the
            // category of the request log and a part of it.
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddFilter("Microsoft.AspNetCore", LogLevel.Trace);
            builder.AddFilter(RequestUrlLogFilter.Category, LogLevel.Debug);
            builder.AddFilter<LogMessageCollector>(null, LogLevel.Trace);
            builder.AddFilter<LogMessageCollector>("Microsoft.AspNetCore.Hosting", LogLevel.Trace);
            builder.AddFilter<LogMessageCollector>(RequestUrlLogFilter.Category, LogLevel.Trace);
            builder.AddProvider(collector);
            if (withFilter)
            {
                builder.Services.TryAddEnumerable(
                    ServiceDescriptor.Singleton<IPostConfigureOptions<LoggerFilterOptions>, RequestUrlLogFilter>());
            }
        });
    }

    private static void Log(ILoggerFactory factory)
    {
        ILogger request = factory.CreateLogger(RequestUrlLogFilter.Category);
        request.LogInformation(Url);
        request.LogDebug(Url);
        request.LogWarning("request warning");
        factory.CreateLogger("Microsoft.AspNetCore.Routing").LogDebug("routing debug");
        factory.CreateLogger("Microsoft.AspNetCore.Hosting.Lifetime").LogDebug("hosting debug");
    }
}
