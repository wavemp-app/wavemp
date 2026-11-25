using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Devices;

namespace WaveMP.Core.Devices.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the devices of the profiles: the coordinator of the live devices and their playback (one per server,
    /// in memory) and the stored devices. Needs <c>AddWaveMpData</c> and <c>AddWaveMpStreaming</c>. The host registers
    /// the <see cref="IDeviceNotifier"/> of its real-time transport; until it does, nothing is sent.
    /// </summary>
    public static IServiceCollection AddWaveMpDevices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDeviceNotifier, NullDeviceNotifier>();
        services.AddSingleton<DeviceCoordinator>();
        services.AddSingleton<IDeviceCoordinator>(provider => provider.GetRequiredService<DeviceCoordinator>());
        services.AddScoped<IDeviceService, DeviceService>();

        return services;
    }
}
