using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the settings of the online search for album info. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateOnlineMetadataSettingsDto
{
    /// <summary>
    /// Switches the online search on or off.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// Address of MusicBrainz: an absolute http or https address without query; a trailing slash is removed.
    /// </summary>
    [StringLength(2048)]
    public string? MusicBrainzUrl { get; init; }

    /// <summary>
    /// Address of the Cover Art Archive: an absolute http or https address without query; a trailing slash is
    /// removed.
    /// </summary>
    [StringLength(2048)]
    public string? CoverArtArchiveUrl { get; init; }

    /// <summary>
    /// Shortest time between two requests to MusicBrainz in milliseconds; musicbrainz.org always gets at least one
    /// second.
    /// </summary>
    [Range(0, 60_000)]
    public int? MusicBrainzRequestIntervalMilliseconds { get; init; }
}
