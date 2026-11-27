namespace WaveMP.Core.Lyrics.Searches;

/// <summary>
/// A track of LRCLIB with lyrics that a search found.
/// </summary>
/// <param name="Id">Id of the track at LRCLIB.</param>
/// <param name="Title">Its title.</param>
/// <param name="Artist">Its artist, if LRCLIB names one.</param>
/// <param name="Album">Its album, if LRCLIB names one.</param>
/// <param name="Duration">Its duration, if known.</param>
/// <param name="Synchronized">Whether it has synchronized lyrics.</param>
/// <param name="FitsDuration">Whether its duration fits the track (see
/// <see cref="Downloads.LrclibMatcher.DurationTolerance"/>); lyrics of a track that does not fit are kept without
/// their times.</param>
public sealed record LyricsMatch(
    long Id,
    string Title,
    string? Artist,
    string? Album,
    TimeSpan? Duration,
    bool Synchronized,
    bool FitsDuration);
