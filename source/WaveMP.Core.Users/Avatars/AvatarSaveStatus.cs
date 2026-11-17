namespace WaveMP.Core.Users.Avatars;

/// <summary>
/// Outcome of storing an uploaded avatar.
/// </summary>
public enum AvatarSaveStatus
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
    /// Avatars cannot be stored: ffmpeg is missing or the folder cannot be written.
    /// </summary>
    Unavailable,
}
