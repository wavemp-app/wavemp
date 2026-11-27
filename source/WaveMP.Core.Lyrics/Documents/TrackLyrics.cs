namespace WaveMP.Core.Lyrics.Documents;

/// <summary>
/// The lyrics of a track and where they come from.
/// </summary>
/// <param name="Document">The lyrics.</param>
/// <param name="Source">Where they come from.</param>
/// <param name="OtherVersion">Whether they belong to another version of the song (another duration), so they are
/// shown without times, which would not fit the track.</param>
public sealed record TrackLyrics(LyricsDocument Document, LyricsSource Source, bool OtherVersion = false);
