namespace WaveMP.Data.Projections;

/// <summary>
/// A song of the library with its values and all its files, for planning an organize operation.
/// </summary>
/// <param name="TrackId">Id of the song.</param>
/// <param name="SourceId">Its media source.</param>
/// <param name="DirectoryPath">Its folder relative to the folder of the source.</param>
/// <param name="BaseName">Base name of its files.</param>
/// <param name="Title">Title as shown.</param>
/// <param name="ArtistName">Its artist.</param>
/// <param name="AlbumId">Its album.</param>
/// <param name="AlbumTitle">Title of its album.</param>
/// <param name="AlbumArtistName">Artist of its album.</param>
/// <param name="IsCompilation">Whether its album is a compilation.</param>
/// <param name="Year">Its year, otherwise the year of its album.</param>
/// <param name="TrackNumber">Its track number.</param>
/// <param name="DiscNumber">Its disc number.</param>
/// <param name="Genre">Its genre.</param>
/// <param name="Files">All its files: audio (also duplicates of lower quality), video, pictures and lyrics.</param>
public sealed record OrganizeSongRow(
    Guid TrackId,
    Guid SourceId,
    string DirectoryPath,
    string BaseName,
    string Title,
    string? ArtistName,
    Guid? AlbumId,
    string? AlbumTitle,
    string? AlbumArtistName,
    bool IsCompilation,
    int? Year,
    int? TrackNumber,
    int? DiscNumber,
    string? Genre,
    IReadOnlyList<OrganizeFileRow> Files);
