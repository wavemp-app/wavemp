namespace WaveMP.Data.Repositories;

/// <summary>
/// The album lists of the Subsonic API that the general album list does not offer.
/// </summary>
public enum SubsonicAlbumOrder
{
    /// <summary>
    /// Albums in random order.
    /// </summary>
    Random,

    /// <summary>
    /// Albums the user played, the most played first.
    /// </summary>
    Frequent,

    /// <summary>
    /// Albums the user played, the most recently played first.
    /// </summary>
    Recent,

    /// <summary>
    /// Albums with tracks the user rated, the highest average rating first.
    /// </summary>
    Highest,

    /// <summary>
    /// Albums of a range of years, in the direction of the range.
    /// </summary>
    Year,
}
