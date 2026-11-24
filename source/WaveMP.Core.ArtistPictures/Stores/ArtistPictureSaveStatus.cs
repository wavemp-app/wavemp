namespace WaveMP.Core.ArtistPictures.Stores;

/// <summary>
/// Outcome of storing an artist picture.
/// </summary>
public enum ArtistPictureSaveStatus
{
    Saved,

    /// <summary>
    /// The content is no JPEG, PNG or WebP picture.
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// The picture is damaged or cannot be converted.
    /// </summary>
    Unreadable,

    /// <summary>
    /// Pictures cannot be stored: ffmpeg is missing or the folder cannot be written.
    /// </summary>
    Unavailable,
}
