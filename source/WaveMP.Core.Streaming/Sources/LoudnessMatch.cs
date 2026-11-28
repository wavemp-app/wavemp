namespace WaveMP.Core.Streaming.Sources;

/// <summary>
/// Brings the sound of a video to the loudness of the audio file of the same track, so a client that switches
/// between both sources keeps the level.
/// </summary>
public static class LoudnessMatch
{
    /// <summary>
    /// The largest gain in dB either way; a larger difference points to a wrong measurement rather than a quiet
    /// video.
    /// </summary>
    public const double MaxDb = 12;

    /// <summary>
    /// The gain in dB for a sound of <paramref name="sourceLufs"/> to play as loud as <paramref name="referenceLufs"/>,
    /// rounded to 0.1 dB and limited to <see cref="MaxDb"/>; <c>null</c> if a loudness is unknown.
    /// </summary>
    public static double? Between(double? referenceLufs, double? sourceLufs)
    {
        return referenceLufs is { } reference && sourceLufs is { } source
            ? Math.Round(Math.Clamp(reference - source, -MaxDb, MaxDb), 1)
            : null;
    }
}
