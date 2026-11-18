using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// What the HTTP clients of the scrobbling services share: the User-Agent, the version of WaveMP and the HTTP client
/// itself.
/// </summary>
internal static class ScrobbleHttp
{
    /// <summary>
    /// Name of WaveMP as client of the services.
    /// </summary>
    public const string ClientName = "WaveMP";

    /// <summary>
    /// Version of WaveMP without the source revision, e.g. <c>1.0.42</c>.
    /// </summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>
    /// The User-Agent of the requests, e.g. <c>WaveMP/1.0.42 (https://github.com/wavemp-app/wavemp)</c>.
    /// </summary>
    public static ProductInfoHeaderValue[] UserAgent { get; } =
    [
        new(ClientName, Version),
        new("(https://github.com/wavemp-app/wavemp)"),
    ];

    /// <summary>
    /// An HTTP client of a service for the lifetime of the service; connections are renewed regularly, so changes of
    /// DNS apply.
    /// </summary>
    public static HttpClient Create(Uri baseAddress, TimeSpan timeout)
    {
        SocketsHttpHandler handler = new()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.All,
        };
        HttpClient client = new(handler)
        {
            BaseAddress = baseAddress,
            Timeout = timeout,
        };
        foreach (ProductInfoHeaderValue product in UserAgent)
        {
            client.DefaultRequestHeaders.UserAgent.Add(product);
        }

        return client;
    }

    private static string ReadVersion()
    {
        string version = typeof(ScrobbleHttp).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";

        // The source revision after "+" is no part of the product version.
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? version[..plus] : version;
    }
}
