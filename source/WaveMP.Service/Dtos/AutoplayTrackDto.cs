namespace WaveMP.Service.Dtos;

/// <summary>
/// A song autoplay chose, with how the client plays it.
/// </summary>
public sealed class AutoplayTrackDto
{
    /// <summary>
    /// The song.
    /// </summary>
    public required TrackDto Track { get; init; }

    /// <summary>
    /// How the client plays the song, as GetTrackPlayback answers for the formats of the request, with the signed
    /// stream URL. The URL expires (expiresAt); clients ask GetTrackPlayback again for a song whose URL has expired
    /// when it is due.
    /// </summary>
    public required TrackPlaybackDto Playback { get; init; }
}
