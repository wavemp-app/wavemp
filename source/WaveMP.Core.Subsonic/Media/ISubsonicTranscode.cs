namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// A running transcode of <c>stream</c>. Disposing it stops ffmpeg.
/// </summary>
public interface ISubsonicTranscode : IDisposable
{
    /// <summary>
    /// The transcoded sound as ffmpeg writes it.
    /// </summary>
    Stream Output { get; }

    /// <summary>
    /// Completes with the exit code of ffmpeg.
    /// </summary>
    Task<int> Exited { get; }

    /// <summary>
    /// The end of the error output of ffmpeg, for the log.
    /// </summary>
    string ErrorOutput { get; }
}
