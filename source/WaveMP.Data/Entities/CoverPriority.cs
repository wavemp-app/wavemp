namespace WaveMP.Data.Entities;

/// <summary>
/// Which cover a track shows: its own cover (a picture with its name, a cover selected for it or an online cover of
/// the track) or the cover of its album (a picture in the folder of the album, a picture embedded in the songs of the
/// album or an online cover of the album). A track never shows the picture of an artist. Stored as its name.
/// </summary>
public enum CoverPriority
{
    /// <summary>
    /// The cover of the album, otherwise the own cover.
    /// </summary>
    AlbumFirst,

    /// <summary>
    /// The own cover, otherwise the cover of the album.
    /// </summary>
    TrackFirst,

    /// <summary>
    /// Only the own cover; without one the track shows no cover.
    /// </summary>
    TrackOnly,
}
