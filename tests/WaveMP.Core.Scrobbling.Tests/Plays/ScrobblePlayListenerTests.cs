using NSubstitute;
using WaveMP.Core.Scrobbling.Plays;
using WaveMP.Core.Scrobbling.Queues;
using WaveMP.Core.Scrobbling.Tests.Clients;
using WaveMP.Core.Streaming.Histories;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Scrobbling.Tests.Plays;

[TestFixture]
public class ScrobblePlayListenerTests
{
    [Test]
    public async Task PlayRecordedAsync_Always_QueuesThePlayOfTheUserWithItsStart()
    {
        IScrobbleQueue queue = Substitute.For<IScrobbleQueue>();
        TrackRow track = ScrobbleTracksTests.Track("Title", "Artist", null, null);
        PlayHistoryEntry entry = new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TrackId = track.Id,
            PlayedAt = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero),
        };

        await new ScrobblePlayListener(queue).PlayRecordedAsync(new PlayHistoryItem(entry, track));

        await queue.Received(1).EnqueueAsync(entry.UserId, track, entry.PlayedAt, Arg.Any<CancellationToken>());
    }
}
