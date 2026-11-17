namespace WaveMP.Data.Projections;

/// <summary>
/// A track with what its lyrics need: the names to look them up with, its lyrics file, the files whose tags may carry
/// lyrics and the result of the last online lookup.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="DirectoryPath">Folder of the track relative to the media folder.</param>
/// <param name="BaseName">File name of the track without extension, as on disk.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="ArtistName">Name of the artist, or <c>null</c> if unknown.</param>
/// <param name="AlbumTitle">Title of the album, or <c>null</c> for tracks without album.</param>
/// <param name="Duration">Duration of the track, or <c>null</c> if unknown.</param>
/// <param name="LyricsFilePath">Path of the lyrics file of the track relative to the media folder, or
/// <c>null</c>.</param>
/// <param name="AudioFilePath">Path of the primary audio file relative to the media folder, or <c>null</c>.</param>
/// <param name="VideoFilePath">Path of the primary video file relative to the media folder, or <c>null</c>.</param>
/// <param name="Download">The result of the last online lookup, or <c>null</c> if the track was never looked
/// up.</param>
public sealed record LyricsTrackRow(
    Guid TrackId,
    string DirectoryPath,
    string BaseName,
    string Title,
    string? ArtistName,
    string? AlbumTitle,
    TimeSpan? Duration,
    string? LyricsFilePath,
    string? AudioFilePath,
    string? VideoFilePath,
    LyricsDownloadRow? Download);
