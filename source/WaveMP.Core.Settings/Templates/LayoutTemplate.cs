namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// A parsed layout template: the folder names and, last, the file name (without extension) of a song, each a list of
/// parts. A template with more than one name is folder-based (e.g. <c>{AlbumArtist}/{Album}/{Title}</c>), one with
/// only a file name is file-based (e.g. <c>{Artist} - {Title}</c>).
/// </summary>
public sealed record LayoutTemplate(IReadOnlyList<IReadOnlyList<LayoutTemplatePart>> Segments)
{
    public bool IsFolderBased => Segments.Count > 1;

    /// <summary>
    /// The folder names, without the file name.
    /// </summary>
    public IEnumerable<IReadOnlyList<LayoutTemplatePart>> Folders => Segments.Take(Segments.Count - 1);

    /// <summary>
    /// The file name without extension.
    /// </summary>
    public IReadOnlyList<LayoutTemplatePart> FileName => Segments[^1];
}
