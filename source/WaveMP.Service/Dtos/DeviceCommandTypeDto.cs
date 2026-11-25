using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What a remote asks the active device to do.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<DeviceCommandTypeDto>))]
public enum DeviceCommandTypeDto
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
    /// Jump to <c>positionMilliseconds</c> in the current song.
    /// </summary>
    Seek,

    /// <summary>
    /// Set the volume to <c>volume</c> (and mute or unmute with <c>muted</c>).
    /// </summary>
    SetVolume,

    /// <summary>
    /// Turn shuffle on or off (<c>enabled</c>).
    /// </summary>
    SetShuffle,

    /// <summary>
    /// Set the repeat mode (<c>repeat</c>).
    /// </summary>
    SetRepeat,

    /// <summary>
    /// Turn crossfading on or off (<c>enabled</c>).
    /// </summary>
    SetCrossfade,

    /// <summary>
    /// Turn autoplay on or off (<c>enabled</c>).
    /// </summary>
    SetAutoplay,

    /// <summary>
    /// Replace the queue with the songs and play from <c>startIndex</c>, e.g. a song, an album or a playlist.
    /// </summary>
    PlayTracks,

    /// <summary>
    /// Add the songs to the end of the queue.
    /// </summary>
    AddToQueue,

    /// <summary>
    /// Insert the songs after the current entry.
    /// </summary>
    PlayNext,

    /// <summary>
    /// Play the entry at <c>index</c>.
    /// </summary>
    PlayQueueEntry,

    /// <summary>
    /// Remove the entry at <c>index</c>.
    /// </summary>
    RemoveQueueEntry,

    /// <summary>
    /// Move the entry at <c>index</c> to <c>toIndex</c>.
    /// </summary>
    MoveQueueEntry,

    /// <summary>
    /// Remove all entries except the current one.
    /// </summary>
    ClearQueue,
}
