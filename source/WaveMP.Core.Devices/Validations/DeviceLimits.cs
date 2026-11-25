using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Validations;

/// <summary>
/// Limits of the devices and their messages.
/// </summary>
public static class DeviceLimits
{
    /// <summary>
    /// Most devices a profile keeps; registering another one forgets the least recently active closed devices above
    /// the limit.
    /// </summary>
    public const int MaxDevicesPerProfile = 50;

    /// <summary>
    /// Longest id of a device.
    /// </summary>
    public const int IdMaxLength = Device.IdMaxLength;

    /// <summary>
    /// Longest name of a device, reported or given by the user.
    /// </summary>
    public const int NameMaxLength = Device.NameMaxLength;

    /// <summary>
    /// Most entries of a live queue, the same as of a saved playback state.
    /// </summary>
    public const int MaxQueueItems = PlaybackStateLimits.MaxItems;

    /// <summary>
    /// Most songs of a command that plays or queues songs.
    /// </summary>
    public const int MaxTrackIds = PlaybackStateLimits.MaxItems;

    /// <summary>
    /// Longest position or duration of a song in milliseconds (24 hours).
    /// </summary>
    public const int MaxMilliseconds = 24 * 60 * 60 * 1000;

    /// <summary>
    /// How long the live state of a profile stays in memory after its last device closed its connection.
    /// </summary>
    public static readonly TimeSpan IdleSessionLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How often the last activity of a connected device is written at most while it plays or sends commands.
    /// </summary>
    public static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);
}
