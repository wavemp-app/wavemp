namespace WaveMP.Core.Streaming.Decisions;

/// <summary>
/// How a client plays a track.
/// </summary>
public enum PlaybackMethod
{
    /// <summary>
    /// The client plays the file as it is.
    /// </summary>
    DirectPlay,

    /// <summary>
    /// The client cannot play the file; the server converts it to HLS.
    /// </summary>
    Transcode,

    /// <summary>
    /// Videos only: the client plays the codec of the picture but not the container (or not the codec of the sound);
    /// the server copies the picture into an MP4 file without converting it, which the client plays like a file.
    /// </summary>
    Remux,
}
