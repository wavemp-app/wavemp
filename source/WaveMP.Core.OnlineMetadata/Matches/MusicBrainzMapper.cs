using System.Globalization;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Core.OnlineMetadata.Clients;

namespace WaveMP.Core.OnlineMetadata.Matches;

/// <summary>
/// Turns the answers of MusicBrainz into the tags they suggest.
/// </summary>
public static class MusicBrainzMapper
{
    /// <summary>
    /// The suggestions of a recording: one per release (on its first listed medium and track), or one without album
    /// if the recording has no release.
    /// </summary>
    public static IReadOnlyList<(OnlineRelease? Release, OnlineTrackValues Values, TimeSpan? Duration)> ForRecording(
        MusicBrainzRecording recording)
    {
        string? artist = Credit(recording.ArtistCredit);
        TimeSpan? recordingLength = Length(recording.Length);
        if (recording.Releases.Count == 0)
        {
            return [(
                null,
                new OnlineTrackValues(
                    MetadataText.Normalize(recording.Title),
                    artist,
                    null,
                    null,
                    Year(recording.FirstReleaseDate),
                    null,
                    null,
                    Genre(recording.Tags)),
                recordingLength)];
        }

        return [.. recording.Releases.Select(release =>
        {
            MusicBrainzMedium? medium = release.Media.FirstOrDefault();
            MusicBrainzTrack? track = medium?.Tracks.FirstOrDefault();
            OnlineTrackValues values = new(
                MetadataText.Normalize(track?.Title) ?? MetadataText.Normalize(recording.Title),
                artist,
                MetadataText.Normalize(release.Title),
                Credit(release.ArtistCredit) ?? artist,
                Year(release.Date) ?? Year(recording.FirstReleaseDate),
                TrackNumber(track?.Number) ?? (medium?.TrackOffset is { } offset ? offset + 1 : null),
                medium?.Position is > 0 ? medium.Position : null,
                Genre(recording.Tags) ?? Genre(release.Tags));
            return ((OnlineRelease?)ToRelease(release), values, Length(track?.Length) ?? recordingLength);
        })];
    }

    /// <summary>
    /// The suggestion of a release for an album.
    /// </summary>
    public static (OnlineRelease Release, OnlineAlbumValues Values) ForRelease(MusicBrainzRelease release)
    {
        return (
            ToRelease(release),
            new OnlineAlbumValues(
                MetadataText.Normalize(release.Title),
                Credit(release.ArtistCredit),
                Year(release.Date),
                Genre(release.Tags)));
    }

    /// <summary>
    /// The artists as credited, joined by their phrases (e.g. "Artist A &amp; Artist B"); <c>null</c> without artists.
    /// </summary>
    public static string? Credit(IReadOnlyList<MusicBrainzArtistCredit>? credits)
    {
        if (credits is null || credits.Count == 0)
        {
            return null;
        }

        return MetadataText.Normalize(string.Concat(credits.Select(credit => credit.Name + credit.JoinPhrase)));
    }

    /// <summary>
    /// The year of a date of MusicBrainz (<c>YYYY</c>, <c>YYYY-MM</c> or <c>YYYY-MM-DD</c>).
    /// </summary>
    public static int? Year(string? date)
    {
        return date is { Length: >= 4 }
            && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out int year)
            && year > 0
            ? year
            : null;
    }

    /// <summary>
    /// The tag with the most votes as genre, its words capitalized (e.g. "hip hop" becomes "Hip Hop").
    /// </summary>
    public static string? Genre(IReadOnlyList<MusicBrainzTag> tags)
    {
        string? name = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Name) && tag.Count > 0)
            .OrderByDescending(tag => tag.Count)
            .ThenBy(tag => tag.Name, StringComparer.Ordinal)
            .Select(tag => tag.Name)
            .FirstOrDefault();
        return name is null
            ? null
            : MetadataText.Normalize(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant()));
    }

    private static int? TrackNumber(string? number)
    {
        return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value
            : null;
    }

    private static TimeSpan? Length(long? milliseconds)
    {
        return milliseconds is > 0 ? TimeSpan.FromMilliseconds(milliseconds.Value) : null;
    }

    private static OnlineRelease ToRelease(MusicBrainzRelease release)
    {
        return new OnlineRelease(
            release.Id,
            release.Date is { Length: > 0 } ? release.Date : null,
            release.Country is { Length: > 0 } ? release.Country : null,
            release.Media.Select(medium => medium.Format).FirstOrDefault(format => !string.IsNullOrWhiteSpace(format)),
            release.TrackCount);
    }
}
