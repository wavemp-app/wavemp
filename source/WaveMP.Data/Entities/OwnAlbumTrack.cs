namespace WaveMP.Data.Entities;

/// <summary>
/// A song of an own album. A song is a member of at most one own album.
/// </summary>
public class OwnAlbumTrack
{
    public required Guid OwnAlbumId { get; set; }

    public OwnAlbum? OwnAlbum { get; set; }

    public required Guid TrackId { get; set; }

    public Track? Track { get; set; }

    /// <summary>
    /// Index of the song in the album, starting at 0; its track number is the position plus 1. Removed songs keep
    /// their last position.
    /// </summary>
    public required int Position { get; set; }

    public required OwnAlbumTrackState State { get; set; }

    /// <summary>
    /// The backup of the tags written by the first apply of the album to this song: its original tags, restored when
    /// the song is taken out of the album again.
    /// </summary>
    public Guid? OriginalTagBackupId { get; set; }

    public TagBackup? OriginalTagBackup { get; set; }

    public required DateTimeOffset AddedAt { get; set; }
}
