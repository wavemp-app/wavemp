using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// Compares and combines the tag values an organize operation writes with the tags the library read. Texts are
/// compared in their normalized form, as the library stores them.
/// </summary>
public static class OrganizeTags
{
    /// <summary>
    /// The fields of <paramref name="desired"/> whose value differs from the tags of the file; an empty value if the
    /// file already has every value.
    /// </summary>
    public static OrganizeTagValues Difference(OrganizeTagValues desired, MediaFileTags? current)
    {
        OrganizeTagValues result = Copy(desired);
        result.Fields = [.. desired.Fields.Where(field => !Equals(field, desired, current))];
        return result;
    }

    /// <summary>
    /// Whether the file has every value of <paramref name="written"/>.
    /// </summary>
    public static bool Matches(OrganizeTagValues written, MediaFileTags? current)
    {
        return written.Fields.All(field => Equals(field, written, current));
    }

    /// <summary>
    /// The values of the fields of <paramref name="fields"/> as the file has them.
    /// </summary>
    public static OrganizeTagValues Snapshot(IEnumerable<OrganizeTagField> fields, MediaFileTags? current)
    {
        return new OrganizeTagValues
        {
            Fields = [.. fields],
            Album = current?.Album,
            AlbumArtists = current?.AlbumArtist is { } albumArtist ? [albumArtist] : [],
            TrackNumber = current?.TrackNumber,
            TrackCount = current?.TrackCount,
            DiscNumber = current?.DiscNumber,
            DiscCount = current?.DiscCount,
            Year = current?.Year,
            Genres = current?.Genres is { } genres ? [genres] : current?.Genre is { } genre ? [genre] : [],
        };
    }

    /// <summary>
    /// Whether the file still has the values of a <see cref="Snapshot"/> taken before (its fields only), e.g. the tags
    /// at the time of a preview.
    /// </summary>
    public static bool StillMatches(OrganizeTagValues snapshot, MediaFileTags? current)
    {
        OrganizeTagValues now = Snapshot(snapshot.Fields, current);
        return snapshot.Fields.All(field => field switch
        {
            OrganizeTagField.Album => SameText(snapshot.Album, now.Album),
            OrganizeTagField.AlbumArtist => snapshot.AlbumArtists.Select(MetadataText.Normalize)
                .SequenceEqual(now.AlbumArtists.Select(MetadataText.Normalize)),
            OrganizeTagField.TrackNumber => snapshot.TrackNumber == now.TrackNumber,
            OrganizeTagField.TrackCount => snapshot.TrackCount == now.TrackCount,
            OrganizeTagField.DiscNumber => snapshot.DiscNumber == now.DiscNumber,
            OrganizeTagField.Year => snapshot.Year == now.Year,
            OrganizeTagField.Genre => snapshot.Genres.Select(MetadataText.Normalize)
                .SequenceEqual(now.Genres.Select(MetadataText.Normalize)),
            _ => true,
        });
    }

    /// <summary>
    /// The values of the fields of <paramref name="fields"/> as the backup holds them.
    /// </summary>
    public static OrganizeTagValues FromBackup(IEnumerable<OrganizeTagField> fields, TagBackup backup)
    {
        return new OrganizeTagValues
        {
            Fields = [.. fields],
            Album = backup.Album,
            AlbumArtists = [.. backup.AlbumArtists],
            TrackNumber = backup.TrackNumber,
            TrackCount = backup.TrackCount,
            DiscNumber = backup.DiscNumber,
            DiscCount = backup.DiscCount,
            Year = backup.Year,
            Genres = [.. backup.Genres],
        };
    }

