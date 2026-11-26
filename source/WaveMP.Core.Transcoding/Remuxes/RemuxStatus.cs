namespace WaveMP.Core.Transcoding.Remuxes;

/// <summary>
/// Outcome of a request for the remuxed video of a track.
/// </summary>
public enum RemuxStatus
{
    /// <summary>
    /// The remuxed file is ready.
    /// </summary>
    Ready,

    /// <summary>
    /// The track does not exist.
    /// </summary>
    TrackNotFound,

    /// <summary>
    /// The track has no video.
    /// </summary>
    NoVideo,

    /// <summary>
    /// The video is gone since the last scan.
    /// </summary>
    FileMissing,

    /// <summary>
    /// ffmpeg cannot be started or the cache cannot be written.
    /// </summary>
    Unavailable,

    /// <summary>
    /// ffmpeg could not remux the file (e.g. broken timestamps); the video is transcoded instead from now on.
    /// </summary>
    Failed,
}
