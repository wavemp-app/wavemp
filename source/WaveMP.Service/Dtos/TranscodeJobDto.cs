namespace WaveMP.Service.Dtos;

/// <summary>
/// A running transcode.
/// </summary>
public sealed class TranscodeJobDto
{
    /// <summary>
    /// Id of the transcode.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Id of the track.
    /// </summary>
    public required Guid TrackId { get; init; }

    /// <summary>
    /// The track; null if it was removed in the meantime.
    /// </summary>
    public TrackDto? Track { get; init; }

    /// <summary>
    /// Video with sound or sound only.
    /// </summary>
    public required HlsMediaDto Media { get; init; }

    /// <summary>
    /// The encoder of the picture; none for software and for sound only.
    /// </summary>
    public required HardwareAcceleratorDto Accelerator { get; init; }

    /// <summary>
    /// The segment the transcode started at (0 from the start, later after a seek).
    /// </summary>
    public required int StartSegment { get; init; }

    /// <summary>
    /// Index of the next segment the transcode produces: the segments before it exist.
    /// </summary>
    public required int CompletedSegments { get; init; }

    /// <summary>
    /// Number of segments of the file.
    /// </summary>
    public required int SegmentCount { get; init; }

    /// <summary>
    /// When the transcode started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// When a client last asked for one of its segments.
    /// </summary>
    public required DateTimeOffset LastAccessAt { get; init; }

    /// <summary>
    /// Id of the profile whose player started it, if known.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// The speed factor of the transcode: seconds of the file produced per second since its start (as the speed of
    /// ffmpeg); below 1 it is slower than real time. Null before its first segment.
    /// </summary>
    public double? Speed { get; init; }

    /// <summary>
    /// Whether the transcode produces its segments slower than real time, measured after its first two segments: a
    /// player that plays from its start has to wait for segments, so its video stutters.
    /// </summary>
    public required bool SlowerThanRealTime { get; init; }
}
