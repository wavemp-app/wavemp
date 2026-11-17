namespace WaveMP.Service.Dtos;

/// <summary>
/// A track in the overview of the file selection: its number of files per category and the state of its selections.
/// </summary>
public sealed class TrackFileSummaryDto
{
    public required Guid TrackId { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// Artist of the track; null if unknown.
    /// </summary>
    public ArtistReferenceDto? Artist { get; init; }

    /// <summary>
    /// Album of the track; null for tracks without album.
    /// </summary>
    public AlbumReferenceDto? Album { get; init; }

    /// <summary>
    /// Folder of the track relative to the media folder with / as separator; empty for the media folder itself.
    /// </summary>
    public required string DirectoryPath { get; init; }

    /// <summary>
    /// File name of the track without extension, shared by all its files.
    /// </summary>
    public required string BaseName { get; init; }

    public required int AudioCount { get; init; }

    public required int VideoCount { get; init; }

    /// <summary>
    /// Number of covers of the track itself: pictures with its base name and pictures embedded in its files.
    /// </summary>
    public required int CoverCount { get; init; }

    public required FileSelectionStateDto Audio { get; init; }

    public required FileSelectionStateDto Video { get; init; }

    public required FileSelectionStateDto Cover { get; init; }
}
