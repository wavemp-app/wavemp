namespace WaveMP.Core.Organizing.Templates;

/// <summary>
/// Where a layout template puts a song: its folders below the root of the media source and its file name without
/// extension. All files of the song get this base name with their own extension.
/// </summary>
/// <param name="Folders">The folder names, outermost first; empty for a file-based template.</param>
/// <param name="BaseName">The file name without extension.</param>
/// <param name="IsTooLong">Whether the relative path of the file with the longest extension is longer than the scan
/// allows (UTF-8 bytes); such a song cannot be moved.</param>
public sealed record RenderedPath(IReadOnlyList<string> Folders, string BaseName, bool IsTooLong)
{
    /// <summary>
    /// The folder relative to the root of the source, with <c>/</c>; empty for the root itself.
    /// </summary>
    public string DirectoryPath => string.Join('/', Folders);

    /// <summary>
    /// The path of a file of the song relative to the root of the source, with <c>/</c>.
    /// </summary>
    /// <param name="extension">Extension without dot, or <c>null</c> for none.</param>
    public string RelativePathOf(string? extension)
    {
        string fileName = string.IsNullOrEmpty(extension) ? BaseName : BaseName + "." + extension;
        return Folders.Count == 0 ? fileName : DirectoryPath + "/" + fileName;
    }
}
