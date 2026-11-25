using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Devices.Tests.Fakes;

/// <summary>
/// The devices of the profiles in memory, with the rules of the real repository.
/// </summary>
internal sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly Lock _lock = new();
    private readonly List<Device> _devices = [];

    /// <summary>
    /// Profiles that do not exist; registering a device for them throws.
    /// </summary>
    public HashSet<Guid> MissingUsers { get; } = [];

    public Task<IReadOnlyList<Device>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Device>>([.. _devices
                .Where(device => device.UserId == userId)
                .OrderByDescending(device => device.LastActiveAt)
                .ThenBy(device => device.DeviceId, StringComparer.Ordinal)
                .Select(Copy)]);
        }
    }

    public Task<Device?> FindAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(Find(userId, deviceId) is { } device ? Copy(device) : null);
        }
    }

    public Task<Device> RegisterAsync(
        Guid userId,
        string deviceId,
        string reportedName,
        DeviceType type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (MissingUsers.Contains(userId))
        {
            throw new UserNotFoundException(userId);
        }

        lock (_lock)
        {
            Device? device = Find(userId, deviceId);
            if (device is null)
            {
                device = new Device
                {
                    UserId = userId,
                    DeviceId = deviceId,
                    ReportedName = reportedName,
                    Type = type,
                    CreatedAt = now,
                    LastActiveAt = now,
                };
                _devices.Add(device);
            }
            else
            {
                device.ReportedName = reportedName;
                device.Type = type;
                device.LastActiveAt = device.LastActiveAt > now ? device.LastActiveAt : now;
            }

            return Task.FromResult(Copy(device));
        }
    }

    public Task<Device?> RenameAsync(
        Guid userId,
        string deviceId,
        string? customName,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            Device? device = Find(userId, deviceId);
            if (device is null)
            {
                return Task.FromResult<Device?>(null);
            }

            device.CustomName = customName;
            return Task.FromResult<Device?>(Copy(device));
        }
    }

    public Task<bool> DeleteAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(
                _devices.RemoveAll(device => device.UserId == userId && device.DeviceId == deviceId) > 0);
        }
    }

    public Task TouchAsync(
        Guid userId,
        string deviceId,
        DateTimeOffset activeAt,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (Find(userId, deviceId) is { } device && device.LastActiveAt < activeAt)
            {
                device.LastActiveAt = activeAt;
            }

            return Task.CompletedTask;
        }
    }

    public Task<int> EvictAsync(
        Guid userId,
        int maxDevices,
        IReadOnlyCollection<string> keptDeviceIds,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            List<Device> ofUser = [.. _devices.Where(device => device.UserId == userId)];
            int excess = ofUser.Count - maxDevices;
            if (excess <= 0)
            {
                return Task.FromResult(0);
            }

            Device[] removed = [.. ofUser
                .Where(device => !keptDeviceIds.Contains(device.DeviceId))
                .OrderBy(device => device.LastActiveAt)
                .ThenBy(device => device.DeviceId, StringComparer.Ordinal)
                .Take(excess)];
            foreach (Device device in removed)
            {
                _devices.Remove(device);
            }

            return Task.FromResult(removed.Length);
        }
    }

    private Device? Find(Guid userId, string deviceId)
    {
        return _devices.FirstOrDefault(device => device.UserId == userId && device.DeviceId == deviceId);
    }

    private static Device Copy(Device device)
    {
        return new Device
        {
            UserId = device.UserId,
            DeviceId = device.DeviceId,
            ReportedName = device.ReportedName,
            CustomName = device.CustomName,
            Type = device.Type,
            CreatedAt = device.CreatedAt,
            LastActiveAt = device.LastActiveAt,
        };
    }
}
