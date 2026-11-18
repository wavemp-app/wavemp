using System.IO.Compression;
using System.Reflection;

namespace WaveMP.Core.Themes.Templates;

/// <summary>
/// The template of a theme package: an example theme with a commented CSS file, its manifest and its pictures
/// (<c>assets/theme-template</c>, embedded into this assembly). Theme authors download it as ZIP file, change it and
/// upload it again; it passes the checks of an upload as it is.
/// </summary>
public static class ThemeTemplate
{
    /// <summary>
    /// Name of the ZIP file for downloads.
    /// </summary>
    public const string FileName = "wavemp-theme-template.zip";

    private const string ResourcePrefix = "theme-template/";

    // A fixed time for every entry, so the same template always gives the same ZIP file.
    private static readonly DateTimeOffset _entryTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<byte[]> _zip = new(CreateZip);

    /// <summary>
    /// The paths of the files of the template, as they lie in the package.
    /// </summary>
    public static IReadOnlyList<string> Paths { get; } = [.. typeof(ThemeTemplate).Assembly
        .GetManifestResourceNames()
        .Select(name => name.Replace('\\', '/'))
        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        .Select(name => name[ResourcePrefix.Length..])
        .Order(StringComparer.Ordinal)];

    /// <summary>
    /// The template as ZIP file.
    /// </summary>
    public static byte[] Zip => _zip.Value;

    private static byte[] CreateZip()
    {
        Assembly assembly = typeof(ThemeTemplate).Assembly;
        Dictionary<string, string> resources = assembly.GetManifestResourceNames()
            .ToDictionary(name => name.Replace('\\', '/'), name => name, StringComparer.Ordinal);
        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string path in Paths)
            {
                ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                entry.LastWriteTime = _entryTime;
                using Stream source = assembly.GetManifestResourceStream(resources[ResourcePrefix + path])!;
                using Stream target = entry.Open();
                source.CopyTo(target);
            }
        }

        return output.ToArray();
    }
}
