namespace WaveMP.Service.Dtos;

/// <summary>
/// Settings of the artist pictures from the internet (Wikidata and Wikimedia Commons through the MusicBrainz artist,
/// and fanart.tv with an API key). The API key itself is never returned.
/// </summary>
public sealed class ArtistPictureSettingsDto
{
    /// <summary>
    /// Whether admins may look up artist pictures online. Off by default; while it is off, WaveMP sends no request for
    /// artist pictures to any service.
    /// </summary>
    public required bool OnlineLookupEnabled { get; init; }

    /// <summary>
    /// Whether an API key of fanart.tv is stored; only then is fanart.tv asked. The key can be set or removed
    /// (UpdateArtistPictureSettings) but never read.
    /// </summary>
    public required bool FanartTvApiKeySet { get; init; }
}
