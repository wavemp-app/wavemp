using System.Net;

namespace WaveMP.Core.OnlineMetadata.Tests.Fakes;

/// <summary>
/// A simulated service: answers the requests with the queued responses in their order (the last one again once the
/// queue is empty) and records each request with its User-Agent and the simulated time. No request leaves the
/// process.
/// </summary>
internal sealed class StubHandler(TimeProvider clock) : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();
    private Func<HttpResponseMessage> _last = () => new HttpResponseMessage(HttpStatusCode.NotFound);

    public List<RecordedRequest> Requests { get; } = [];

    public StubHandler Respond(HttpStatusCode status, byte[] body, Action<HttpResponseMessage>? configure = null)
    {
        _responses.Enqueue(() =>
        {
            HttpResponseMessage response = new(status) { Content = new ByteArrayContent(body) };
            configure?.Invoke(response);
            return response;
        });
        return this;
    }

    public StubHandler Respond(HttpStatusCode status, string body = "", Action<HttpResponseMessage>? configure = null)
    {
        return Respond(status, System.Text.Encoding.UTF8.GetBytes(body), configure);
    }

    public StubHandler Fail(Exception exception)
    {
        _responses.Enqueue(() => throw exception);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri!.AbsoluteUri,
                request.Headers.UserAgent.ToString(),
                clock.GetUtcNow()));
            if (_responses.TryDequeue(out Func<HttpResponseMessage>? next))
            {
                _last = next;
            }
        }

        return Task.FromResult(_last());
    }

    public sealed record RecordedRequest(string Url, string UserAgent, DateTimeOffset SentAt);
}
