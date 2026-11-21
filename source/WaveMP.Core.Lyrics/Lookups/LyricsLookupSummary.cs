namespace WaveMP.Core.Lyrics.Lookups;

/// <summary>
/// The stored lookups that found no lyrics.
/// </summary>
/// <param name="NotFound">Tracks the lyrics service had no lyrics for.</param>
/// <param name="Instrumental">Tracks the lyrics service lists as instrumental.</param>
/// <param name="LookUpAgainAfter">How long the download job waits before it looks such a track up again;
/// <c>null</c> if it never does.</param>
public sealed record LyricsLookupSummary(int NotFound, int Instrumental, TimeSpan? LookUpAgainAfter);
