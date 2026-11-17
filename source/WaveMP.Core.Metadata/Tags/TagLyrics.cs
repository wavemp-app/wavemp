namespace WaveMP.Core.Metadata.Tags;

/// <summary>
/// The lyrics in the tags of a file, as stored: synchronized lyrics (e.g. the ID3 frame SYLT) and unsynchronized
/// lyrics (e.g. the ID3 frame USLT or the Vorbis comment LYRICS), which may contain LRC timestamps as well.
/// </summary>
/// <param name="Synchronized">The entries of the synchronized lyrics in the order of the tag; empty if there are
/// none.</param>
/// <param name="Unsynchronized">The unsynchronized lyrics, or <c>null</c> if there are none.</param>
public sealed record TagLyrics(IReadOnlyList<TimedLyricsText> Synchronized, string? Unsynchronized);
