using WaveMP.Data.Entities;

namespace WaveMP.Core.Users.Avatars;

/// <summary>
/// Outcome of setting the avatar of a profile.
/// </summary>
public enum AvatarStatus
{
    Saved,

    /// <summary>
    /// The profile does not exist (any more).
    /// </summary>
    UserNotFound,

    /// <summary>
    /// There is no built-in avatar with the id.
    /// </summary>
    PresetNotFound,

    /// <summary>
    /// The content is no JPEG, PNG or WebP picture.
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// The picture is damaged or cannot be converted.
    /// </summary>
    Unreadable,

    /// <summary>
    /// Avatars cannot be stored right now: ffmpeg is missing or the folder cannot be written.
    /// </summary>
    Unavailable,
}

/// <summary>
/// The profile with its new avatar, or why it was not set.
/// </summary>
/// <param name="Status">Whether the avatar was set.</param>
/// <param name="User">The profile with its new avatar; set if saved.</param>
public sealed record AvatarResult(AvatarStatus Status, User? User)
{
    public static AvatarResult Failed(AvatarStatus status)
    {
        return new AvatarResult(status, null);
    }
}
