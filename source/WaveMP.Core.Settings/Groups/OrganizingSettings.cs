namespace WaveMP.Core.Settings.Groups;

/// <summary>
/// Settings of the organization of the media files: the layout template that names the folders and files of a song
/// when an admin organizes the library (see <see cref="Templates.LayoutTemplateSyntax"/>). Whether it is folder-based
/// or file-based also decides how the scan cleans titles. Stored as the settings group <see cref="Key"/>; values
/// missing in the stored JSON keep the defaults below.
/// </summary>
public sealed class OrganizingSettings
{
    /// <summary>
    /// Key of the settings group.
    /// </summary>
    public const string Key = "organizing";

    /// <summary>
    /// The default layout: a folder per album artist and album, files named by track number and title.
    /// </summary>
    public const string DefaultLayoutTemplate = "{AlbumArtist}/{Album}/{TrackNumber:00} {Title}";

    /// <summary>
    /// The layout template, normalized (trimmed, NFC).
    /// </summary>
    public string LayoutTemplate { get; init; } = DefaultLayoutTemplate;
}
