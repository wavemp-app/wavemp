namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// Decides how <c>stream</c> delivers a track from the parameters <c>format</c>, <c>maxBitRate</c> and
/// <c>timeOffset</c>:
/// <list type="bullet">
/// <item><c>format=raw</c> delivers the file as it is.</item>
/// <item>A known format (<c>mp3</c>, <c>opus</c>, <c>aac</c>) is delivered directly if the file already has it and
/// stays within <c>maxBitRate</c>, otherwise transcoded to it.</item>
/// <item>Without a format, files that Subsonic apps commonly play (MP3, FLAC, AAC, Ogg, Opus, WAV) are delivered
/// directly while they stay within <c>maxBitRate</c>; all others are transcoded to MP3.</item>
/// <item>The sound of a track with only a video is always transcoded.</item>
/// </list>
/// The bitrate of a transcode is <c>maxBitRate</c>, without one the bitrate of the transcoding settings, within 32 to
/// 320 kbit/s.
/// </summary>
public static class SubsonicStreamPlanner
{
    private static readonly HashSet<string> _commonExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "flac", "m4a", "aac", "ogg", "oga", "opus", "wav",
    };

    /// <param name="format">The parameter <c>format</c>.</param>
    /// <param name="maxBitRateKbps">The parameter <c>maxBitRate</c>; 0 or <c>null</c> for no limit.</param>
    /// <param name="timeOffsetSeconds">The parameter <c>timeOffset</c>.</param>
    /// <param name="extension">Extension of the file whose sound the track plays.</param>
    /// <param name="fileBitrate">Bitrate of the file in bit/s, if known.</param>
    /// <param name="fromVideo">Whether the sound comes from the video of the track.</param>
    /// <param name="defaultBitrateKbps">Bitrate of transcodes without <c>maxBitRate</c>.</param>
    public static SubsonicStreamPlan Plan(
        string? format,
        int? maxBitRateKbps,
        int? timeOffsetSeconds,
        string extension,
        int? fileBitrate,
        bool fromVideo,
        int defaultBitrateKbps)
    {
        int limit = maxBitRateKbps is > 0 ? maxBitRateKbps.Value : 0;
        bool raw = string.Equals(format?.Trim(), SubsonicTranscodeFormats.Raw, StringComparison.OrdinalIgnoreCase);
        if (raw && !fromVideo)
        {
            return SubsonicStreamPlan.Direct;
        }

        bool withinLimit = limit == 0 || fileBitrate is not { } bitrate || bitrate <= limit * 1000;
        SubsonicTranscodeFormat? requested = SubsonicTranscodeFormats.Parse(format);
        bool direct = !fromVideo && withinLimit && (requested is { } known
            ? SubsonicTranscodeFormats.Matches(known, extension)
            : _commonExtensions.Contains(extension));
        if (direct)
        {
            return SubsonicStreamPlan.Direct;
        }

        int bitrateKbps = Math.Clamp(
            limit > 0 ? limit : defaultBitrateKbps,
            SubsonicTranscodeFormats.MinBitrateKbps,
            SubsonicTranscodeFormats.MaxBitrateKbps);
        return new SubsonicStreamPlan(
            true,
            requested ?? SubsonicTranscodeFormats.Default,
            bitrateKbps,
            Math.Max(0, timeOffsetSeconds ?? 0));
    }
}
