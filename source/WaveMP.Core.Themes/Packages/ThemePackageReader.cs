using System.IO.Compression;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Packages;

/// <summary>
/// Unpacks a theme package (a ZIP file) into memory, never onto the disk, and checks its structure: the number of
/// entries, the size of every file and of all files together (counted while unpacking, so a ZIP file that claims
/// smaller sizes than it holds cannot get past the limits), the paths and the file types. A package whose files all
/// lie in one folder (as when a folder is zipped) is read as if that folder were the root.
/// </summary>
internal static class ThemePackageReader
{
    private const int BufferSize = 81_920;

    /// <summary>
    /// Whether the content starts like a ZIP file (a local file header or, for an empty archive, the end record).
    /// </summary>
    public static bool LooksLikeZip(ReadOnlySpan<byte> header)
    {
        return header.StartsWith("PK\u0003\u0004"u8) || header.StartsWith("PK\u0005\u0006"u8);
    }

    public static ThemePackage Read(Stream content, ThemeOptions options)
    {
        List<ThemeError> errors = [];
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        try
        {
            using ZipArchive archive = new(content, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > options.MaxEntries)
            {
                errors.Add(new ThemeError(ThemeErrorCode.PackageTooManyEntries, Max: options.MaxEntries));
                return Result(files, errors);
            }

            List<ZipArchiveEntry> entries = [.. archive.Entries.Where(entry => !PackagePaths.IsIgnored(entry.FullName))];
            string prefix = CommonRootFolder(entries);
            long unpacked = 0;
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in entries)
            {
                string path = entry.FullName[prefix.Length..];
                if (!PackagePaths.IsValid(path))
                {
                    errors.Add(new ThemeError(ThemeErrorCode.InvalidPath, Path: Shorten(entry.FullName)));
                    continue;
                }

                if (!seen.Add(path))
                {
                    errors.Add(new ThemeError(ThemeErrorCode.DuplicatePath, Path: path));
                    continue;
                }

                PackageFileKind kind = PackagePaths.KindOf(path);
                if (kind == PackageFileKind.Unsupported)
                {
                    errors.Add(new ThemeError(ThemeErrorCode.UnsupportedFile, Path: path));
                    continue;
                }

                if (kind == PackageFileKind.Document)
                {
                    continue;
                }

                if (entry.IsEncrypted)
                {
                    errors.Add(new ThemeError(ThemeErrorCode.PackageCorrupt, Path: path));
                    continue;
                }

                long limit = kind switch
                {
                    PackageFileKind.Stylesheet => options.MaxStylesheetBytes,
                    PackageFileKind.Font => options.MaxFontBytes,
                    _ => options.MaxFileBytes,
                };
                byte[]? data = ReadEntry(entry, limit);
                if (data is null)
                {
                    errors.Add(new ThemeError(ThemeErrorCode.FileTooLarge, Path: path, Max: limit));
                    continue;
                }

                unpacked += data.Length;
                if (unpacked > options.MaxUnpackedBytes)
                {
                    errors.Add(new ThemeError(ThemeErrorCode.PackageTooLarge, Max: options.MaxUnpackedBytes));
                    return Result(files, errors);
                }

                files[path] = data;
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException)
        {
            errors.Add(new ThemeError(ThemeErrorCode.PackageCorrupt));
        }

        return Result(files, errors);
    }

    private static ThemePackage Result(Dictionary<string, byte[]> files, List<ThemeError> errors)
    {
        return new ThemePackage { Files = files, Errors = errors };
    }

    /// <summary>
    /// The folder all entries lie in (with its slash), if there is no manifest at the root but one in that folder;
    /// otherwise empty.
    /// </summary>
    private static string CommonRootFolder(IReadOnlyList<ZipArchiveEntry> entries)
    {
        if (entries.Count == 0 || entries.Any(entry => entry.FullName == PackagePaths.ManifestFileName))
        {
            return string.Empty;
        }

        int slash = entries[0].FullName.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0)
        {
            return string.Empty;
        }

        string prefix = entries[0].FullName[..(slash + 1)];
        bool shared = entries.All(entry => entry.FullName.StartsWith(prefix, StringComparison.Ordinal));
        return shared && entries.Any(entry => entry.FullName == prefix + PackagePaths.ManifestFileName)
            ? prefix
            : string.Empty;
    }

    /// <summary>
    /// The unpacked content of an entry, or <c>null</c> as soon as it is longer than <paramref name="limit"/>.
    /// </summary>
    private static byte[]? ReadEntry(ZipArchiveEntry entry, long limit)
    {
        if (entry.Length > limit)
        {
            return null;
        }

        using Stream stream = entry.Open();
        using MemoryStream result = new();
        byte[] buffer = new byte[BufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (result.Length + read > limit)
            {
                return null;
            }

            result.Write(buffer, 0, read);
        }

        return result.ToArray();
    }

    private static string Shorten(string value)
    {
        const int MaxShown = PackagePaths.MaxLength;
        string printable = new([.. value.Select(character => char.IsControl(character) ? '?' : character)]);
        return printable.Length <= MaxShown ? printable : printable[..MaxShown];
    }
}
