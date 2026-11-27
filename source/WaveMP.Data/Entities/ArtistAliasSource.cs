namespace WaveMP.Data.Entities;

/// <summary>
/// Where an alias of an artist came from.
/// </summary>
public enum ArtistAliasSource
{
    /// <summary>
    /// Added by an admin on the page of the artist.
    /// </summary>
    Admin,

    /// <summary>
    /// Taken from MusicBrainz by the online lookup of the artist: one of its aliases or its sort name.
    /// </summary>
    MusicBrainz,
}
