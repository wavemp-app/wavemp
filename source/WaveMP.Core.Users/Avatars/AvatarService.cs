using System.Globalization;
using Microsoft.Extensions.Logging;
using WaveMP.Core.Caching.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Users.Avatars;

internal sealed class AvatarService(
    IUserRepository users,
    IAvatarStore store,
    TimeProvider timeProvider,
    ILogger<AvatarService> logger) : IAvatarService
{
    internal const string ContentType = "image/webp";

    public async Task<AvatarResult> UploadAsync(
        Guid userId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        User? user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return AvatarResult.Failed(AvatarStatus.UserNotFound);
        }

        AvatarSaveStatus saved = await store.SaveAsync(userId, content, cancellationToken);
        if (saved != AvatarSaveStatus.Saved)
        {
            return AvatarResult.Failed(saved switch
            {
                AvatarSaveStatus.UnsupportedFormat => AvatarStatus.UnsupportedFormat,
                AvatarSaveStatus.Unreadable => AvatarStatus.Unreadable,
                _ => AvatarStatus.Unavailable,
            });
        }

        if (!await SaveAsync(user, presetId: null, cancellationToken))
        {
            // Deleted in the meantime.
            store.Delete(userId);
            return AvatarResult.Failed(AvatarStatus.UserNotFound);
        }

        return new AvatarResult(AvatarStatus.Saved, user);
    }

    public async Task<AvatarResult> ChoosePresetAsync(
        Guid userId,
        string presetId,
        CancellationToken cancellationToken = default)
    {
        if (!AvatarPresets.Contains(presetId))
        {
            return AvatarResult.Failed(AvatarStatus.PresetNotFound);
        }

        User? user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return AvatarResult.Failed(AvatarStatus.UserNotFound);
        }

        if (!await SaveAsync(user, presetId, cancellationToken))
        {
            return AvatarResult.Failed(AvatarStatus.UserNotFound);
        }

        // An uploaded picture is no longer used.
        store.Delete(userId);
        return new AvatarResult(AvatarStatus.Saved, user);
    }

    public async Task<bool> RemoveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        User? user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (user.AvatarUpdatedAt is not null)
        {
            user.AvatarUpdatedAt = null;
            user.AvatarPresetId = null;
            try
            {
                await users.UpdateAsync(user, cancellationToken);
            }
            catch (UserNotFoundException)
            {
                return false;
            }
        }

        store.Delete(userId);
        return true;
    }

    public async Task<CoverDeliveryResult> GetAsync(
        Guid userId,
        int? size,
        CancellationToken cancellationToken = default)
    {
        User? user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return CoverDeliveryResult.OwnerNotFound;
        }

        if (user.AvatarUpdatedAt is not { } updatedAt)
        {
            return CoverDeliveryResult.NotFound;
        }

        int snapped = CoverSizes.SnapStored(size ?? CoverSizes.Large);
        string version = AvatarVersions.Of(updatedAt);
        if (user.AvatarPresetId is { } presetId)
        {
            // The hash changes the tag when a new version of WaveMP changes the picture of the preset.
            Stream? preset = AvatarPresets.Open(presetId, snapped);
            if (preset is null)
            {
                logger.LogWarning(
                    "The built-in avatar {PresetId} of the profile {UserId} does not exist",
                    presetId,
                    userId);
                return CoverDeliveryResult.NotFound;
            }

            string presetTag = Invariant($"{version}-{presetId}-{AvatarPresets.HashOf(presetId, snapped)}-{snapped}");
            return CoverDeliveryResult.Found(new CoverDelivery(preset, ContentType, presetTag, updatedAt));
        }

        Stream? content = store.Open(userId, snapped);
        if (content is null)
        {
            logger.LogWarning("The avatar of the profile {UserId} is missing in its folder", userId);
            return CoverDeliveryResult.NotFound;
        }

        return CoverDeliveryResult.Found(
            new CoverDelivery(content, ContentType, Invariant($"{version}-{snapped}"), updatedAt));
    }

    public CoverDeliveryResult GetPreset(string presetId, int? size)
    {
        int snapped = CoverSizes.SnapStored(size ?? CoverSizes.Large);
        Stream? content = AvatarPresets.Open(presetId, snapped);
        if (content is null)
        {
            return CoverDeliveryResult.NotFound;
        }

        string etag = Invariant($"preset-{presetId}-{AvatarPresets.HashOf(presetId, snapped)}-{snapped}");
        return CoverDeliveryResult.Found(new CoverDelivery(content, ContentType, etag, LastModified: null));
    }

    public async Task RemoveOrphansAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> stored = store.ListUserIds();
        if (stored.Count == 0)
        {
            return;
        }

        IReadOnlySet<Guid> uploaded = await users.ListUploadedAvatarIdsAsync(stored, cancellationToken);
        foreach (Guid id in stored.Where(id => !uploaded.Contains(id)))
        {
            store.Delete(id);
        }
    }

    /// <summary>
    /// Sets the avatar of the tracked user: a new version and the preset (<c>null</c> for an uploaded picture).
    /// Returns <c>false</c> if the user was deleted in the meantime.
    /// </summary>
    private async Task<bool> SaveAsync(User user, string? presetId, CancellationToken cancellationToken)
    {
        user.AvatarUpdatedAt = NextVersion(user.AvatarUpdatedAt);
        user.AvatarPresetId = presetId;
        try
        {
            await users.UpdateAsync(user, cancellationToken);
            return true;
        }
        catch (UserNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// The time of the change, in microseconds (the precision of PostgreSQL), and always later than the previous
    /// version, so two changes in the same microsecond still get different versions.
    /// </summary>
    private DateTimeOffset NextVersion(DateTimeOffset? previous)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        now = new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerMicrosecond), TimeSpan.Zero);
        return previous is { } last && now <= last ? last.AddTicks(TimeSpan.TicksPerMicrosecond) : now;
    }

    private static string Invariant(FormattableString value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
