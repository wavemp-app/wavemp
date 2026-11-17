using WaveMP.Core.Caching.Covers;

namespace WaveMP.Core.Users.Avatars;

/// <summary>
/// The avatars of the profiles: an uploaded picture or one of the <see cref="AvatarPresets"/>. Profiles without an
/// avatar show their initials.
/// </summary>
public interface IAvatarService
{
    /// <summary>
    /// Stores an uploaded picture (JPEG, PNG or WebP, recognized by its content) as avatar of the profile, cropped to a
    /// square around its center. Replaces an earlier avatar.
    /// </summary>
    Task<AvatarResult> UploadAsync(Guid userId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets one of the built-in avatars as avatar of the profile. Replaces an earlier avatar.
    /// </summary>
    Task<AvatarResult> ChoosePresetAsync(
        Guid userId,
        string presetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the avatar of the profile; it shows its initials again. Returns <c>false</c> if the profile does not
    /// exist; a profile without an avatar is fine.
    /// </summary>
    Task<bool> RemoveAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The avatar of the profile as square WebP in the size (rounded up to 96, 300 or 1000; the largest without a
    /// size).
    /// </summary>
    Task<CoverDeliveryResult> GetAsync(Guid userId, int? size, CancellationToken cancellationToken = default);

    /// <summary>
    /// A built-in avatar as square WebP in the size (rounded up to 96, 300 or 1000; the largest without a size);
    /// <see cref="CoverDeliveryResult.NotFound"/> for an unknown id.
    /// </summary>
    CoverDeliveryResult GetPreset(string presetId, int? size);

    /// <summary>
    /// Removes the stored avatars of profiles that no longer exist.
    /// </summary>
    Task RemoveOrphansAsync(CancellationToken cancellationToken = default);
}
