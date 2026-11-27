using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What publishing uploaded lyrics to the online lyrics service (LRCLIB) did.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LyricsPublicationDto>))]
public enum LyricsPublicationDto
{
    /// <summary>
    /// Publishing was not asked for.
    /// </summary>
    NotRequested,

    /// <summary>
    /// LRCLIB took the lyrics.
    /// </summary>
    Published,

    /// <summary>
    /// The download of lyrics is switched off in the library settings, which also keeps WaveMP from contacting
    /// LRCLIB to publish.
    /// </summary>
    Disabled,

    /// <summary>
    /// The track lacks what LRCLIB needs: title, artist, album and duration.
    /// </summary>
    MissingMetadata,

    /// <summary>
    /// LRCLIB refused the lyrics.
    /// </summary>
    Rejected,

    /// <summary>
    /// LRCLIB refuses requests for a while; try again later.
    /// </summary>
    RateLimited,

    /// <summary>
    /// LRCLIB cannot be reached, or its challenge (a proof of work) could not be solved in time.
    /// </summary>
    Unavailable,
}