    /// <summary>
    /// The changes the tag writer writes for <paramref name="values"/>: every named field, album artists and genres
    /// as lists, the number of discs together with the disc number.
    /// </summary>
    public static TagChanges ToTagChanges(OrganizeTagValues values)
    {
        TagChanges changes = new();
        foreach (OrganizeTagField field in values.Fields.Distinct())
        {
            changes = field switch
            {
                OrganizeTagField.Album => changes with { Album = new TagChange<string?>(values.Album) },
                OrganizeTagField.AlbumArtist => changes with
                {
                    AlbumArtists = new TagChange<IReadOnlyList<string>>([.. values.AlbumArtists]),
                },
                OrganizeTagField.TrackNumber => changes with { TrackNumber = new TagChange<int?>(Positive(values.TrackNumber)) },
                OrganizeTagField.TrackCount => changes with { TrackCount = new TagChange<int?>(Positive(values.TrackCount)) },
                OrganizeTagField.DiscNumber => changes with
                {
                    DiscNumber = new TagChange<int?>(Positive(values.DiscNumber)),
                    DiscCount = new TagChange<int?>(Positive(values.DiscCount)),
                },
                OrganizeTagField.Year => changes with { Year = new TagChange<int?>(Positive(values.Year)) },
                OrganizeTagField.Genre => changes with
                {
                    Genres = new TagChange<IReadOnlyList<string>>([.. values.Genres]),
                },
                _ => changes,
            };
        }

        return changes;
    }

    /// <summary>
    /// The tags of a file after the changes were written.
    /// </summary>
    public static MediaTags Apply(MediaTags tags, OrganizeTagValues? changes)
    {
        if (changes is null || changes.IsEmpty)
        {
            return tags;
        }

        MediaTags result = tags;
        foreach (OrganizeTagField field in changes.Fields)
        {
            result = field switch
            {
                OrganizeTagField.Album => result with { Album = MetadataText.Normalize(changes.Album) },
                OrganizeTagField.AlbumArtist => result with
                {
                    AlbumArtist = MetadataText.Normalize(changes.AlbumArtists.FirstOrDefault()),
                },
                OrganizeTagField.TrackNumber => result with { TrackNumber = Positive(changes.TrackNumber) },
                OrganizeTagField.TrackCount => result with { TrackCount = Positive(changes.TrackCount) },
                OrganizeTagField.DiscNumber => result with { DiscNumber = Positive(changes.DiscNumber) },
                OrganizeTagField.Year => result with { Year = Positive(changes.Year) },
                OrganizeTagField.Genre => result with { Genre = MetadataText.Normalize(changes.Genres.FirstOrDefault()) },
                _ => result,
            };
        }

        return result;
    }

    private static bool Equals(OrganizeTagField field, OrganizeTagValues values, MediaFileTags? current)
    {
        return field switch
        {
            OrganizeTagField.Album => SameText(values.Album, current?.Album),
            OrganizeTagField.AlbumArtist => SameText(values.AlbumArtists.FirstOrDefault(), current?.AlbumArtist),
            OrganizeTagField.TrackNumber => Positive(values.TrackNumber) == current?.TrackNumber,
            OrganizeTagField.TrackCount => Positive(values.TrackCount) == current?.TrackCount,
            OrganizeTagField.DiscNumber => Positive(values.DiscNumber) == current?.DiscNumber,
            OrganizeTagField.Year => Positive(values.Year) == current?.Year,
            OrganizeTagField.Genre => SameText(values.Genres.FirstOrDefault(), current?.Genre),
            _ => false,
        };
    }

    private static bool SameText(string? left, string? right)
    {
        return string.Equals(MetadataText.Normalize(left), MetadataText.Normalize(right), StringComparison.Ordinal);
    }

    private static int? Positive(int? value)
    {
        return value is > 0 ? value : null;
    }

    private static OrganizeTagValues Copy(OrganizeTagValues values)
    {
        return new OrganizeTagValues
        {
            Fields = [.. values.Fields],
            Album = values.Album,
            AlbumArtists = [.. values.AlbumArtists],
            TrackNumber = values.TrackNumber,
            TrackCount = values.TrackCount,
            DiscNumber = values.DiscNumber,
            DiscCount = values.DiscCount,
            Year = values.Year,
            Genres = [.. values.Genres],
        };
    }
}
