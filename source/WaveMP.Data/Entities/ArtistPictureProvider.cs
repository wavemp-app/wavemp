namespace WaveMP.Data.Entities;

/// <summary>
/// The service an online artist picture came from.
/// </summary>
public enum ArtistPictureProvider
{
    /// <summary>
    /// Wikimedia Commons, found through the Wikidata item of the MusicBrainz artist.
    /// </summary>
    Wikimedia,

    /// <summary>
    /// fanart.tv, asked with the API key of the admin.
    /// </summary>
    FanartTv,
}
