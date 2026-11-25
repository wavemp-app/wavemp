using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Devices.Tests.Fakes;

/// <summary>
/// A coordinator with devices in memory, recorded messages, a library of known songs and a clock the test moves.
/// </summary>
internal sealed class DeviceTestContext : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 11, 25, 20, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;
    private readonly Dictionary<Guid, TrackRow> _library = [];

    public DeviceTestContext()
    {
        Clock = new ManualClock(Start);
        Repository = new InMemoryDeviceRepository();
        Notifier = new RecordingNotifier();
        Tracks = Substitute.For<ITrackRepository>();
        Tracks.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<TrackRow>>([.. call.Arg<IReadOnlyCollection<Guid>>()
                .Where(_library.ContainsKey)
                .Select(id => _library[id])]));
        States = Substitute.For<IPlaybackStateService>();
        States.SaveAsync(Arg.Any<Guid>(), Arg.Any<SavePlaybackStateCommand>(), Arg.Any<CancellationToken>())
            .Returns(PlaybackStateSaveStatus.Saved);

        ServiceCollection services = new();
        services.AddScoped<IDeviceRepository>(_ => Repository);
        services.AddScoped(_ => Tracks);
        services.AddScoped(_ => States);
        _services = services.BuildServiceProvider();
        Coordinator = new DeviceCoordinator(
            _services.GetRequiredService<IServiceScopeFactory>(),
            Notifier,
            Clock,
            NullLogger<DeviceCoordinator>.Instance);
        Service = new DeviceService(Repository, Coordinator);
    }

    public ManualClock Clock { get; }

    public InMemoryDeviceRepository Repository { get; }

    public RecordingNotifier Notifier { get; }

    public ITrackRepository Tracks { get; }

    public IPlaybackStateService States { get; }

    public DeviceCoordinator Coordinator { get; }

    public DeviceService Service { get; }

    /// <summary>
    /// The version a new live state starts with.
    /// </summary>
    public static long InitialVersion => Start.ToUnixTimeMilliseconds();

    /// <summary>
    /// Adds a song to the library and returns its id.
    /// </summary>
    public Guid AddTrack(string title)
    {
        TrackRow track = new(
            Guid.NewGuid(),
            title,
            null,
            "Artist",
            null,
            "Album",
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            TimeSpan.FromMinutes(3),
            true,
            false,
            false,
            Start);
        _library[track.Id] = track;
        return track.Id;
    }

    /// <summary>
    /// Opens a connection of the profile and registers a device with it.
    /// </summary>
    public async Task<DeviceConnection> RegisterAsync(
        Guid userId,
        string deviceId,
        bool remoteControl = false,
        string? connectionId = null,
        Guid? sessionId = null)
    {
        DeviceConnection connection = new(
            userId,
            sessionId ?? Guid.NewGuid(),
            connectionId ?? $"connection-{deviceId}-{Guid.NewGuid():N}");
        RegisterResult result = await Coordinator.RegisterAsync(
            connection,
            new RegisterDeviceCommand(deviceId, $"Device {deviceId}", DeviceType.Desktop, remoteControl));
        if (!result.IsRegistered)
        {
            throw new InvalidOperationException("The device was not registered.");
        }

        return connection;
    }

    /// <summary>
    /// The device publishes that it plays the songs and claims playback.
    /// </summary>
    public Task<PublishResult> PlayAsync(
        DeviceConnection connection,
        IReadOnlyList<Guid> trackIds,
        int currentIndex = 0,
        int positionMilliseconds = 0,
        bool claim = true)
    {
        return Coordinator.PublishAsync(
            connection,
            new PublishPlaybackCommand(claim, Playback(trackIds, currentIndex, positionMilliseconds)));
    }

    /// <summary>
    /// A valid playing state with a queue of the songs.
    /// </summary>
    public static LivePlaybackInput Playback(
        IReadOnlyList<Guid> trackIds,
        int currentIndex = 0,
        int positionMilliseconds = 0)
    {
        return new LivePlaybackInput(
            LivePlaybackStatus.Playing,
            new LiveQueueInput([.. trackIds.Select(id => new PlaybackStateItem(id, false))], false, [], null),
            currentIndex,
            positionMilliseconds,
            180_000,
            RepeatMode.Off,
            0.8,
            false,
            false,
            false);
    }

    public void Dispose()
    {
        _services.Dispose();
    }
}
