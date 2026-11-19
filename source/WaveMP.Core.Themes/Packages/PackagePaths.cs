using System.Text.RegularExpressions;

namespace WaveMP.Core.Themes.Packages;

/// <summary>
/// Rules for the paths of the files of a theme package. A path is relative to the root of the package, uses
/// <c>/</c> between its segments, and every segment consists of letters, digits, <c>.</c>, <c>_</c> and <c>-</c>
/// without starting with a dot. So no path can be absolute, leave the package (<c>..</c>), hide a file, or mean
/// something else on another file system (backslashes, drive letters, colons, percent signs, control characters).
/// </summary>
internal static partial class PackagePaths
{
    /// <summary>
    /// Most segments of a path (four folders and the file).
    /// </summary>
    public const int MaxSegments = 5;

    /// <summary>
    /// Longest segment.
    /// </summary>
    public const int MaxSegmentLength = 64;

    /// <summary>
    /// Longest path.
    /// </summary>
    public const int MaxLength = 200;

    public const string ManifestFileName = "manifest.json";

    // Files that ZIP tools and file managers add on their own; they are ignored, not rejected.
    private static readonly HashSet<string> _systemFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store",
        "Thumbs.db",
        "desktop.ini",
    };

    private const string MacResourceFolder = "__MACOSX";

    // Text files about the theme (license, read-me); they are allowed but not installed.
    private static readonly HashSet<string> _documentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LICENSE",
        "LICENCE",
        "NOTICE",
        "README",
        "COPYING",
        "AUTHORS",
    };

    private static readonly HashSet<string> _documentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".txt",
    };

    /// <summary>
    /// Whether an entry is a folder or a file that tools add on their own (macOS resource forks, thumbnail caches).
    /// </summary>
    public static bool IsIgnored(string entryName)
    {
        if (entryName.EndsWith('/') || entryName.EndsWith('\\'))
        {
            return true;
        }

        string[] segments = entryName.Split('/', '\\');
        return segments[0] == MacResourceFolder || _systemFileNames.Contains(segments[^1]);
    }

    /// <summary>
    /// Whether a path is valid (see the class).
    /// </summary>
    public static bool IsValid(string path)
    {
        if (path.Length is 0 or > MaxLength)
        {
            return false;
        }

        string[] segments = path.Split('/');
        return segments.Length <= MaxSegments && segments.All(IsValidSegment);
    }

    /// <summary>
    /// The kind of a file by its path.
    /// </summary>
    public static PackageFileKind KindOf(string path)
    {
        if (path == ManifestFileName)
        {
            return PackageFileKind.Manifest;
        }

        string name = path[(path.LastIndexOf('/') + 1)..];
        string extension = Path.GetExtension(name);
        if (_documentNames.Contains(Path.GetFileNameWithoutExtension(name)) &&
            (extension.Length == 0 || _documentExtensions.Contains(extension)))
        {
            return PackageFileKind.Document;
        }

        return extension.ToLowerInvariant() switch
        {
            ".css" => PackageFileKind.Stylesheet,
            ".svg" => PackageFileKind.Svg,
            ".png" => PackageFileKind.Png,
            ".webp" => PackageFileKind.WebP,
            ".woff2" => PackageFileKind.Font,
            ".md" or ".txt" => PackageFileKind.Document,
            _ => PackageFileKind.Unsupported,
        };
    }

    /// <summary>
    /// Resolves a relative address of a file of the package (e.g. <c>images/glass.svg</c> or
    /// <c>../images/glass.svg</c>) against the folder of <paramref name="from"/>. Returns <c>null</c> if the address
    /// leaves the package or a segment is no valid segment.
    /// </summary>
    public static string? Resolve(string from, string relative)
    {
        List<string> segments = [.. from.Split('/').SkipLast(1)];
        foreach (string segment in relative.Split('/'))
        {
            switch (segment)
            {
                case ".":
                    continue;
                case "..":
                    if (segments.Count == 0)
                    {
                        return null;
                    }

                    segments.RemoveAt(segments.Count - 1);
                    continue;
                default:
                    if (!IsValidSegment(segment))
                    {
                        return null;
                    }

                    segments.Add(segment);
                    continue;
            }
        }

        string path = string.Join('/', segments);
        return IsValid(path) ? path : null;
    }

    private static bool IsValidSegment(string segment)
    {
        return segment.Length <= MaxSegmentLength && SegmentPattern().IsMatch(segment);
    }

    [GeneratedRegex(@"\A[A-Za-z0-9_-][A-Za-z0-9._-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}
