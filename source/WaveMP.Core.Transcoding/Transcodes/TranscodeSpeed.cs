using WaveMP.Core.Transcoding.Profiles;

namespace WaveMP.Core.Transcoding.Transcodes;

/// <summary>
/// The speed of a transcode as the speed factor of ffmpeg: seconds of the file produced per second of time. Below 1 the
/// transcode is slower than real time, so a player that plays from its start has to wait for segments sooner or later.
/// Pure functions.
/// </summary>
public static class TranscodeSpeed
{
    /// <summary>
    /// Segments a transcode has to produce before its speed counts as measured: the first one includes the start of
    /// ffmpeg and the opening of the file.
    /// </summary>
    public const int MeasuredAfterSegments = 2;

    /// <summary>
    /// Seconds of the file the segments from <paramref name="startSegment"/> to before <paramref name="nextSegment"/>
    /// cover.
    /// </summary>
    public static TimeSpan ProducedOf(TranscodeVariant variant, int startSegment, int nextSegment)
    {
        ArgumentNullException.ThrowIfNull(variant);
        TimeSpan produced = TimeSpan.Zero;
        int end = Math.Min(nextSegment, variant.SegmentCount);
        for (int index = Math.Max(0, startSegment); index < end; index++)
        {
            produced += variant.DurationOf(index);
        }

        return produced;
    }

    /// <summary>
    /// The speed factor, or <c>null</c> before the first segment or without elapsed time.
    /// </summary>
    public static double? Of(TimeSpan produced, TimeSpan elapsed)
    {
        if (produced <= TimeSpan.Zero || elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        return Math.Round(produced / elapsed, 2);
    }

    /// <summary>
    /// Whether a transcode with this speed after this many segments is slower than real time.
    /// </summary>
    public static bool IsSlowerThanRealTime(double? speed, int producedSegments)
    {
        return speed is { } value && value < 1 && producedSegments >= MeasuredAfterSegments;
    }
}
