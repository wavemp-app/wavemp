namespace WaveMP.Core.Lyrics.Searches;

/// <summary>
/// The result of a search for lyrics, or why there is none.
/// </summary>
/// <param name="Status">Whether the search ran.</param>
/// <param name="Title">The title that was searched for.</param>
/// <param name="Artist">The artist that was searched for, or <c>null</c>.</param>
/// <param name="Matches">The tracks of LRCLIB with lyrics, the best first.</param>
/// <param name="RetryAfter">How long LRCLIB asks to wait, for <see cref="LyricsSearchStatus.RateLimited"/>.</param>
public sealed record LyricsSearchResult(
    LyricsSearchStatus Status,
    string Title = "",
    string? Artist = null,
    IReadOnlyList<LyricsMatch>? Matches = null,
    TimeSpan? RetryAfter = null)
{
    public static LyricsSearchResult Disabled { get; } = new(LyricsSearchStatus.Disabled);

    public static LyricsSearchResult TrackNotFound { get; } = new(LyricsSearchStatus.TrackNotFound);

    public static LyricsSearchResult Unavailable { get; } = new(LyricsSearchStatus.Unavailable);
}
