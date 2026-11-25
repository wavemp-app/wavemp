using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;

namespace WaveMP.Data.Repositories;

internal sealed class DeviceRepository(WaveMpDbContext dbContext) : IDeviceRepository
{
    public async Task<IReadOnlyList<Device>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Devices.AsNoTracking()
            .Where(device => device.UserId == userId)
            .OrderByDescending(device => device.LastActiveAt)
            .ThenBy(device => device.DeviceId)
            .ToListAsync(cancellationToken);
    }

    public Task<Device?> FindAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        return dbContext.Devices.AsNoTracking()
            .FirstOrDefaultAsync(
                device => device.UserId == userId && device.DeviceId == deviceId,
                cancellationToken);
    }

    public Task<Device> RegisterAsync(
        Guid userId,
        string deviceId,
        string reportedName,
        DeviceType type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        string typeName = type.ToString();
        DateTimeOffset at = now.ToUniversalTime();
        return dbContext.InUserTransactionAsync(
            userId,
            async token =>
            {
                // One statement, so two connections of the same device registering at the same time cannot both
                // insert it.
                await dbContext.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO devices (user_id, device_id, reported_name, custom_name, type, created_at,
                        last_active_at)
                    VALUES ({userId}, {deviceId}, {reportedName}, NULL, {typeName}, {at}, {at})
                    ON CONFLICT (user_id, device_id) DO UPDATE
                    SET reported_name = EXCLUDED.reported_name, type = EXCLUDED.type,
                        last_active_at = GREATEST(devices.last_active_at, EXCLUDED.last_active_at)
                    """,
                    token);
                return await dbContext.Devices.AsNoTracking()
                    .FirstAsync(device => device.UserId == userId && device.DeviceId == deviceId, token);
            },
            cancellationToken);
    }

    public async Task<Device?> RenameAsync(
        Guid userId,
        string deviceId,
        string? customName,
        CancellationToken cancellationToken = default)
    {
        int updated = await dbContext.Devices
            .Where(device => device.UserId == userId && device.DeviceId == deviceId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(device => device.CustomName, customName),
                cancellationToken);
        return updated == 0 ? null : await FindAsync(userId, deviceId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        int deleted = await dbContext.Devices
            .Where(device => device.UserId == userId && device.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public Task TouchAsync(
        Guid userId,
        string deviceId,
        DateTimeOffset activeAt,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset at = activeAt.ToUniversalTime();
        return dbContext.Devices
            .Where(device => device.UserId == userId && device.DeviceId == deviceId && device.LastActiveAt < at)
            .ExecuteUpdateAsync(setters => setters.SetProperty(device => device.LastActiveAt, at), cancellationToken);
    }

    public Task<int> EvictAsync(
        Guid userId,
        int maxDevices,
        IReadOnlyCollection<string> keptDeviceIds,
        CancellationToken cancellationToken = default)
    {
        string[] kept = [.. keptDeviceIds];
        int max = Math.Max(maxDevices, 0);
        return dbContext.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM devices
            WHERE user_id = {userId} AND device_id IN (
                SELECT device_id FROM devices
                WHERE user_id = {userId} AND device_id <> ALL({kept})
                ORDER BY last_active_at, device_id
                LIMIT GREATEST((SELECT count(*) FROM devices WHERE user_id = {userId}) - {max}, 0))
            """,
            cancellationToken);
    }
}
