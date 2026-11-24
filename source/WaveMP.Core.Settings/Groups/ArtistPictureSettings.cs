namespace WaveMP.Core.Settings.Groups;

/// <summary>
/// Settings of the artist pictures from the internet: Wikidata and Wikimedia Commons (through the MusicBrainz artist)
/// and, with an API key, fanart.tv. The online lookup is off by default; while it is off, WaveMP sends no request for
/// artist pictures to any service. Stored as the settings group <see cref="Key"/>; values missing in the stored JSON
/// keep the defaults below. The address of MusicBrainz and the time between its requests come from
/// <see cref="OnlineMetadataSettings"/>.
/// </summary>
public sealed class ArtistPictureSettings
{
    /// <summary>
    /// Key of the settings group.
    /// </summary>
    public const string Key = "artist-pictures";

    /// <summary>
    /// Whether admins may look up artist pictures online. Off by default, because it sends artist names to MusicBrainz
    /// and MusicBrainz artist ids to Wikidata, Wikimedia Commons and fanart.tv.
    /// </summary>
    public bool OnlineLookupEnabled { get; init; }

    /// <summary>
    /// The personal API key of fanart.tv the admin entered; without one, fanart.tv is not asked. A secret: it is never
    /// returned by the API, never written to the log and only sent to fanart.tv in a request header.
    /// </summary>
    public string? FanartTvApiKey { get; init; }
}
