using System.Diagnostics.CodeAnalysis;
using WaveMP.Core.Lyrics.Documents;

namespace WaveMP.Core.Lyrics.Services;

/// <summary>
/// The lyrics of a track, or why there are none.
/// </summary>
public sealed record LyricsResult(LyricsStatus Status, TrackLyrics? Lyrics = null)
{
    [MemberNotNullWhen(true, nameof(Lyrics))]
    public bool IsFound => Status == LyricsStatus.Found && Lyrics is not null;

    public static LyricsResult TrackNotFound { get; } = new(LyricsStatus.TrackNotFound);

    public static LyricsResult NoLyrics { get; } = new(LyricsStatus.NoLyrics);

    public static LyricsResult Found(TrackLyrics lyrics)
    {
        return new LyricsResult(LyricsStatus.Found, lyrics);
    }
}
