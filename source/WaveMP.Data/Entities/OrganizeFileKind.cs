namespace WaveMP.Data.Entities;

/// <summary>
/// Kind of a file an organize operation moves. Stored as its name.
/// </summary>
public enum OrganizeFileKind
{
    Audio,
    Video,
    Image,
    Lyrics,

    /// <summary>
    /// The cover of an own album, written from the data folder into the album folder.
    /// </summary>
    AlbumCover,
}
