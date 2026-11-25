using WaveMP.Core.Devices.Validations;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// The live playback of a profile as the server keeps it in memory: the last state the active device published, with
/// the time the position was measured.
/// </summary>
internal sealed record LivePlayback(
    LivePlaybackStatus Status,
    IReadOnlyList<PlaybackStateItem> Items,
    int CurrentIndex,
    int PositionMilliseconds,
    DateTimeOffset MeasuredAt,
    int DurationMilliseconds,
    bool Shuffle,
    IReadOnlyList<int> OriginalOrder,
    RepeatMode Repeat,
    PlaybackSource? Source,
    double Volume,
    bool Muted,
    bool Crossfade,
    bool Autoplay)
{
    /// <summary>
    /// The playback of a profile before any device published one: nothing loaded at full volume.
    /// </summary>
    public static LivePlayback Empty(DateTimeOffset now)
    {
        return new LivePlayback(
            LivePlaybackStatus.Idle,
            [],
            -1,
            0,
            now,
            0,
            false,
            [],
            RepeatMode.Off,
            null,
            1,
            false,
            false,
            false);
    }

    /// <summary>
    /// The state a device published; the queue stays as it is if the device left it out.
    /// </summary>
    public static LivePlayback From(LivePlaybackInput input, LivePlayback previous, DateTimeOffset now)
    {
        LiveQueueInput? queue = input.Queue;
        return new LivePlayback(
            input.Status,
            queue is null ? previous.Items : [.. queue.Items],
            input.CurrentIndex,
            input.PositionMilliseconds,
            now,
            input.DurationMilliseconds,
            queue?.Shuffle ?? previous.Shuffle,
            queue is null ? previous.OriginalOrder : queue.Shuffle ? [.. queue.OriginalOrder] : [],
            input.Repeat,
            queue is null ? previous.Source : queue.Source,
            input.Volume,
            input.Muted,
            input.Crossfade,
            input.Autoplay);
    }

    /// <summary>
    /// The position within the current song at <paramref name="now"/>: while playing, the time since it was measured
    /// is added, up to the end of the song.
    /// </summary>
    public int PositionAt(DateTimeOffset now)
    {
        if (Status != LivePlaybackStatus.Playing || now <= MeasuredAt)
        {
            return PositionMilliseconds;
        }

        long position = PositionMilliseconds + (long)(now - MeasuredAt).TotalMilliseconds;
        long end = DurationMilliseconds > 0 ? DurationMilliseconds : DeviceLimits.MaxMilliseconds;
        return (int)Math.Min(position, end);
    }

    /// <summary>
    /// Whether the queue (entries, order, shuffle and source) is the same as the one of <paramref name="other"/>.
    /// </summary>
    public bool HasSameQueueAs(LivePlayback other)
    {
        return Shuffle == other.Shuffle
            && Source == other.Source
            && Items.SequenceEqual(other.Items)
            && OriginalOrder.SequenceEqual(other.OriginalOrder);
    }

    /// <summary>
    /// The state as the remotes see it at <paramref name="now"/>.
    /// </summary>
    public LivePlaybackView ToView(
        long version,
        long queueVersion,
        string? activeDeviceId,
        DateTimeOffset now,
        PlaybackHandover? handover = null)
    {
        return new LivePlaybackView(
            version,
            queueVersion,
            activeDeviceId,
            Status,
            CurrentIndex,
            CurrentIndex >= 0 && CurrentIndex < Items.Count ? Items[CurrentIndex].TrackId : null,
            Items.Count,
            PositionAt(now),
            DurationMilliseconds,
            Shuffle,
            Repeat,
            Source,
            Volume,
            Muted,
            Crossfade,
            Autoplay,
            handover);
    }
}
