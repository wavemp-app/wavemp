namespace WaveMP.Service.Dtos;

/// <summary>
/// A track of a problem of the library: what identifies it and where its files lie.
/// </summary>
public sealed class LibraryIssueTrackDto
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// Artist of the track; null if unknown.
    /// </summary>
    public ArtistReferenceDto? Artist { get; init; }

    /// <summary>
    /// All artists of the track in their order, each with its role; the first one is Artist.
    /// </summary>
    public required IReadOnlyList<ArtistCreditDto> Artists { get; init; }

    /// <summary>
    /// Album of the track; null for tracks without album.
    /// </summary>
    public AlbumReferenceDto? Album { get; init; }

    /// <summary>
    /// The media source the files of the track lie in.
    /// </summary>
    public required Guid SourceId { get; init; }

    public required string SourceName { get; init; }

    /// <summary>
    /// Folder of the track relative to the folder of its media source with / as separator; empty for that folder
    /// itself.
    /// </summary>
    public required string DirectoryPath { get; init; }

    /// <summary>
    /// File name of the track without extension, shared by all its files.
    /// </summary>
    public required string BaseName { get; init; }

    /// <summary>
    /// Duration in milliseconds; null if unknown.
    /// </summary>
    public long? DurationMilliseconds { get; init; }

    public required bool HasAudio { get; init; }

    public required bool HasVideo { get; init; }
}
