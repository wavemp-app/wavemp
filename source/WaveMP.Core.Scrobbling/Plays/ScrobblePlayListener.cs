using WaveMP.Core.Scrobbling.Queues;
using WaveMP.Core.Streaming.Histories;

namespace WaveMP.Core.Scrobbling.Plays;

/// <summary>
/// Queues every play of the history as scrobble: clients report a play once half of the song or four minutes have
/// been played, which is the moment the services expect the scrobble.
/// </summary>
internal sealed class ScrobblePlayListener(IScrobbleQueue queue) : IPlayHistoryListener
{
    public Task PlayRecordedAsync(PlayHistoryItem item, CancellationToken cancellationToken = default)
    {
        return queue.EnqueueAsync(item.Entry.UserId, item.Track, item.Entry.PlayedAt, cancellationToken);
    }
}
