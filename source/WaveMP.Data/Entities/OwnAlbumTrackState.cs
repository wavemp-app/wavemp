namespace WaveMP.Data.Entities;

/// <summary>
/// Whether a song belongs to an own album. Stored as its name.
/// </summary>
public enum OwnAlbumTrackState
{
    /// <summary>
    /// The song is part of the own album.
    /// </summary>
    Member,

    /// <summary>
    /// The song was taken out of the own album after the album was applied to the files; the next apply restores its
    /// original tags and moves it by the template, then the row is deleted.
    /// </summary>
    Removed,
}
