namespace WaveMP.Data.Entities;

/// <summary>
/// What an album shows when it has no cover of its own (a picture in its folder, a picture embedded in its songs or an
/// online cover of the album). An album never shows the cover of another album or a picture with the name of one of
/// its songs. Stored as its name.
/// </summary>
public enum AlbumCoverFallback
{
    /// <summary>
    /// The picture of its album artist: a picture in the folder of the artist, an uploaded one or one taken over from
    /// the online lookup. The collage of album covers of an artist without a picture is no picture of the artist.
    /// </summary>
    ArtistPicture,

    /// <summary>
    /// Nothing: without a cover of its own the album shows no cover.
    /// </summary>
    None,
}
