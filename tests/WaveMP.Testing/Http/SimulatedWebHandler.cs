using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace WaveMP.Testing.Http;

/// <summary>
/// Simulated web services for an HTTP client: answers requests by the routes the test registers (the route registered
/// last wins), answers 404 for everything else and records every request with its headers. No request leaves the
/// process.
/// </summary>
public sealed class SimulatedWebHandler : HttpMessageHandler
{
    private readonly Lock _lock = new();
    private readonly List<(Func<Uri, bool> Matches, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes
        = [];
    private readonly ConcurrentQueue<SimulatedRequest> _requests = new();

    /// <summary>
    /// The requests in the order they arrived.
    /// </summary>
    public IReadOnlyList<SimulatedRequest> Requests => [.. _requests];

    /// <summary>
    /// Answers requests whose address matches.
    /// </summary>
    public SimulatedWebHandler On(Func<Uri, bool> matches, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        lock (_lock)
        {
            _routes.Add((matches, respond));
        }

        return this;
    }

    /// <summary>
    /// Answers requests whose address starts with <paramref name="prefix"/> with the content.
    /// </summary>
    public SimulatedWebHandler On(
        string prefix,
        byte[] content,
        string contentType,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        return On(
            url => url.AbsoluteUri.StartsWith(prefix, StringComparison.Ordinal),
            _ =>
            {
                ByteArrayContent body = new(content);
                body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                return new HttpResponseMessage(status) { Content = body };
            });
    }

    /// <summary>
    /// Answers requests whose address starts with <paramref name="prefix"/> with JSON.
    /// </summary>
    public SimulatedWebHandler OnJson(string prefix, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return On(prefix, Encoding.UTF8.GetBytes(json), "application/json", status);
    }

    /// <summary>
    /// Forgets the routes and the recorded requests.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _routes.Clear();
            _requests.Clear();
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Uri url = request.RequestUri!;
        _requests.Enqueue(new SimulatedRequest(
            url,
            request.Headers.ToDictionary(
                header => header.Key,
                header => header.Key == "User-Agent"
                    ? request.Headers.UserAgent.ToString()
                    : string.Join(",", header.Value))));
        Func<HttpRequestMessage, HttpResponseMessage>? respond;
        lock (_lock)
        {
            respond = _routes.LastOrDefault(route => route.Matches(url)).Respond;
        }

        return Task.FromResult(respond?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

/// <summary>
/// A request the simulated services received.
/// </summary>
/// <param name="Url">Address of the request.</param>
/// <param name="Headers">Its headers, values joined by commas (the user agent as sent).</param>
public sealed record SimulatedRequest(Uri Url, IReadOnlyDictionary<string, string> Headers);
