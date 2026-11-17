namespace WaveMP.Data.Projections;

/// <summary>
/// A track in the overview of the file selection: its files per category and the state of its selections.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="ArtistId">Id of the artist of the track.</param>
/// <param name="ArtistName">Name of the artist of the track.</param>
/// <param name="AlbumId">Id of the album.</param>
/// <param name="AlbumTitle">Title of the album.</param>
/// <param name="AlbumArtistId">Id of the album artist.</param>
/// <param name="AlbumArtistName">Name of the album artist.</param>
/// <param name="AlbumIsCompilation">Whether the album is a compilation.</param>
/// <param name="DirectoryPath">Folder of the track relative to the media folder.</param>
/// <param name="BaseName">File name of the track without extension.</param>
/// <param name="AudioCount">Number of audio files.</param>
/// <param name="VideoCount">Number of video files.</param>
/// <param name="CoverCount">Number of covers of the track itself: picture files with its base name and pictures
/// embedded in its files.</param>
/// <param name="Audio">State of the selection of the audio file.</param>
/// <param name="Video">State of the selection of the video file.</param>
/// <param name="Cover">State of the selection of the cover.</param>
public sealed record TrackFileSummaryRow(
    Guid TrackId,
    string Title,
    Guid? ArtistId,
    string? ArtistName,
    Guid? AlbumId,
    string? AlbumTitle,
    Guid? AlbumArtistId,
    string? AlbumArtistName,
    bool AlbumIsCompilation,
    string DirectoryPath,
    string BaseName,
    int AudioCount,
    int VideoCount,
    int CoverCount,
    FileSelectionState Audio,
    FileSelectionState Video,
    FileSelectionState Cover);
