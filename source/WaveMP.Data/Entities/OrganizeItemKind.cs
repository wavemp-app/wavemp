namespace WaveMP.Data.Entities;

/// <summary>
/// What an item of an organize operation changes. Stored as its name.
/// </summary>
public enum OrganizeItemKind
{
    /// <summary>
    /// The files and tags of a song.
    /// </summary>
    Track,

    /// <summary>
    /// The cover file of an own album in its album folder.
    /// </summary>
    AlbumCover,

    /// <summary>
    /// Files of a folder that belong to no song, e.g. its album cover, that follow the songs when all songs of the
    /// folder move into the same new folder.
    /// </summary>
    FolderFiles,
}
