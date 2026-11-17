using System.Globalization;

namespace WaveMP.Core.Users.Avatars;

/// <summary>
/// The version of an avatar: the time it was set as text. Clients use it to tell a new picture from one they already
/// loaded; it is also part of the entity tag.
/// </summary>
public static class AvatarVersions
{
    public static string Of(DateTimeOffset avatarUpdatedAt)
    {
        return avatarUpdatedAt.UtcTicks.ToString("x", CultureInfo.InvariantCulture);
    }
}
