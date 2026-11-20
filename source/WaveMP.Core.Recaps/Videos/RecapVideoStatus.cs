namespace WaveMP.Core.Recaps.Videos;

/// <summary>
/// Result of the conversion of a recorded video.
/// </summary>
public enum RecapVideoStatus
{
    Converted,

    /// <summary>
    /// The video is larger than allowed.
    /// </summary>
    TooLarge,

    /// <summary>
    /// The content is no WebM, Matroska or MP4 video.
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// ffmpeg could not convert the video, e.g. because it is damaged.
    /// </summary>
    Unreadable,

    /// <summary>
    /// ffmpeg is missing or the folder of the videos cannot be written.
    /// </summary>
    Unavailable,
}
