namespace WaveMP.Data.Entities;

/// <summary>
/// Where a user stopped listening, one row per user: the queue, its current entry and position, shuffle and repeat,
/// where the list came from and the device that changed it last. The players save it while they play, so the user
/// continues on another device or after a reload. The tracks are kept by their ids only; tracks that left the library
/// are skipped when the state is read, while a track a rescan finds at a new place keeps its id and stays in the
/// queue. The state disappears with its user.
/// </summary>
public class PlaybackState
{
    /// <summary>
    /// Longest id of a device in <see cref="DeviceId"/>.
    /// </summary>
    public const int DeviceIdMaxLength = 64;

    /// <summary>
    /// Longest name of a device in <see cref="DeviceName"/>.
    /// </summary>
    public const int DeviceNameMaxLength = 100;

    public required Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// The tracks of the queue in the order they play; the same track may appear more than once.
    /// </summary>
    public List<Guid> TrackIds { get; set; } = [];

    /// <summary>
    /// Positions in <see cref="TrackIds"/> of the entries autoplay added after the list had ended, ascending.
    /// </summary>
    public List<int> AutoplayPositions { get; set; } = [];

    /// <summary>
    /// Position of the current entry in <see cref="TrackIds"/>; -1 without one.
    /// </summary>
    public required int CurrentIndex { get; set; }

    /// <summary>
    /// Position within the current track in milliseconds.
    /// </summary>
    public required int PositionMilliseconds { get; set; }

    /// <summary>
    /// Whether the queue is shuffled.
    /// </summary>
    public required bool Shuffle { get; set; }

    /// <summary>
    /// While shuffled: the positions in <see cref="TrackIds"/> in the order before shuffling, so turning shuffle off
    /// restores it; empty otherwise.
    /// </summary>
    public List<int> OriginalOrder { get; set; } = [];

    /// <summary>
    /// Repeat mode of the player. Stored as its name.
    /// </summary>
    public required RepeatMode Repeat { get; set; }

    /// <summary>
    /// What the list was started from; <c>null</c> for a list of single songs. Stored as its name.
    /// </summary>
    public PlaybackSourceKind? SourceKind { get; set; }

    /// <summary>
    /// Id of the album, artist, playlist or smart playlist of <see cref="SourceKind"/>. When a scan removes an album or
    /// artist whose songs moved to others, it points to the one that got most of them.
    /// </summary>
    public Guid? SourceId { get; set; }

    /// <summary>
    /// Id the device chose for itself, so it recognizes its own state.
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// Name of the device for "continue listening from ...", e.g. "Firefox · Windows".
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// When the state changed on its device; never later than the time the server saved it. The most recently
    /// changed state wins.
    /// </summary>
    public required DateTimeOffset ChangedAt { get; set; }
}
