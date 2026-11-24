using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// A song of an own album.
/// </summary>
/// <param name="TrackId">Id of the song.</param>
/// <param name="Position">Index of the song in the album, starting at 0.</param>
/// <param name="State">Whether the song is a member or was taken out after the last apply.</param>
/// <param name="OriginalTagBackupId">Backup of its original tags, written by the first apply.</param>
/// <param name="AddedAt">When the song was added.</param>
public sealed record OwnAlbumMemberRow(
    Guid TrackId,
    int Position,
    OwnAlbumTrackState State,
    Guid? OriginalTagBackupId,
    DateTimeOffset AddedAt);
