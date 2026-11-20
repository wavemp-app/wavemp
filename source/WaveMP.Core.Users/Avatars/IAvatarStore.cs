namespace WaveMP.Core.Users.Avatars;

/// <summary>
/// The uploaded avatars in the folder <c>avatars</c> of the data folder: one folder per profile with the picture as
/// square WebP files of 96, 300 and 1000 pixels (never enlarged). The uploaded file itself is not kept. Unlike the
/// cache, the folder holds data of the users and belongs into backups.
/// </summary>
public interface IAvatarStore
{
    /// <summary>
    /// Whether avatars can be stored; <c>false</c> if the folder cannot be written.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Creates the folder and removes unfinished files. A folder that cannot be written does not fail; the reason is
    /// logged and uploads are refused.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognizes the picture by its content (JPEG, PNG or WebP), crops it to a square around its center and stores
    /// it in every size, replacing an earlier avatar of the profile.
    /// </summary>
    Task<AvatarSaveStatus> SaveAsync(Guid userId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored picture of the size (see <see cref="Caching.Covers.CoverSizes.SnapStored"/>), or <c>null</c>
    /// if there is none. The caller disposes it.
    /// </summary>
    Stream? Open(Guid userId, int size);

    /// <summary>
    /// Removes the uploaded avatar of a profile; a profile without one is fine.
    /// </summary>
    void Delete(Guid userId);

    /// <summary>
    /// The ids of the profiles that have a folder in the store.
    /// </summary>
    IReadOnlyList<Guid> ListUserIds();
}
