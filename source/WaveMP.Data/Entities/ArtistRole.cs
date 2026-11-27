namespace WaveMP.Data.Entities;

/// <summary>
/// The role of an artist on a song. Stored as its name.
/// </summary>
public enum ArtistRole
{
    /// <summary>
    /// A main artist of the song.
    /// </summary>
    Main,

    /// <summary>
    /// An artist featured on the song, e.g. "K.Flay" in "Bob Sinclar feat. K.Flay".
    /// </summary>
    Featured,
}
