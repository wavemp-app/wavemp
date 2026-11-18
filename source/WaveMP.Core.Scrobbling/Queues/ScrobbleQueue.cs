using Microsoft.Extensions.Options;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Options;
using WaveMP.Core.Scrobbling.Rules;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scrobbling.Queues;

internal sealed class ScrobbleQueue(
    IScrobbleRepository repository,
    ScrobbleQueueSignal signal,
    TimeProvider timeProvider,
    IOptions<ScrobblingOptions> options) : IScrobbleQueue
{
    public async Task<int> EnqueueAsync(
        Guid userId,
        TrackRow track,
        DateTimeOffset playedAt,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!ScrobbleRules.IsEligible(track.Duration)
            || ScrobbleRules.IsExpired(playedAt, now, options.Value.MaxAge)
            || ScrobbleTracks.From(track) is not { } song)
        {
            return 0;
        }

        // Also for connections whose authorization failed: their scrobbles are sent once the user connects again.
        IReadOnlyList<ScrobbleConnection> connections =
            await repository.ListConnectionsAsync(userId, cancellationToken);
        if (connections.Count == 0)
        {
            return 0;
        }

        ScrobbleQueueEntry[] entries = [.. connections.Select(connection => new ScrobbleQueueEntry
        {
            Id = Guid.CreateVersion7(now),
            ConnectionId = connection.Id,
            Artist = song.Artist,
            Title = song.Title,
            Album = song.Album,
            AlbumArtist = song.AlbumArtist,
            TrackNumber = song.TrackNumber,
            Duration = song.Duration,
            PlayedAt = playedAt,
            NextAttemptAt = now,
        })];
        int added = await repository.EnqueueAsync(entries, cancellationToken);
        if (added > 0)
        {
            signal.Notify();
        }

        return added;
    }
}
