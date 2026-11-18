namespace WaveMP.Core.Streaming.Signatures;

/// <summary>
/// Who plays a signed media URL. It decides how long the URL stays valid, not what it grants.
/// </summary>
public enum MediaUrlTarget
{
    /// <summary>
    /// The client itself, e.g. an audio or video element of the web app.
    /// </summary>
    Local,

    /// <summary>
    /// A casting device (Chromecast, AirPlay) the client hands the URL to. The device keeps the URL while the client
    /// controls it, also while it waits paused, so the URL stays valid longer.
    /// </summary>
    Cast,
}
