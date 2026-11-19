namespace WaveMP.Service.Dtos;

/// <summary>
/// Settings of the online search for album info: tags from MusicBrainz and covers from the Cover Art Archive.
/// </summary>
public sealed class OnlineMetadataSettingsDto
{
    /// <summary>
    /// Whether admins may search album info online. Off by default; while it is off, WaveMP sends no request to
    /// either service.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// Address of MusicBrainz (default <c>https://musicbrainz.org</c>), e.g. an own mirror; no trailing slash.
    /// </summary>
    public required string MusicBrainzUrl { get; init; }

    /// <summary>
    /// Address of the Cover Art Archive (default <c>https://coverartarchive.org</c>); no trailing slash.
    /// </summary>
    public required string CoverArtArchiveUrl { get; init; }

    /// <summary>
    /// Shortest time between two requests to MusicBrainz in milliseconds (0 to 60,000, default 1,000). Applies to an
    /// own mirror; musicbrainz.org always gets at least one second.
    /// </summary>
    public required int MusicBrainzRequestIntervalMilliseconds { get; init; }
}
