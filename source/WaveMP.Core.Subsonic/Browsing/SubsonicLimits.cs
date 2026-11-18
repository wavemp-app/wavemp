namespace WaveMP.Core.Subsonic.Browsing;

/// <summary>
/// Default and largest sizes of the lists of the Subsonic API.
/// </summary>
public static class SubsonicLimits
{
    /// <summary>
    /// Default size of <c>getAlbumList2</c> and <c>getRandomSongs</c>.
    /// </summary>
    public const int DefaultListSize = 10;

    /// <summary>
    /// Largest size of <c>getAlbumList2</c> and <c>getRandomSongs</c>, as in the specification.
    /// </summary>
    public const int MaxListSize = 500;

    /// <summary>
    /// Default number of artists, albums and songs of <c>search3</c>.
    /// </summary>
    public const int DefaultSearchCount = 20;

    /// <summary>
    /// Largest number of artists, albums and songs of one page of <c>search3</c>; apps copy the library in pages.
    /// </summary>
    public const int MaxSearchCount = 1000;

    /// <summary>
    /// The size of a list that is not paged (e.g. all artists of <c>getArtists</c>).
    /// </summary>
    public const int Unlimited = int.MaxValue;

    /// <summary>
    /// The value of a size parameter within 0 and <paramref name="max"/>; <paramref name="fallback"/> if it is
    /// missing.
    /// </summary>
    public static int Size(int? value, int fallback, int max)
    {
        return Math.Clamp(value ?? fallback, 0, max);
    }

    /// <summary>
    /// The value of an offset parameter, at least 0.
    /// </summary>
    public static int Offset(int? value)
    {
        return Math.Max(0, value ?? 0);
    }
}
