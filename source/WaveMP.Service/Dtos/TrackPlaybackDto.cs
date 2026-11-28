namespace WaveMP.Service.Dtos;

/// <summary>
/// How the client plays the audio of a track, and the signed URL of its stream.
/// </summary>
public sealed class TrackPlaybackDto
{
    /// <summary>
    /// Id of the track.
    /// </summary>
    public required Guid TrackId { get; init; }

    /// <summary>
    /// Direct play or transcoding.
    /// </summary>
    public required PlaybackMethodDto Method { get; init; }

    /// <summary>
    /// The file as it is (direct play) or an HLS stream of the sound (transcoding).
    /// </summary>
    public required StreamProtocolDto Protocol { get; init; }

    /// <summary>
    /// Format of the audio file; null if it is none of the formats clients play directly.
    /// </summary>
    public AudioFormatDto? Format { get; init; }

    /// <summary>
    /// Why the track has to be transcoded; null for direct play.
    /// </summary>
    public TranscodeReasonDto? TranscodeReason { get; init; }

    /// <summary>
    /// MIME type the stream is delivered with; null without stream.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Signed, short-lived URL of the stream (path with query, relative to the server), for players that cannot send
    /// the session token: the file for direct play, the HLS master playlist for transcoding. Null if the track has to
    /// be transcoded and transcoding is unavailable. It stays valid until expiresAt and while the session is active.
    /// </summary>
    public string? StreamUrl { get; init; }

    /// <summary>
    /// When the stream URL expires; null without stream.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Duration of the file in milliseconds, if known.
    /// </summary>
    public long? DurationMilliseconds { get; init; }

    /// <summary>
    /// The ReplayGain tags of the audio file.
    /// </summary>
    public required ReplayGainDto ReplayGain { get; init; }

    /// <summary>
    /// Signed URL of the cover of the track (path with query, relative to the server), which a casting device shows
    /// next to the music; only for the target Cast, otherwise null. It is valid as long as the stream URL; for a track
    /// without a cover it answers 404.
    /// </summary>
    public string? CoverUrl { get; init; }

    /// <summary>
    /// Which recording the stream plays: the audio file or the sound of the video (see the parameter sound; Video
    /// also for tracks that only have a video).
    /// </summary>
    public required SoundSourceDto Sound { get; init; }

    /// <summary>
    /// Where the real song lies in the played recording. Clients skip intro and outro with it, map positions to the
    /// other recording when they switch between them and show synchronized lyrics relative to the song.
    /// </summary>
    public required SongSegmentDto SongSegment { get; init; }

    /// <summary>
    /// Integrated loudness of the played recording in LUFS (EBU R 128), if measured; clients play the audio file and
    /// the sound of the video at the same loudness with it.
    /// </summary>
    public double? LoudnessLufs { get; init; }

    /// <summary>
    /// For the sound of a video of a track that also has an audio file: the gain in dB (at most 12 either way) that
    /// brings it to the loudness of the audio file; its ReplayGain is that of the audio file. Clients add it to the
    /// gain of the playback, so switching between both sources keeps the level. Null for the audio file itself and
    /// while a loudness is unknown.
    /// </summary>
    public double? LoudnessMatchDb { get; init; }
}
