using AwesomeAssertions;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Devices.Tests.Fakes;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Tests.Playbacks;

[TestFixture]
public class LivePlaybackTests
{
    private static readonly DateTimeOffset _now = DeviceTestContext.Start;
    private static readonly Guid[] _tracks = [Guid.NewGuid(), Guid.NewGuid()];

    [Test]
    public void PositionAt_WhilePlaying_AddsTheTimeSinceItWasMeasured()
    {
        LivePlayback playback = Published(DeviceTestContext.Playback(_tracks, 0, 10_000));

        playback.PositionAt(_now.AddSeconds(2.5)).Should().Be(12_500);
    }

    [Test]
    public void PositionAt_WhilePlayingPastTheEnd_StopsAtTheEndOfTheSong()
    {
        LivePlayback playback = Published(DeviceTestContext.Playback(_tracks, 0, 179_000));

        playback.PositionAt(_now.AddMinutes(5)).Should().Be(180_000);
    }

    [Test]
    public void PositionAt_WhilePlayingWithoutDuration_StopsAt24Hours()
    {
        LivePlayback playback = Published(DeviceTestContext.Playback(_tracks, 0, 0) with { DurationMilliseconds = 0 });

        playback.PositionAt(_now.AddDays(3)).Should().Be(86_400_000);
    }

    [TestCase(LivePlaybackStatus.Paused)]
    [TestCase(LivePlaybackStatus.Loading)]
    [TestCase(LivePlaybackStatus.Idle)]
    public void PositionAt_WhileNotPlaying_KeepsThePosition(LivePlaybackStatus status)
    {
        LivePlayback playback = Published(DeviceTestContext.Playback(_tracks, 0, 10_000) with { Status = status });

        playback.PositionAt(_now.AddMinutes(1)).Should().Be(10_000);
    }

    [Test]
    public void From_WithoutQueue_KeepsTheQueueOfThePreviousState()
    {
        LivePlayback previous = Published(DeviceTestContext.Playback(_tracks) with
        {
            Queue = new LiveQueueInput(
                [new(_tracks[0], false), new(_tracks[1], true)],
                true,
                [1, 0],
                new PlaybackSource(PlaybackSourceKind.Playlist, Guid.Empty)),
        });

        var next = LivePlayback.From(
            DeviceTestContext.Playback(_tracks, 1) with { Queue = null, Repeat = RepeatMode.All },
            previous,
            _now);

        next.HasSameQueueAs(previous).Should().BeTrue();
        next.CurrentIndex.Should().Be(1);
        next.Repeat.Should().Be(RepeatMode.All);
    }

    [Test]
    public void From_NotShuffled_DropsTheOrderBeforeShuffling()
    {
        var next = LivePlayback.From(
            DeviceTestContext.Playback(_tracks) with
            {
                Queue = new LiveQueueInput([new(_tracks[0], false), new(_tracks[1], false)], false, [1, 0], null),
            },
            LivePlayback.Empty(_now),
            _now);

        next.OriginalOrder.Should().BeEmpty();
    }

    [Test]
    public void HasSameQueueAs_OtherOrderShuffleOrSource_IsFalse()
    {
        LivePlayback playback = Published(DeviceTestContext.Playback(_tracks));

        playback.HasSameQueueAs(playback with { Items = [.. playback.Items.Reverse()] }).Should().BeFalse();
        playback.HasSameQueueAs(playback with { Shuffle = true }).Should().BeFalse();
        playback.HasSameQueueAs(playback with { Source = new PlaybackSource(PlaybackSourceKind.Album, Guid.Empty) })
            .Should().BeFalse();
        playback.HasSameQueueAs(playback with { Items = [.. playback.Items] }).Should().BeTrue();
    }

    [Test]
    public void ToView_Always_NamesTheCurrentSongAndTheLengthOfTheQueue()
    {
        LivePlayback playback = Published(DeviceTestContext.Playback(_tracks, 1, 1_000));

        LivePlaybackView view = playback.ToView(7, 5, "laptop", _now, new PlaybackHandover("phone", true));

        view.Should().BeEquivalentTo(new
        {
            Version = 7L,
            QueueVersion = 5L,
            ActiveDeviceId = "laptop",
            CurrentTrackId = (Guid?)_tracks[1],
            QueueLength = 2,
            PositionMilliseconds = 1_000,
            Handover = new PlaybackHandover("phone", true),
        });
    }

    [Test]
    public void ToView_WithoutCurrentEntry_HasNoCurrentSong()
    {
        LivePlaybackView view = LivePlayback.Empty(_now).ToView(1, 1, null, _now);

        view.CurrentTrackId.Should().BeNull();
        view.QueueLength.Should().Be(0);
    }

    private static LivePlayback Published(LivePlaybackInput input)
    {
        return LivePlayback.From(input, LivePlayback.Empty(_now), _now);
    }
}
