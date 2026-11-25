using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Extensions;
using WaveMP.Core.Devices.Tests.Fakes;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Devices.Tests.Extensions;

[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddWaveMpDevices_WithItsDependencies_ResolvesAllServices()
    {
        using ServiceProvider provider = CreateServices().BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDeviceService>().Should().NotBeNull();
        provider.GetRequiredService<IDeviceCoordinator>().Should().BeSameAs(
            provider.GetRequiredService<DeviceCoordinator>());
        provider.GetRequiredService<IDeviceNotifier>().Should().BeOfType<NullDeviceNotifier>();
    }

    [Test]
    public void AddWaveMpDevices_NotifierOfTheHost_IsUsed()
    {
        ServiceCollection services = CreateServices();
        services.AddSingleton<IDeviceNotifier, RecordingNotifier>();

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDeviceNotifier>().Should().BeOfType<RecordingNotifier>();
    }

    private static ServiceCollection CreateServices()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped(_ => Substitute.For<IDeviceRepository>());
        services.AddScoped(_ => Substitute.For<ITrackRepository>());
        services.AddScoped(_ => Substitute.For<IPlaybackStateService>());
        services.AddWaveMpDevices();
        return services;
    }
}
