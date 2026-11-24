using System.Text;
using WaveMP.Core.Scanning.Qualities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Groupings;

/// <summary>
/// Finds the pictures of artists in their folders: pictures named after the artist picture names of the settings (e.g.
/// <c>artist.jpg</c>) directly in the folder of the artist. The folder of an artist is, for every folder with its
/// tracks (as track artist or as album artist of their album), the topmost folder on the way from the source down to
/// it whose name matches the artist (<c>Weezer/Weezer/Album</c>: the outer one). Names match when their letters and
/// digits are the same, without regard to case (<c>AC/DC</c> matches the folders <c>AC_DC</c> and <c>ACDC</c>); a name
/// without letters and digits matches nothing. Folders not named after the artist, such as <c>Compilations</c> or the
/// folder of an album, are never used. The best picture comes first: by the order of the names, then by quality, then
/// by source and path, so the choice is always the same.
/// </summary>
public static class ArtistPictureCandidates
{
    /// <summary>
    /// The pictures per artist, best first; artists without a picture are left out.
    /// </summary>
    public static IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> Select(
        IReadOnlyList<ArtistFolder> artistFolders,
        IReadOnlyList<ArtistPictureImage> images,
        IReadOnlyList<string> pictureNames)
    {
        var priority = pictureNames
            .Select((name, index) => (Key: FileGrouper.NormalizeBaseName(name), Index: index))
            .DistinctBy(entry => entry.Key)
            .ToDictionary(entry => entry.Key, entry => entry.Index, StringComparer.Ordinal);
        ILookup<(Guid SourceId, string DirectoryPath), ArtistPictureImage> imagesByDirectory = images
            .Where(image => priority.ContainsKey(image.NormalizedBaseName))
            .ToLookup(image => (image.SourceId, image.DirectoryPath));
        if (imagesByDirectory.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ArtistPictureImage>>();
        }

        Dictionary<Guid, IReadOnlyList<ArtistPictureImage>> pictures = [];
        foreach (IGrouping<Guid, ArtistFolder> artist in artistFolders.GroupBy(folder => folder.ArtistId))
        {
            HashSet<(Guid SourceId, string DirectoryPath)> directories = [];
            foreach (ArtistFolder folder in artist)
            {
                string key = NameKey(folder.ArtistName);
                if (key.Length > 0 && TopmostFolderNamed(folder.DirectoryPath, key) is { } directory)
                {
                    directories.Add((folder.SourceId, directory));
                }
            }

            List<ArtistPictureImage> candidates =
                [.. directories.SelectMany(directory => imagesByDirectory[directory])];
            if (candidates.Count == 0)
            {
                continue;
            }

            candidates.Sort((x, y) =>
            {
                int result = priority[x.NormalizedBaseName].CompareTo(priority[y.NormalizedBaseName]);
                result = result != 0
                    ? result
                    : ImageQuality.Compare(
                        (true, x.Width, x.Height, x.Size, string.Empty),
                        (true, y.Width, y.Height, y.Size, string.Empty));
                result = result != 0 ? result : x.SourceId.CompareTo(y.SourceId);
                return result != 0 ? result : string.CompareOrdinal(x.RelativePath, y.RelativePath);
            });
            pictures[artist.Key] = candidates;
        }

        return pictures;
    }

    /// <summary>
    /// The key two names match by: the letters and digits of the name in upper case, everything else left out.
    /// </summary>
    public static string NameKey(string name)
    {
        StringBuilder key = new(name.Length);
        foreach (char character in name.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(character))
            {
                key.Append(char.ToUpperInvariant(character));
            }
        }

        return key.ToString();
    }

    /// <summary>
    /// The topmost folder of <paramref name="directoryPath"/> (itself or a folder above it, relative to the source and
    /// separated by <c>/</c>) whose name has the key <paramref name="key"/>, otherwise <c>null</c>.
    /// </summary>
    private static string? TopmostFolderNamed(string directoryPath, string key)
    {
        // The root of the source has no name.
        if (directoryPath.Length == 0)
        {
            return null;
        }

        int start = 0;
        while (true)
        {
            int slash = directoryPath.IndexOf('/', start);
            int end = slash < 0 ? directoryPath.Length : slash;
            if (NameKey(directoryPath[start..end]) == key)
            {
                return directoryPath[..end];
            }

            if (slash < 0)
            {
                return null;
            }

            start = slash + 1;
        }
    }
}
