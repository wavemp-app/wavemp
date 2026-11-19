namespace WaveMP.Data.Projections;

/// <summary>
/// A track of a problem of the library: what identifies it and where its files lie.
/// </summary>
/// <param name="Id">Id of the track.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="ArtistId">Id of the artist of the track.</param>
/// <param name="ArtistName">Name of the artist of the track.</param>
/// <param name="AlbumId">Id of the album.</param>
/// <param name="AlbumTitle">Title of the album.</param>
/// <param name="AlbumArtistId">Id of the album artist.</param>
/// <param name="AlbumArtistName">Name of the album artist.</param>
/// <param name="AlbumIsCompilation">Whether the album is a compilation.</param>
/// <param name="SourceId">Id of the media source of the track.</param>
/// <param name="SourceName">Name of the media source.</param>
/// <param name="DirectoryPath">Folder of the track relative to the folder of its source.</param>
/// <param name="BaseName">File name of the track without extension.</param>
/// <param name="Duration">Duration of the track.</param>
/// <param name="HasAudio">Whether an audio file exists.</param>
/// <param name="HasVideo">Whether a video file exists.</param>
public sealed record LibraryIssueTrackRow(
    Guid Id,
    string Title,
    Guid? ArtistId,
    string? ArtistName,
    Guid? AlbumId,
    string? AlbumTitle,
    Guid? AlbumArtistId,
    string? AlbumArtistName,
    bool AlbumIsCompilation,
    Guid SourceId,
    string SourceName,
    string DirectoryPath,
    string BaseName,
    TimeSpan? Duration,
    bool HasAudio,
    bool HasVideo);
