namespace WaveMP.Data.Projections;

/// <summary>
/// The values of a song that a layout template names, with the names of its files.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Title">Title of the track.</param>
/// <param name="ArtistName">Name of the artist, or <c>null</c> if unknown.</param>
/// <param name="AlbumTitle">Title of the album, or <c>null</c> for tracks without album.</param>
/// <param name="AlbumArtistName">Name of the album artist, or <c>null</c>.</param>
/// <param name="IsCompilation">Whether the album is a compilation.</param>
/// <param name="Year">Year of the track, otherwise of its album, or <c>null</c>.</param>
/// <param name="TrackNumber">Number of the track, or <c>null</c>.</param>
/// <param name="DiscNumber">Number of the disc, or <c>null</c>.</param>
/// <param name="Genre">Genre of the track, or <c>null</c>.</param>
/// <param name="BaseName">File name of the track without extension, as on disk.</param>
/// <param name="PrimaryExtension">Extension of the file the track plays (audio before video), or <c>null</c>.</param>
/// <param name="Extensions">Extensions of all files of the track, lower case without dot.</param>
public sealed record TemplateSongRow(
    Guid TrackId,
    string Title,
    string? ArtistName,
    string? AlbumTitle,
    string? AlbumArtistName,
    bool IsCompilation,
    int? Year,
    int? TrackNumber,
    int? DiscNumber,
    string? Genre,
    string BaseName,
    string? PrimaryExtension,
    IReadOnlyList<string> Extensions);
