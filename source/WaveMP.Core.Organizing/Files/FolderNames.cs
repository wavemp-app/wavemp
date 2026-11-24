using System.Text;

namespace WaveMP.Core.Organizing.Files;

/// <summary>
/// Compares file and folder names the way the strictest file system does: without regard to case (Windows, macOS,
/// SMB shares) and to the Unicode normalization (macOS stores decomposed names). Two names with the same key are the
/// same name somewhere, so WaveMP never creates both.
/// </summary>
public static class FolderNames
{
    /// <summary>
    /// The key of a name or relative path: NFC, upper case (invariant).
    /// </summary>
    public static string Key(string name)
    {
        return name.Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    public static bool AreSame(string left, string right)
    {
        return string.Equals(Key(left), Key(right), StringComparison.Ordinal);
    }

    /// <summary>
    /// The name of a file without its last extension.
    /// </summary>
    public static string BaseNameOf(string fileName)
    {
        int dot = fileName.LastIndexOf('.');
        return dot <= 0 ? fileName : fileName[..dot];
    }

    /// <summary>
    /// The last extension of a file name without the dot, as written; <c>null</c> without one.
    /// </summary>
    public static string? ExtensionOf(string fileName)
    {
        int dot = fileName.LastIndexOf('.');
        return dot <= 0 || dot == fileName.Length - 1 ? null : fileName[(dot + 1)..];
    }

    /// <summary>
    /// The folder of a relative path with <c>/</c>; empty for the folder of the source.
    /// </summary>
    public static string DirectoryOf(string relativePath)
    {
        int slash = relativePath.LastIndexOf('/');
        return slash < 0 ? string.Empty : relativePath[..slash];
    }

    /// <summary>
    /// The name of the file of a relative path with <c>/</c>.
    /// </summary>
    public static string FileNameOf(string relativePath)
    {
        return relativePath[(relativePath.LastIndexOf('/') + 1)..];
    }

    /// <summary>
    /// A relative path of a folder and a name; the folder may be empty.
    /// </summary>
    public static string Combine(string directoryPath, string name)
    {
        return directoryPath.Length == 0 ? name : directoryPath + "/" + name;
    }
}
