namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// How <c>stream</c> delivers the sound of a track.
/// </summary>
/// <param name="Transcode">Whether the sound is transcoded; otherwise the file is delivered as it is.</param>
/// <param name="Format">The format of the transcode.</param>
/// <param name="BitrateKbps">The bitrate of the transcode in kbit/s.</param>
/// <param name="OffsetSeconds">Where the transcode starts in the track (OpenSubsonic <c>transcodeOffset</c>).</param>
public sealed record SubsonicStreamPlan(
    bool Transcode,
    SubsonicTranscodeFormat Format,
    int BitrateKbps,
    int OffsetSeconds)
{
    public static SubsonicStreamPlan Direct { get; } = new(false, SubsonicTranscodeFormats.Default, 0, 0);
}
