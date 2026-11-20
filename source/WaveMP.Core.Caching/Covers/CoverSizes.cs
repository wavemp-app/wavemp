namespace WaveMP.Core.Caching.Covers;

/// <summary>
/// The sizes of scaled pictures: the largest width and height in pixels. Clients may ask for any size from
/// <see cref="MinRequested"/> to <see cref="MaxRequested"/>; it is rounded up to the next defined size, so the cache
/// stays small. Covers of albums and tracks are scaled from the pictures of the library, which are often large, in
/// all <see cref="Scaled"/> sizes, so that they stay sharp on screens with a pixel density of 2 or 3 and on large
/// screens. Pictures stored by WaveMP itself (avatars, own covers of playlists) exist in the <see cref="Stored"/>
/// sizes only.
/// </summary>
public static class CoverSizes
{
    /// <summary>
    /// Lists and small covers, e.g. the mini player.
    /// </summary>
    public const int Small = 96;

    /// <summary>
    /// Small covers on screens with a pixel density of 2 or 3.
    /// </summary>
    public const int SmallDense = 192;

    /// <summary>
    /// Tiles and detail pages.
    /// </summary>
    public const int Medium = 300;

    /// <summary>
    /// Tiles and detail pages on screens with a pixel density of 2 or 3.
    /// </summary>
    public const int MediumDense = 600;

    /// <summary>
    /// Large covers, e.g. the stage of Now Playing and the full-screen player of phones.
    /// </summary>
    public const int Large = 1000;

    /// <summary>
    /// Large covers on screens with a high pixel density, e.g. the stage of Now Playing on a 4K screen; created only
    /// when they are asked for.
    /// </summary>
    public const int ExtraLarge = 2000;

    public const int MinRequested = 1;
    public const int MaxRequested = 4096;

    /// <summary>
    /// The sizes of covers of albums and tracks, created from the picture of the library when they are first asked
    /// for (the smaller ones after a scan as well, see <see cref="Prepared"/>).
    /// </summary>
    public static IReadOnlyList<int> Scaled { get; } = [Small, SmallDense, Medium, MediumDense, Large, ExtraLarge];

    /// <summary>
    /// The sizes of pictures WaveMP stores itself when they are uploaded (avatars, own covers of playlists).
    /// </summary>
    public static IReadOnlyList<int> Stored { get; } = [Small, Medium, Large];

    /// <summary>
    /// The sizes prepared after a scan.
    /// </summary>
    public static IReadOnlyList<int> Prepared { get; } = [Small, Medium];

    /// <summary>
    /// The size of <see cref="Scaled"/> for a cover of an album or track: the smallest that is at least as large as
    /// the requested one, otherwise the largest.
    /// </summary>
    public static int SnapScaled(int requested)
    {
        return SnapTo(Scaled, requested);
    }

    /// <summary>
    /// The size of <see cref="Stored"/> for a stored picture: the smallest that is at least as large as the requested
    /// one, otherwise the largest.
    /// </summary>
    public static int SnapStored(int requested)
    {
        return SnapTo(Stored, requested);
    }

    private static int SnapTo(IReadOnlyList<int> sizes, int requested)
    {
        foreach (int size in sizes)
        {
            if (size >= requested)
            {
                return size;
            }
        }

        return sizes[^1];
    }
}
