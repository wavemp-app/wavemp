using System.Globalization;
using System.Text;

namespace WaveMP.Core.OnlineMetadata.Matches;

/// <summary>
/// How well a result of MusicBrainz agrees with a song or album of the library, from 0 (nothing in common) to 100 (all
/// known values agree). Only values the library knows count: for songs title, artist, album and duration, for albums
/// title, artist and number of tracks, each with its weight.
/// </summary>
public static class MatchScorer
{
    internal const double TitleWeight = 40;
    internal const double ArtistWeight = 25;
    internal const double AlbumWeight = 15;
    internal const double DurationWeight = 20;

    internal const double AlbumTitleWeight = 50;
    internal const double AlbumArtistWeight = 30;
    internal const double TrackCountWeight = 20;

    /// <summary>
    /// Durations that differ by up to this much agree completely.
    /// </summary>
    internal static readonly TimeSpan DurationExact = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Durations that differ by this much or more do not agree at all.
    /// </summary>
    internal static readonly TimeSpan DurationNone = TimeSpan.FromSeconds(15);

    public static int ForTrack(TrackLookup song, OnlineTrackValues values, TimeSpan? duration)
    {
        List<(double Weight, double Value)> parts = [(TitleWeight, TextSimilarity(song.Title, values.Title))];
        if (!string.IsNullOrWhiteSpace(song.Artist))
        {
            parts.Add((ArtistWeight, TextSimilarity(song.Artist, values.Artist)));
        }

        if (!string.IsNullOrWhiteSpace(song.Album))
        {
            parts.Add((AlbumWeight, TextSimilarity(song.Album, values.Album)));
        }

        if (song.Duration is { } local && local > TimeSpan.Zero)
        {
            parts.Add((DurationWeight, duration is { } remote ? DurationSimilarity(local, remote) : 0));
        }

        return Combine(parts);
    }

    public static int ForAlbum(AlbumLookup album, OnlineAlbumValues values, int? trackCount)
    {
        List<(double Weight, double Value)> parts = [(AlbumTitleWeight, TextSimilarity(album.Album, values.Album))];
        if (!string.IsNullOrWhiteSpace(album.Artist))
        {
            parts.Add((AlbumArtistWeight, TextSimilarity(album.Artist, values.AlbumArtist)));
        }

        if (album.TrackCount > 0)
        {
            parts.Add((TrackCountWeight, trackCount is > 0 ? CountSimilarity(album.TrackCount, trackCount.Value) : 0));
        }

        return Combine(parts);
    }

    /// <summary>
    /// Similarity of two texts from 0 to 1, without regard to case, accents, punctuation and "&amp;" or "and": 1 for
    /// the same words, otherwise by the edit distance of the letters.
    /// </summary>
    public static double TextSimilarity(string? left, string? right)
    {
        string a = Simplify(left);
        string b = Simplify(right);
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        if (a == b)
        {
            return 1;
        }

        int distance = Distance(a, b);
        return Math.Max(0, 1 - ((double)distance / Math.Max(a.Length, b.Length)));
    }

    /// <summary>
    /// Similarity of two durations from 0 to 1: 1 up to <see cref="DurationExact"/> of difference, 0 from
    /// <see cref="DurationNone"/>, linear in between.
    /// </summary>
    public static double DurationSimilarity(TimeSpan left, TimeSpan right)
    {
        TimeSpan difference = (left - right).Duration();
        if (difference <= DurationExact)
        {
            return 1;
        }

        if (difference >= DurationNone)
        {
            return 0;
        }

        return 1 - ((difference - DurationExact) / (DurationNone - DurationExact));
    }

    /// <summary>
    /// The text as it is compared: lower case, without accents, "&amp;" as "and", only letters and digits separated by
    /// single spaces.
    /// </summary>
    internal static string Simplify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new(text.Length);
        bool space = false;
        foreach (char character in text.Replace("&", " and ", StringComparison.Ordinal).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (space && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToLowerInvariant(character));
                space = false;
            }
            else
            {
                space = true;
            }
        }

        return builder.ToString();
    }

    private static double CountSimilarity(int left, int right)
    {
        return 1 - ((double)Math.Abs(left - right) / Math.Max(left, right));
    }

    private static int Combine(IReadOnlyList<(double Weight, double Value)> parts)
    {
        double weights = parts.Sum(part => part.Weight);
        return weights <= 0
            ? 0
            : (int)Math.Round(100 * parts.Sum(part => part.Weight * part.Value) / weights, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The Levenshtein distance of two texts.
    /// </summary>
    private static int Distance(string a, string b)
    {
        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
