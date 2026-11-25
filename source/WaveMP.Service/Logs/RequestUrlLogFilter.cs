using Microsoft.Extensions.Options;

namespace WaveMP.Service.Logs;

/// <summary>
/// Keeps the request log of ASP.NET Core (<see cref="Category"/>) at <see cref="MinimumLevel"/> or above, whatever the
/// configuration says, also for single log providers. That category logs every request with its full URL including
/// the query string at the level Information, and query strings carry credentials: the tickets of the real-time
/// connection of the devices (<c>access_token</c>) and the signatures of media URLs. Runs after the configuration, so
/// an operator who raises <c>Microsoft.AspNetCore</c> to Information or Debug still gets no credentials in the log.
/// </summary>
public sealed class RequestUrlLogFilter : IPostConfigureOptions<LoggerFilterOptions>
{
    /// <summary>
    /// Category of the request log of the ASP.NET Core host ("Request starting ...", "Request finished ...").
    /// </summary>
    public const string Category = "Microsoft.AspNetCore.Hosting.Diagnostics";

    /// <summary>
    /// The lowest level the category may log at; its warnings and errors carry no URLs.
    /// </summary>
    public const LogLevel MinimumLevel = LogLevel.Warning;

    public void PostConfigure(string? name, LoggerFilterOptions options)
    {
        LoggerFilterRule[] rules = [.. options.Rules];
        options.Rules.Clear();
        foreach (LoggerFilterRule rule in rules)
        {
            // Rules for the category itself (or a part of it) may not lower its level.
            options.Rules.Add(rule.CategoryName is { } category && category.StartsWith(Category, StringComparison.Ordinal)
                ? new LoggerFilterRule(rule.ProviderName, category, Raise(rule.LogLevel), rule.Filter)
                : rule);
        }

        // One rule for every provider that has rules of its own (they take precedence over the rules for all
        // providers), and one for all providers, unless a raised rule for the category itself is there already.
        foreach (string? provider in rules.Select(rule => rule.ProviderName).Append(null).Distinct())
        {
            if (rules.Any(rule => rule.ProviderName == provider && rule.CategoryName == Category))
            {
                continue;
            }

            options.Rules.Add(new LoggerFilterRule(provider, Category, MinimumLevel, filter: null));
        }
    }

    private static LogLevel Raise(LogLevel? level)
    {
        return level is { } value && value > MinimumLevel ? value : MinimumLevel;
    }
}
