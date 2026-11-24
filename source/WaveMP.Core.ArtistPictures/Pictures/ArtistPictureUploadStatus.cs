namespace WaveMP.Core.ArtistPictures.Pictures;

/// <summary>
/// Outcome of uploading a picture of an artist.
/// </summary>
public enum ArtistPictureUploadStatus
{
    Saved,

    /// <summary>
    /// The artist does not exist (any more).
    /// </summary>
    ArtistNotFound,

    /// <summary>
    /// The content is no JPEG, PNG or WebP picture.
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// The picture is damaged or cannot be converted.
    /// </summary>
    Unreadable,

    /// <summary>
    /// Pictures cannot be stored right now: ffmpeg is missing or the folder cannot be written.
    /// </summary>
    Unavailable,
}
