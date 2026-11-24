namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// A value of a song that a layout template can name as placeholder, e.g. <c>{AlbumArtist}</c>. The names are
/// compared without regard to case.
/// </summary>
public enum TemplateField
{
    Artist,
    AlbumArtist,
    Album,
    Year,
    TrackNumber,
    DiscNumber,
    Title,
    Genre,
}
