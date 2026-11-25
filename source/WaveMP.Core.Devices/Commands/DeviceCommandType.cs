namespace WaveMP.Core.Devices.Commands;

/// <summary>
/// What a remote asks the active device to do.
/// </summary>
public enum DeviceCommandType
{
    /// <summary>
    /// Play (resume) the current song.
    /// </summary>
    Play,

    /// <summary>
    /// Pause.
    /// </summary>
    Pause,

    /// <summary>
    /// Stop and go back to the beginning of the current song.
    /// </summary>
    Stop,

    /// <summary>
    /// Go to the next song.
    /// </summary>
    Next,

    /// <summary>
    /// Go to the previous song (or the beginning of the current one).
    /// </summary>
    Previous,

    /// <summary>
    /// Jump to <see cref="DeviceCommand.PositionMilliseconds"/> in the current song.
    /// </summary>
    Seek,

    /// <summary>
    /// Set the volume to <see cref="DeviceCommand.Volume"/> (and mute or unmute with
    /// <see cref="DeviceCommand.Muted"/>).
    /// </summary>
    SetVolume,

    /// <summary>
    /// Turn shuffle on or off (<see cref="DeviceCommand.Enabled"/>).
    /// </summary>
    SetShuffle,

    /// <summary>
    /// Set the repeat mode (<see cref="DeviceCommand.Repeat"/>).
    /// </summary>
    SetRepeat,

    /// <summary>
    /// Turn crossfading on or off (<see cref="DeviceCommand.Enabled"/>).
    /// </summary>
    SetCrossfade,

    /// <summary>
    /// Turn autoplay on or off (<see cref="DeviceCommand.Enabled"/>).
    /// </summary>
    SetAutoplay,

    /// <summary>
    /// Replace the queue with <see cref="DeviceCommand.TrackIds"/> and play from
    /// <see cref="DeviceCommand.StartIndex"/>, e.g. a song, an album or a playlist.
    /// </summary>
    PlayTracks,

    /// <summary>
    /// Add <see cref="DeviceCommand.TrackIds"/> to the end of the queue.
    /// </summary>
    AddToQueue,

    /// <summary>
    /// Insert <see cref="DeviceCommand.TrackIds"/> after the current entry.
    /// </summary>
    PlayNext,

    /// <summary>
    /// Play the entry at <see cref="DeviceCommand.Index"/>.
    /// </summary>
    PlayQueueEntry,

    /// <summary>
    /// Remove the entry at <see cref="DeviceCommand.Index"/>.
    /// </summary>
    RemoveQueueEntry,

    /// <summary>
    /// Move the entry at <see cref="DeviceCommand.Index"/> to <see cref="DeviceCommand.ToIndex"/>.
    /// </summary>
    MoveQueueEntry,

    /// <summary>
    /// Remove all entries except the current one.
    /// </summary>
    ClearQueue,
}
