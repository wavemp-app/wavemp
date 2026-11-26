using WaveMP.Core.Transcoding.Accelerators;
using WaveMP.Core.Transcoding.Profiles;

namespace WaveMP.Core.Transcoding.Transcodes;

/// <summary>
/// A running transcode, as shown to an admin.
/// </summary>
/// <param name="Id">Id of the transcode.</param>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Media">Video with sound or sound only.</param>
/// <param name="Accelerator">The encoder of the picture; <see cref="HardwareAccelerator.None"/> for software and for
/// sound only.</param>
/// <param name="StartSegment">The segment the transcode started at.</param>
/// <param name="CompletedSegments">How many segments of the file exist from the start segment on: the index of the
/// next segment.</param>
/// <param name="SegmentCount">Number of segments of the file.</param>
/// <param name="StartedAt">When the transcode started.</param>
/// <param name="LastAccessAt">When a client last asked for one of its segments.</param>
/// <param name="UserId">The user whose request started it.</param>
/// <param name="Speed">The speed factor: seconds of the file produced per second since the start; below 1 the transcode
/// is slower than real time. <c>null</c> before the first segment.</param>
public sealed record TranscodeJobInfo(
    Guid Id,
    Guid TrackId,
    HlsMedia Media,
    HardwareAccelerator Accelerator,
    int StartSegment,
    int CompletedSegments,
    int SegmentCount,
    DateTimeOffset StartedAt,
    DateTimeOffset LastAccessAt,
    Guid? UserId,
    double? Speed = null)
{
    /// <summary>
    /// Whether the transcode produces its segments slower than real time (measured after its first segments).
    /// </summary>
    public bool SlowerThanRealTime =>
        TranscodeSpeed.IsSlowerThanRealTime(Speed, CompletedSegments - StartSegment);
}
