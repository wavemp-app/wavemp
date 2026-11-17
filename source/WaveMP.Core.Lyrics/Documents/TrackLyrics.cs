namespace WaveMP.Core.Lyrics.Documents;

/// <summary>
/// The lyrics of a track and where they come from.
/// </summary>
public sealed record TrackLyrics(LyricsDocument Document, LyricsSource Source);
