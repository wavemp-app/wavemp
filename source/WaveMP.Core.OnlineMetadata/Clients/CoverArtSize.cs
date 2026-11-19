namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The sizes of the front covers of the Cover Art Archive (thumbnails of the longer side in pixels).
/// </summary>
public enum CoverArtSize
{
    /// <summary>
    /// 250 pixels, for previews.
    /// </summary>
    Small = 250,

    /// <summary>
    /// 500 pixels.
    /// </summary>
    Large = 500,

    /// <summary>
    /// 1200 pixels, the cover that is stored.
    /// </summary>
    ExtraLarge = 1200,
}
