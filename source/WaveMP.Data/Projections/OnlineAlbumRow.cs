namespace WaveMP.Data.Projections;

/// <summary>
/// An album as the online search of its metadata needs it: its tracks in the order of disc and track number, with the
/// folders they lie in.
/// </summary>
/// <param name="AlbumId">Id of the album.</param>
/// <param name="Tracks">The tracks of the album.</param>
public sealed record OnlineAlbumRow(Guid AlbumId, IReadOnlyList<OnlineAlbumTrackRow> Tracks);

/// <summary>
/// A track of an album in the online search.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="SourceId">Id of the media source of the track.</param>
/// <param name="DirectoryPath">Folder of the track relative to the folder of its source.</param>
/// <param name="Duration">Duration of the track, or <c>null</c> if unknown.</param>
public sealed record OnlineAlbumTrackRow(Guid TrackId, Guid SourceId, string DirectoryPath, TimeSpan? Duration);
