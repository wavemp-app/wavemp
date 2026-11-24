namespace WaveMP.Core.ArtistPictures.Pictures;

/// <summary>
/// What an artist shows as its picture, in the order of priority.
/// </summary>
public enum ArtistPictureShownKind
{
    /// <summary>
    /// The picture file in the folder of the artist.
    /// </summary>
    Local,

    /// <summary>
    /// The picture an admin uploaded.
    /// </summary>
    Uploaded,

    /// <summary>
    /// The picture an admin took over from the online lookup.
    /// </summary>
    Online,

    /// <summary>
    /// No picture of its own: a collage of the covers of its albums.
    /// </summary>
    Albums,

    /// <summary>
    /// Neither a picture nor an album with a cover.
    /// </summary>
    None,
}
