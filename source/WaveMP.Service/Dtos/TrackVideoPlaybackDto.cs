namespace WaveMP.Service.Dtos;

/// <summary>
/// How the client plays the video of a track, and the signed URL of its stream.
/// </summary>
public sealed class TrackVideoPlaybackDto
{
    /// <summary>
    /// Id of the track.
    /// </summary>
    public required Guid TrackId { get; init; }

    /// <summary>
    /// Direct play, remux or transcoding.
    /// </summary>
    public required PlaybackMethodDto Method { get; init; }

    /// <summary>
    /// A file (direct play and remux) or an HLS stream (transcoding).
    /// </summary>
    public required StreamProtocolDto Protocol { get; init; }

    /// <summary>
    /// Why the video has to be remuxed or transcoded; null for direct play.
    /// </summary>
    public TranscodeReasonDto? TranscodeReason { get; init; }

    /// <summary>
    /// Container of the file; null if it is none of the containers clients play directly.
    /// </summary>
    public VideoContainerDto? Container { get; init; }

    /// <summary>
    /// Codec of the picture; null if it is none of the codecs clients play directly.
    /// </summary>
    public VideoCodecDto? VideoCodec { get; init; }

    /// <summary>
    /// Codec of the sound; null without sound or if it is none of the codecs clients play directly.
    /// </summary>
    public AudioCodecDto? AudioCodec { get; init; }

    /// <summary>
    /// Whether the video has sound.
    /// </summary>
    public required bool HasAudio { get; init; }

    /// <summary>
    /// Width of the picture of the file in pixels, if known.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Height of the picture of the file in pixels, if known.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// MIME type the stream is delivered with; null without stream.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Signed, short-lived URL of the stream (path with query, relative to the server), for players that cannot send
    /// the session token: the file for direct play, the remuxed MP4 file for a remux, the HLS master playlist for
    /// transcoding. Null if the video has to be transcoded and transcoding is unavailable. It stays valid until
    /// expiresAt and while the session is active.
    /// </summary>
    public string? StreamUrl { get; init; }

    /// <summary>
    /// When the stream URL expires; null without stream.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Duration of the video in milliseconds, if known.
    /// </summary>
    public long? DurationMilliseconds { get; init; }

    /// <summary>
    /// Start of the song in the video in milliseconds while clients use its song segment, otherwise 0: position of
    /// the video = position of the audio + offset as long as the video cut nothing out. Kept for older clients;
    /// clients map positions with songSegment instead.
    /// </summary>
    public required int OffsetMilliseconds { get; init; }

    /// <summary>
    /// Signed URL of the cover of the track (path with query, relative to the server), which a casting device shows
    /// next to the music; only for the target Cast, otherwise null. It is valid as long as the stream URL; for a track
    /// without a cover it answers 404.
    /// </summary>
    public string? CoverUrl { get; init; }

    /// <summary>
    /// Where the real song lies in the video. Clients that play the audio file and show the video muted map the
    /// position of the song to the video with it (and keep the video still where it cut a part of the song out).
    /// </summary>
    public required SongSegmentDto SongSegment { get; init; }

    /// <summary>
    /// Integrated loudness of the sound of the video in LUFS (EBU R 128), if measured.
    /// </summary>
    public double? LoudnessLufs { get; init; }

    /// <summary>
    /// For a video with sound of a track that also has an audio file: the gain in dB (at most 12 either way) that
    /// brings the sound of the video to the loudness of the audio file. Clients that play the video with its sound
    /// (e.g. on a casting device) play it at the level of the audio file with it. Null without an audio file or sound
    /// and while a loudness is unknown.
    /// </summary>
    public double? LoudnessMatchDb { get; init; }
}
