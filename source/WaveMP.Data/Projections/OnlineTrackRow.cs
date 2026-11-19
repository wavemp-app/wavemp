using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// A track as the online search of its metadata needs it: where its files lie, its album and its cover rules.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="SourceId">Id of the media source of the track.</param>
/// <param name="DirectoryPath">Folder of the track relative to the folder of its source.</param>
/// <param name="BaseName">File name of the track without extension, as on disk.</param>
/// <param name="AlbumId">Id of the album, or <c>null</c> for tracks without album.</param>
/// <param name="Duration">Duration of the track, or <c>null</c> if unknown.</param>
/// <param name="TrackCoverPriority">The cover rule of the track; <c>null</c> inherits the rule of its album.</param>
/// <param name="AlbumCoverPriority">The cover rule of its album; <c>null</c> inherits the global rule.</param>
public sealed record OnlineTrackRow(
    Guid TrackId,
    Guid SourceId,
    string DirectoryPath,
    string BaseName,
    Guid? AlbumId,
    TimeSpan? Duration,
    CoverPriority? TrackCoverPriority,
    CoverPriority? AlbumCoverPriority);
