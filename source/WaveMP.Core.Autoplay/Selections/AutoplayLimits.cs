namespace WaveMP.Core.Autoplay.Selections;

/// <summary>
/// Limits of a request for the songs of autoplay.
/// </summary>
public static class AutoplayLimits
{
    /// <summary>
    /// Most tracks of the list autoplay continues; a longer list is represented by its last tracks.
    /// </summary>
    public const int MaxListTracks = 500;

    /// <summary>
    /// Most tracks a request excludes, e.g. the songs that already wait in the autoplay list of the client.
    /// </summary>
    public const int MaxExcludedTracks = 500;

    /// <summary>
    /// Number of songs a request returns without a count.
    /// </summary>
    public const int DefaultCount = 10;

    /// <summary>
    /// Most songs one request returns.
    /// </summary>
    public const int MaxCount = 50;
}
