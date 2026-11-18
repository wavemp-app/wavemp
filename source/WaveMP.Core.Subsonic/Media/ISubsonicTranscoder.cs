namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// Starts the transcodes of <c>stream</c>: ffmpeg as a separate process (never linked) that writes the sound to its
/// standard output, which goes to the app as it comes.
/// </summary>
public interface ISubsonicTranscoder
{
    /// <summary>
    /// Starts ffmpeg; <c>null</c> if it cannot be started. Disposing the transcode stops ffmpeg.
    /// </summary>
    ISubsonicTranscode? Start(string inputPath, SubsonicStreamPlan plan);
}
