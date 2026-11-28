using WaveMP.Data.Entities;
using WaveMP.Data.Segments;

namespace WaveMP.Data.Projections;

/// <summary>
/// A track with the files the song analysis looks at.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Audio">The primary audio file with its analysis; <c>null</c> without one.</param>
/// <param name="Video">The primary video with its analysis; <c>null</c> without one.</param>
/// <param name="AudioSongStartMilliseconds">Start of the song in the audio file as the admin corrected it.</param>
/// <param name="AudioSongEndMilliseconds">End of the song in the audio file as the admin corrected it.</param>
/// <param name="VideoSongStartMilliseconds">Start of the song in the video as the admin corrected it.</param>
/// <param name="VideoSongEndMilliseconds">End of the song in the video as the admin corrected it.</param>
public sealed record SongAnalysisTrackRow(
    Guid TrackId,
    MediaFile? Audio,
    MediaFile? Video,
    int? AudioSongStartMilliseconds = null,
    int? AudioSongEndMilliseconds = null,
    int? VideoSongStartMilliseconds = null,
    int? VideoSongEndMilliseconds = null)
{
    /// <summary>
    /// Where the song lies in the audio file as the players use it.
    /// </summary>
    public SongSegment AudioSegment => Audio is null
        ? SongSegment.WholeFile
        : SongSegment.Of(Audio.Analysis, AudioSongStartMilliseconds, AudioSongEndMilliseconds, Audio.Duration);

    /// <summary>
    /// Where the song lies in the video as the players use it.
    /// </summary>
    public SongSegment VideoSegment => Video is null
        ? SongSegment.WholeFile
        : SongSegment.Of(Video.Analysis, VideoSongStartMilliseconds, VideoSongEndMilliseconds, Video.Duration);
}
