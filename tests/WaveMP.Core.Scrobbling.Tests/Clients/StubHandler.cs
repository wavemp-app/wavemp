using System.Net;
using System.Text;

namespace WaveMP.Core.Scrobbling.Tests.Clients;

/// <summary>
/// A simulated service: answers every request with the configured response and keeps the requests with their bodies.
/// No request leaves the process.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private Func<HttpResponseMessage> _respond = () => new HttpResponseMessage(HttpStatusCode.NotFound);

    public List<RecordedRequest> Requests { get; } = [];

    public void Respond(HttpStatusCode status, string body)
    {
        _respond = () => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    public void Fail(Exception exception)
    {
        _respond = () => throw exception;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!.AbsoluteUri,
            request.Headers.Authorization?.ToString(),
            body));
        return _respond();
    }

    public sealed record RecordedRequest(HttpMethod Method, string Url, string? Authorization, string? Body);
}
