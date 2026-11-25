using WaveMP.Data.Queries;

namespace WaveMP.Core.Devices.Devices;

/// <summary>
/// The devices a profile has used WaveMP on, with their live state: list, rename and forget them.
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// One page of the devices of the profile: open devices first, then the most recently active.
    /// </summary>
    Task<Page<DeviceView>> ListAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A device of the profile; <c>null</c> if the profile has no device with this id.
    /// </summary>
    Task<DeviceView?> GetAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a device of the profile a name (spaces at its ends are removed). The name the device reports itself
    /// removes the given name. The open devices of the profile learn the new name at once.
    /// </summary>
    Task<DeviceRenameResult> RenameAsync(
        Guid userId,
        string deviceId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets a closed device of the profile; an open device cannot be forgotten.
    /// </summary>
    Task<DeviceDeleteStatus> DeleteAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);
}
