using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What the last lookup of the lyrics of a track in the online lyrics service (LRCLIB) found.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LyricsLookupStatusDto>))]
public enum LyricsLookupStatusDto
{
    /// <summary>
    /// Lyrics were found and saved as lyrics file with the base name of the track.
    /// </summary>
    SavedToFile,

    /// <summary>
    /// Lyrics were found, but the folder of the track is not writable; they are stored in the database.
    /// </summary>
    SavedToDatabase,

    /// <summary>
    /// The service has no lyrics for the track. The download job looks the track up again after a wait, or at once
    /// once the result is forgotten.
    /// </summary>
    NotFound,

    /// <summary>
    /// The service lists the track as instrumental. The download job looks the track up again after a wait, or at
    /// once once the result is forgotten.
    /// </summary>
    Instrumental,

    /// <summary>
    /// The track already had lyrics, so nothing was downloaded.
    /// </summary>
    NotNeeded,

    /// <summary>
    /// An admin uploaded lyrics for the track; the download job never looks it up again.
    /// </summary>
    Uploaded,
}
