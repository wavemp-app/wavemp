using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Where lyrics come from.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LyricsSourceDto>))]
public enum LyricsSourceDto
{
    /// <summary>
    /// The lyrics file with the base name of the track (Song.lrc next to Song.mp3), also one saved by a download.
    /// </summary>
    File,

    /// <summary>
    /// Lyrics downloaded from LRCLIB and stored in the database, because the folder of the track is not writable.
    /// </summary>
    Download,

    /// <summary>
    /// Lyrics an admin uploaded, stored in the database, because the folder of the track is not writable.
    /// </summary>
    Upload,

    /// <summary>
    /// The tags of the audio file (or of the video file of tracks without audio).
    /// </summary>
    Tags,
}
