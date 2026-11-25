using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The devices of the profiles. Nothing is tracked.
/// </summary>
public interface IDeviceRepository
{
    /// <summary>
    /// All devices of the profile, the most recently active first.
    /// </summary>
    Task<IReadOnlyList<Device>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a device of the profile; <c>null</c> if the profile has no device with this id.
    /// </summary>
    Task<Device?> FindAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the device to the profile, or updates its reported name, type and last activity if the profile knows it
    /// already (the name the user gave it stays), in one statement. Returns the device as saved.
    /// </summary>
    /// <exception cref="UserNotFoundException">The profile does not exist.</exception>
    Task<Device> RegisterAsync(
        Guid userId,
        string deviceId,
        string reportedName,
        DeviceType type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the name the user gave the device; <c>null</c> shows the reported name again. Returns the device as
    /// saved, or <c>null</c> if the profile has no device with this id.
    /// </summary>
    Task<Device?> RenameAsync(
        Guid userId,
        string deviceId,
        string? customName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the device of the profile. Returns <c>false</c> if the profile has no device with this id.
    /// </summary>
    Task<bool> DeleteAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notes that the device was active at <paramref name="activeAt"/>; an earlier time does not move its last
    /// activity back.
    /// </summary>
    Task TouchAsync(
        Guid userId,
        string deviceId,
        DateTimeOffset activeAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Keeps at most <paramref name="maxDevices"/> devices of the profile: removes the least recently active devices
    /// above the limit, except those in <paramref name="keptDeviceIds"/> (the open ones). Returns the number of removed
    /// devices.
    /// </summary>
    Task<int> EvictAsync(
        Guid userId,
        int maxDevices,
        IReadOnlyCollection<string> keptDeviceIds,
        CancellationToken cancellationToken = default);
}
