namespace WaveMP.Data.Repositories;

/// <summary>
/// Result of saving the songs of an own album.
/// </summary>
public enum OwnAlbumSaveStatus
{
    Saved,
    NotFound,

    /// <summary>
    /// A song is already a member of another own album.
    /// </summary>
    TrackTaken,
}
