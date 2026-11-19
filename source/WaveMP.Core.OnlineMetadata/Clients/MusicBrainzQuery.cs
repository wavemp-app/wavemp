using System.Globalization;
using System.Text;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The search queries of MusicBrainz (Lucene syntax). The title (and the artist, if known) must match; album, duration
/// and number of tracks only raise the relevance, because they often differ slightly.
/// </summary>
public static class MusicBrainzQuery
{
    /// <summary>
    /// How far the duration of a recording may differ to raise its relevance.
    /// </summary>
    public static readonly TimeSpan DurationTolerance = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The query of the recording search for a song.
    /// </summary>
    public static string Recording(string title, string? artist, string? album, TimeSpan? duration)
    {
        StringBuilder query = new();
        query.Append("recording:").Append(Phrase(title));
        if (!string.IsNullOrWhiteSpace(artist))
        {
            query.Append(" AND artist:").Append(Phrase(artist));
        }

        List<string> optional = [];
        if (!string.IsNullOrWhiteSpace(album))
        {
            optional.Add("release:" + Phrase(album));
        }

        if (duration is { } length && length > TimeSpan.Zero)
        {
            long from = (long)Math.Max(0, (length - DurationTolerance).TotalMilliseconds);
            long to = (long)(length + DurationTolerance).TotalMilliseconds;
            optional.Add(string.Create(CultureInfo.InvariantCulture, $"dur:[{from} TO {to}]"));
        }

        AppendOptional(query, optional, "recording:" + Phrase(title));
        return query.ToString();
    }

    /// <summary>
    /// The query of the release search for an album.
    /// </summary>
    public static string Release(string album, string? artist, int? trackCount)
    {
        StringBuilder query = new();
        query.Append("release:").Append(Phrase(album));
        if (!string.IsNullOrWhiteSpace(artist))
        {
            query.Append(" AND artist:").Append(Phrase(artist));
        }

        List<string> optional = [];
        if (trackCount is > 0)
        {
            optional.Add(string.Create(CultureInfo.InvariantCulture, $"tracks:{trackCount}"));
        }

        AppendOptional(query, optional, "release:" + Phrase(album));
        return query.ToString();
    }

    /// <summary>
    /// A text as a phrase: in quotes, with quotes and backslashes escaped.
    /// </summary>
    public static string Phrase(string text)
    {
        string escaped = text.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + escaped + "\"";
    }

    /// <summary>
    /// Clauses that only raise the relevance: together with a clause that always matches in one OR group, so the
    /// group never excludes a result, whatever the default operator of the search is.
    /// </summary>
    private static void AppendOptional(StringBuilder query, IReadOnlyList<string> optional, string alwaysMatching)
    {
        if (optional.Count == 0)
        {
            return;
        }

        query.Append(" AND (").AppendJoin(" OR ", optional).Append(" OR ").Append(alwaysMatching).Append(')');
    }
}
