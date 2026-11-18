using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Credentials;
using WaveMP.Core.Scrobbling.Options;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scrobbling.Queues;

internal sealed class ScrobbleQueueProcessor(
    IScrobbleRepository repository,
    IScrobbleSender sender,
    IScrobbleCredentialProtector protector,
    TimeProvider timeProvider,
    IOptions<ScrobblingOptions> options,
    ILogger<ScrobbleQueueProcessor> logger) : IScrobbleQueueProcessor
{
    public async Task<DateTimeOffset?> ProcessAsync(CancellationToken cancellationToken = default)
    {
        ScrobblingOptions settings = options.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();
        int expired = await repository.RemoveExpiredAsync(now - settings.MaxAge, cancellationToken);
        if (expired > 0)
        {
            logger.LogWarning("Dropped {Count} scrobbles that could not be sent in time", expired);
        }

        IReadOnlyList<ScrobbleConnection> connections =
            await repository.ListDueConnectionsAsync(now, cancellationToken);
        foreach (ScrobbleConnection connection in connections)
        {
            await SendAsync(connection, now, settings, cancellationToken);
        }

        return await repository.NextAttemptAtAsync(cancellationToken);
    }

    /// <summary>
    /// The wait before the next attempt after <paramref name="attempts"/> failed attempts: the retry delay, doubled
    /// with every further attempt, at most the longest retry delay.
    /// </summary>
    internal static TimeSpan RetryDelay(int attempts, ScrobblingOptions settings)
    {
        double factor = Math.Pow(2, Math.Clamp(attempts, 0, 30));
        double ticks = Math.Min(settings.RetryDelay.Ticks * factor, settings.MaxRetryDelay.Ticks);
        return TimeSpan.FromTicks((long)Math.Max(ticks, 0));
    }

    private async Task SendAsync(
        ScrobbleConnection connection,
        DateTimeOffset now,
        ScrobblingOptions settings,
        CancellationToken cancellationToken)
    {
        if (protector.Unprotect(connection.ProtectedCredential) is not { } credential)
        {
            logger.LogWarning(
                "The credentials of the {Service} connection {ConnectionId} cannot be decrypted; the user has to "
                + "connect the service again",
                connection.Service,
                connection.Id);
            await repository.SetConnectionStatusAsync(
                connection.Id,
                ScrobbleConnectionStatus.AuthorizationFailed,
                cancellationToken);
            return;
        }

        int batchSize = Math.Max(1, settings.BatchSize);
        while (true)
        {
            IReadOnlyList<ScrobbleQueueEntry> entries =
                await repository.ListDueEntriesAsync(connection.Id, now, batchSize, cancellationToken);
            if (entries.Count == 0)
            {
                return;
            }

            ScrobbleSendStatus status = await SendBatchAsync(connection, credential, entries, cancellationToken);
            if (status == ScrobbleSendStatus.Rejected && entries.Count > 1)
            {
                // One bad scrobble must not take the others with it: each is sent on its own.
                foreach (ScrobbleQueueEntry entry in entries)
                {
                    ScrobbleSendStatus single =
                        await SendBatchAsync(connection, credential, [entry], cancellationToken);
                    if (!await ApplyAsync(connection, [entry], single, now, settings, cancellationToken))
                    {
                        return;
                    }
                }

                continue;
            }

            if (!await ApplyAsync(connection, entries, status, now, settings, cancellationToken))
            {
                return;
            }
        }
    }

    private Task<ScrobbleSendStatus> SendBatchAsync(
        ScrobbleConnection connection,
        string credential,
        IReadOnlyList<ScrobbleQueueEntry> entries,
        CancellationToken cancellationToken)
    {
        return sender.SendScrobblesAsync(
            connection.Service,
            credential,
            [.. entries.Select(ScrobbleTracks.From)],
            cancellationToken);
    }

    /// <summary>
    /// Applies the answer of the service to the sent entries. Returns <c>false</c> if the connection cannot take more
    /// scrobbles for now.
    /// </summary>
    private async Task<bool> ApplyAsync(
        ScrobbleConnection connection,
        IReadOnlyList<ScrobbleQueueEntry> entries,
        ScrobbleSendStatus status,
        DateTimeOffset now,
        ScrobblingOptions settings,
        CancellationToken cancellationToken)
    {
        Guid[] ids = [.. entries.Select(entry => entry.Id)];
        switch (status)
        {
            case ScrobbleSendStatus.Accepted:
                await repository.RemoveEntriesAsync(
                    connection.Id,
                    ids,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
                return true;
            case ScrobbleSendStatus.Rejected:
                logger.LogWarning(
                    "{Service} rejected {Count} scrobbles for good; they are dropped",
                    connection.Service,
                    entries.Count);
                await repository.RemoveEntriesAsync(connection.Id, ids, null, cancellationToken);
                return true;
            case ScrobbleSendStatus.Unauthorized:
                logger.LogWarning(
                    "{Service} rejected the credentials of the connection {ConnectionId}; its scrobbles wait until "
                    + "the user connects the service again",
                    connection.Service,
                    connection.Id);
                await repository.SetConnectionStatusAsync(
                    connection.Id,
                    ScrobbleConnectionStatus.AuthorizationFailed,
                    cancellationToken);
                return false;
            default:
                TimeSpan delay = RetryDelay(entries.Max(entry => entry.Attempts), settings);
                logger.LogInformation(
                    "{Service} is unavailable; the scrobbles of the connection {ConnectionId} are sent again in "
                    + "{Delay}",
                    connection.Service,
                    connection.Id,
                    delay);
                await repository.PostponeDueEntriesAsync(connection.Id, now, now + delay, cancellationToken);
                return false;
        }
    }
}
