using System.Net.Http.Headers;
using System.Reflection;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The User-Agent of all requests to MusicBrainz and the Cover Art Archive, as the terms of use of MusicBrainz ask for:
/// <c>WaveMP/&lt;version&gt; ( https://github.com/wavemp-app/wavemp )</c>.
/// </summary>
public static class OnlineUserAgent
{
    public const string Contact = "( https://github.com/wavemp-app/wavemp )";

    /// <summary>
    /// The products of the header.
    /// </summary>
    public static ProductInfoHeaderValue[] Products { get; } =
    [
        new("WaveMP", Version()),
        new(Contact),
    ];

    /// <summary>
    /// The header as text.
    /// </summary>
    public static string Value { get; } = $"WaveMP/{Version()} {Contact}";

    private static string Version()
    {
        string version = typeof(OnlineUserAgent).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";

        // The source revision after "+" is no part of the product version.
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? version[..plus] : version;
    }
}
