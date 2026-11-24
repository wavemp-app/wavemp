namespace WaveMP.Core.Organizing.Templates;

/// <summary>
/// The values of a song that a layout template names. Missing values are <c>null</c>; numbers are positive.
/// </summary>
/// <param name="Title">Title of the song (as shown, i.e. cleaned).</param>
/// <param name="Artist">Artist of the song.</param>
/// <param name="AlbumArtist">Album artist of its album.</param>
/// <param name="Album">Title of its album.</param>
/// <param name="Year">Year of the song.</param>
/// <param name="TrackNumber">Number of the track.</param>
/// <param name="DiscNumber">Number of the disc.</param>
/// <param name="Genre">Genre of the song.</param>
/// <param name="IsCompilation">Whether its album is a compilation; then a missing album artist becomes
/// <see cref="LayoutTemplateRenderer.VariousArtists"/>, so the songs of the compilation stay in one folder.</param>
public sealed record TemplateValues(
    string Title,
    string? Artist = null,
    string? AlbumArtist = null,
    string? Album = null,
    int? Year = null,
    int? TrackNumber = null,
    int? DiscNumber = null,
    string? Genre = null,
    bool IsCompilation = false);
