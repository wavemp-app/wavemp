using Microsoft.Extensions.Options;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Credentials;
using WaveMP.Core.Scrobbling.Options;
using WaveMP.Core.Scrobbling.Queues;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scrobbling.Connections;

internal sealed class ScrobbleConnectionService(
    IScrobbleRepository repository,
    IScrobbleSender sender,
    IScrobbleCredentialProtector protector,
    ScrobbleQueueSignal signal,
    TimeProvider timeProvider,
    IOptions<ScrobblingOptions> options) : IScrobbleConnectionService
{
    public async Task<IReadOnlyList<ScrobbleServiceState>> GetStatesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScrobbleConnection> connections =
            await repository.ListConnectionsAsync(userId, cancellationToken);
        IReadOnlyDictionary<Guid, int> queued = await repository.CountQueuedAsync(userId, cancellationToken);
        return [.. Enum.GetValues<ScrobbleService>().Select(service =>
        {
            ScrobbleConnection? connection = connections.FirstOrDefault(stored => stored.Service == service);
            return State(service, connection, connection is null ? 0 : queued.GetValueOrDefault(connection.Id));
        })];
    }

    public async Task<ScrobbleConnectResult> ConnectAsync(
        Guid userId,
        ScrobbleService service,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable(service))
        {
            return new ScrobbleConnectResult(ScrobbleConnectStatus.NotConfigured, null);
        }

        ScrobbleAuthorization authorization = await sender.AuthorizeAsync(service, token.Trim(), cancellationToken);
        if (!authorization.IsAuthorized)
        {
            return new ScrobbleConnectResult(
                authorization.Status == ScrobbleAuthorizationStatus.Invalid
                    ? ScrobbleConnectStatus.InvalidToken
                    : ScrobbleConnectStatus.ServiceUnavailable,
                null);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ScrobbleConnection saved = await repository.SaveConnectionAsync(
            new ScrobbleConnection
            {
                Id = Guid.CreateVersion7(now),
                UserId = userId,
                Service = service,
                AccountName = authorization.AccountName,
                ProtectedCredential = protector.Protect(authorization.Credential),
                Status = ScrobbleConnectionStatus.Active,
                ConnectedAt = now,
            },
            cancellationToken);

        // Scrobbles that waited for a new authorization go out now.
        signal.Notify();
        IReadOnlyDictionary<Guid, int> queued = await repository.CountQueuedAsync(userId, cancellationToken);
        return new ScrobbleConnectResult(
            ScrobbleConnectStatus.Connected,
            State(service, saved, queued.GetValueOrDefault(saved.Id)));
    }

    public Task<bool> DisconnectAsync(
        Guid userId,
        ScrobbleService service,
        CancellationToken cancellationToken = default)
    {
        return repository.DeleteConnectionAsync(userId, service, cancellationToken);
    }

    private ScrobbleServiceState State(ScrobbleService service, ScrobbleConnection? connection, int queued)
    {
        bool available = IsAvailable(service);
        return new ScrobbleServiceState(
            service,
            available,
            connection,
            queued,
            available && service == ScrobbleService.LastFm ? LastFmAuthorizationUrl() : null);
    }

    private bool IsAvailable(ScrobbleService service)
    {
        return service != ScrobbleService.LastFm || options.Value.IsLastFmConfigured;
    }

    /// <summary>
    /// The page of the web authentication of Last.fm with the API key of WaveMP.
    /// </summary>
    private Uri LastFmAuthorizationUrl()
    {
        ScrobblingOptions settings = options.Value;
        UriBuilder builder = new(settings.LastFmAuthorizationUrl)
        {
            Query = "api_key=" + Uri.EscapeDataString(settings.LastFmApiKey!.Trim()),
        };
        return builder.Uri;
    }
}
