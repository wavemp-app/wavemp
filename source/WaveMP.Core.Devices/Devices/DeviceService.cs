using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Validations;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Devices.Devices;

internal sealed class DeviceService(IDeviceRepository devices, DeviceCoordinator coordinator) : IDeviceService
{
    public async Task<Page<DeviceView>> ListAsync(
        Guid userId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Device> stored = await devices.ListAsync(userId, cancellationToken);
        LiveDevices live = await coordinator.GetLiveDevicesAsync(userId, cancellationToken);
        IReadOnlyList<DeviceView> all = DeviceViews.Build(stored, live);
        return new Page<DeviceView>([.. all.Skip(offset).Take(limit)], offset, limit, all.Count);
    }

    public async Task<DeviceView?> GetAsync(
        Guid userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        Device? device = await devices.FindAsync(userId, deviceId, cancellationToken);
        return device is null
            ? null
            : DeviceViews.Build([device], await coordinator.GetLiveDevicesAsync(userId, cancellationToken))[0];
    }

    public async Task<DeviceRenameResult> RenameAsync(
        Guid userId,
        string deviceId,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (DeviceRules.ValidateName(name, "name") is { } error)
        {
            return new DeviceRenameResult(DeviceRenameStatus.Invalid, null, error);
        }

        string normalized = DeviceRules.NormalizeName(name);
        Device? device = await devices.FindAsync(userId, deviceId, cancellationToken);
        if (device is null)
        {
            return new DeviceRenameResult(DeviceRenameStatus.NotFound, null, null);
        }

        // The name the device reports for itself is no name of its own.
        string? customName = normalized == device.ReportedName ? null : normalized;
        Device? renamed = await coordinator.RenameAsync(userId, deviceId, customName, cancellationToken);
        if (renamed is null)
        {
            return new DeviceRenameResult(DeviceRenameStatus.NotFound, null, null);
        }

        LiveDevices live = await coordinator.GetLiveDevicesAsync(userId, cancellationToken);
        return new DeviceRenameResult(DeviceRenameStatus.Renamed, DeviceViews.Build([renamed], live)[0], null);
    }

    public Task<DeviceDeleteStatus> DeleteAsync(
        Guid userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        return coordinator.ForgetAsync(userId, deviceId, cancellationToken);
    }
}
