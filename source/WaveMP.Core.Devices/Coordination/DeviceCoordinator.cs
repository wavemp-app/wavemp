using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Devices.Validations;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// Keeps the live state of every profile in memory. All changes of a profile run one after another under the lock of
/// its <see cref="DeviceSession"/>, including the messages they send, so every device sees the changes in the order of
/// their versions.
/// </summary>
internal sealed class DeviceCoordinator(
    IServiceScopeFactory scopeFactory,
    IDeviceNotifier notifier,
    TimeProvider timeProvider,
    ILogger<DeviceCoordinator> logger) : IDeviceCoordinator
{
    private readonly ConcurrentDictionary<Guid, DeviceSession> _sessions = new();

    public async Task<RegisterResult> RegisterAsync(
        DeviceConnection connection,
        RegisterDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        List<DeviceValidationError> errors = [];
        AddIfInvalid(DeviceRules.ValidateId(command.DeviceId, "deviceId"), errors);
        AddIfInvalid(DeviceRules.ValidateName(command.Name, "name"), errors);
        if (!Enum.IsDefined(command.Type))
        {
            errors.Add(new DeviceValidationError("type", DeviceValidationCode.InvalidValue));
        }

        if (errors.Count > 0)
        {
            return new RegisterResult(null, errors);
        }

        string name = DeviceRules.NormalizeName(command.Name);
        return await WithSessionAsync(
            connection.UserId,
            async session =>
            {
                DateTimeOffset now = timeProvider.GetUtcNow();
                if (session.Connections.TryGetValue(connection.ConnectionId, out ConnectionEntry? own)
                    && own.DeviceId is { } previousId
                    && previousId != command.DeviceId)
                {
                    // A connection runs one device; registering another id closes the previous one.
                    await RemoveDeviceAsync(session, previousId, connection.ConnectionId, now, cancellationToken);
                }

                if (session.Devices.TryGetValue(command.DeviceId, out LiveDevice? existing)
                    && existing.ConnectionId != connection.ConnectionId)
                {
                    // Two tabs of one browser are one device: the newest connection takes part, the older one stops.
                    if (session.Connections.TryGetValue(existing.ConnectionId, out ConnectionEntry? replaced))
                    {
                        session.Connections[existing.ConnectionId] = replaced with { DeviceId = null };
                    }

                    logger.LogDebug(
                        "Device {DeviceId} of profile {UserId} moved to a new connection",
                        command.DeviceId,
                        connection.UserId);
                    await notifier.DeviceReplacedAsync(existing.ConnectionId, cancellationToken);
                }

                Device device;
                await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                {
                    IDeviceRepository devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                    device = await devices.RegisterAsync(
                        connection.UserId,
                        command.DeviceId,
                        name,
                        command.Type,
                        now,
                        cancellationToken);
                    await devices.EvictAsync(
                        connection.UserId,
                        DeviceLimits.MaxDevicesPerProfile,
                        [.. session.Devices.Keys, command.DeviceId],
                        cancellationToken);
                }

                session.Devices[command.DeviceId] = new LiveDevice(
                    command.DeviceId,
                    connection.ConnectionId,
                    device.CustomName ?? device.ReportedName,
                    command.Type,
                    command.RemoteControl)
                {
                    TouchedAt = now,
                };
                session.Connections[connection.ConnectionId] = new ConnectionEntry(
                    connection.SessionId,
                    command.DeviceId);
                session.EmptySince = null;

                IReadOnlyList<DeviceView> list = await LoadDevicesAsync(session, cancellationToken);
                await notifier.DevicesChangedAsync(
                    session.DeviceConnectionIds(connection.ConnectionId),
                    list,
                    cancellationToken);
                return new RegisterResult(new DeviceSessionSnapshot(command.DeviceId, list, session.View(now)), []);
            },
            cancellationToken);
    }

    public Task DisconnectAsync(DeviceConnection connection, CancellationToken cancellationToken = default)
    {
        return WithExistingSessionAsync(
            connection.UserId,
            async session =>
            {
                await RemoveConnectionAsync(session, connection.ConnectionId, cancellationToken);
                return true;
            },
            false,
            cancellationToken);
    }

    public Task<CommandResult> SetRemoteControlAsync(
        DeviceConnection connection,
        bool allowed,
        CancellationToken cancellationToken = default)
    {
        return WithExistingSessionAsync(
            connection.UserId,
            async session =>
            {
                if (session.DeviceOf(connection.ConnectionId) is not { } device)
                {
                    return CommandResult.NotRegistered;
                }

                if (device.RemoteControl != allowed)
                {
                    device.RemoteControl = allowed;
                    await NotifyDevicesChangedAsync(session, cancellationToken);
                }

                return Accepted(session);
            },
            CommandResult.NotRegistered,
            cancellationToken);
    }

    public Task<PublishResult> PublishAsync(
        DeviceConnection connection,
        PublishPlaybackCommand command,
        CancellationToken cancellationToken = default)
    {
        return WithExistingSessionAsync(
            connection.UserId,
            async session =>
            {
                if (session.DeviceOf(connection.ConnectionId) is not { } device)
                {
                    return PublishResult.NotRegistered;
                }

                IReadOnlyList<DeviceValidationError> errors =
                    PlaybackValidator.Validate(command, session.Playback.Items.Count);
                if (errors.Count > 0)
                {
                    return new PublishResult(PublishStatus.Invalid, session.Version, session.QueueVersion, errors);
                }

                bool activeChanged = session.ActiveDeviceId != device.Id;
                if (activeChanged && !command.Claim)
                {
                    return new PublishResult(PublishStatus.NotActive, session.Version, session.QueueVersion, []);
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                var playback = LivePlayback.From(command.Playback, session.Playback, now);
                bool queueChanged = !playback.HasSameQueueAs(session.Playback);
                session.ActiveDeviceId = device.Id;
                session.Playback = playback;
                session.Version++;
                if (queueChanged)
                {
                    session.QueueVersion = session.Version;
                }

                await TouchIfDueAsync(session, device, now, cancellationToken);
                await notifier.PlaybackChangedAsync(
                    session.DeviceConnectionIds(connection.ConnectionId),
                    session.View(now),
                    cancellationToken);
                if (activeChanged)
                {
                    await NotifyDevicesChangedAsync(session, cancellationToken);
                }

                return new PublishResult(PublishStatus.Accepted, session.Version, session.QueueVersion, []);
            },
            PublishResult.NotRegistered,
            cancellationToken);
    }

    public async Task<CommandResult> SendCommandAsync(
        DeviceConnection connection,
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DeviceValidationError> errors = CommandValidator.Validate(command);
        if (errors.Count > 0)
        {
            return CommandResult.Invalid(errors);
        }

        IReadOnlyList<TrackRow>? tracks = null;
        int? startIndex = null;
        if (CommandValidator.UsesTracks(command.Type))
        {
            (tracks, int start) = await LoadTracksAsync(
                connection.UserId,
                command.TrackIds!,
                command.StartIndex ?? 0,
                cancellationToken);
            if (tracks.Count == 0)
            {
                return CommandResult.Invalid(
                    [new DeviceValidationError("trackIds", DeviceValidationCode.InvalidValue)]);
            }

            startIndex = command.Type == DeviceCommandType.PlayTracks ? start : null;
        }

        return await WithExistingSessionAsync(
            connection.UserId,
            async session =>
            {
                if (session.DeviceOf(connection.ConnectionId) is not { } caller)
                {
                    return CommandResult.NotRegistered;
                }

                if (command.TargetDeviceId == caller.Id)
                {
                    // A device acts on itself without the server.
                    return CommandResult.Invalid(
                        [new DeviceValidationError("targetDeviceId", DeviceValidationCode.InvalidValue)]);
                }

                if (FindTarget(session, command.TargetDeviceId) is not { } target)
                {
                    return Status(session, CommandStatus.DeviceNotFound);
                }

                if (!target.RemoteControl)
                {
                    return Status(session, CommandStatus.RemoteControlNotAllowed);
                }

                if (session.ActiveDeviceId != target.Id)
                {
                    return Status(session, CommandStatus.NotActive);
                }

                if (CommandValidator.UsesQueueVersion(command.Type))
                {
                    if (command.ExpectedQueueVersion != session.QueueVersion)
                    {
                        return Status(session, CommandStatus.Conflict);
                    }

                    IReadOnlyList<DeviceValidationError> positions =
                        ValidatePositions(command, session.Playback.Items.Count);
                    if (positions.Count > 0)
                    {
                        return CommandResult.Invalid(positions) with
                        {
                            Version = session.Version,
                            QueueVersion = session.QueueVersion,
                        };
                    }
                }

                ReceivedCommand received = new(
                    caller.Id,
                    command.Type,
                    session.Version,
                    command.ExpectedQueueVersion,
                    command.PositionMilliseconds,
                    command.Volume,
                    command.Muted,
                    command.Enabled,
                    command.Repeat,
                    tracks,
                    startIndex,
                    command.Type == DeviceCommandType.PlayTracks ? command.Source : null,
                    command.Type == DeviceCommandType.PlayTracks ? command.Shuffle : null,
                    command.Index,
                    command.ToIndex);
                await notifier.CommandReceivedAsync(target.ConnectionId, received, cancellationToken);
                await TouchIfDueAsync(session, caller, timeProvider.GetUtcNow(), cancellationToken);
                return Accepted(session);
            },
            CommandResult.NotRegistered,
            cancellationToken);
    }

    public async Task<CommandResult> TransferAsync(
        DeviceConnection connection,
        TransferPlaybackCommand command,
        CancellationToken cancellationToken = default)
    {
        if (DeviceRules.ValidateId(command.TargetDeviceId, "targetDeviceId") is { } invalid)
        {
            return CommandResult.Invalid([invalid]);
        }

        return await WithExistingSessionAsync(
            connection.UserId,
            async session =>
            {
                if (session.DeviceOf(connection.ConnectionId) is not { } caller)
                {
                    return CommandResult.NotRegistered;
                }

                if (FindTarget(session, command.TargetDeviceId) is not { } target)
                {
                    return Status(session, CommandStatus.DeviceNotFound);
                }

                if (target != caller && !target.RemoteControl)
                {
                    return Status(session, CommandStatus.RemoteControlNotAllowed);
                }

                if (command.ExpectedVersion is { } expected && expected != session.Version)
                {
                    return Status(session, CommandStatus.Conflict);
                }

                string? previous = session.ActiveDeviceId;
                if (previous == target.Id)
                {
                    return Accepted(session);
                }

                if (previous is not null
                    && previous != caller.Id
                    && session.Devices.TryGetValue(previous, out LiveDevice? active)
                    && !active.RemoteControl)
                {
                    // Taking playback away from an open device is controlling it.
                    return Status(session, CommandStatus.RemoteControlNotAllowed);
                }

                DateTimeOffset now = timeProvider.GetUtcNow();
                LivePlayback playback = session.Playback;
                int position = playback.PositionAt(now);
                if (playback.Items.Count > 0)
                {
                    await SaveStateAsync(session.UserId, playback, position, target, now, cancellationToken);
                }

                session.Playback = playback with
                {
                    Status = playback.CurrentIndex >= 0 ? LivePlaybackStatus.Paused : LivePlaybackStatus.Idle,
                    PositionMilliseconds = position,
                    MeasuredAt = now,
                };
                session.ActiveDeviceId = target.Id;
                session.Version++;
                logger.LogDebug(
                    "Playback of profile {UserId} moved from device {FromDeviceId} to {ToDeviceId}",
                    session.UserId,
                    previous,
                    target.Id);

                await TouchIfDueAsync(session, caller, now, cancellationToken);
                await notifier.PlaybackChangedAsync(
                    session.DeviceConnectionIds(),
                    session.View(now, new PlaybackHandover(previous, command.Play)),
                    cancellationToken);
                await NotifyDevicesChangedAsync(session, cancellationToken);
                return Accepted(session);
            },
            CommandResult.NotRegistered,
            cancellationToken);
    }

    public async Task<DeviceQueueResult> GetQueueAsync(
        DeviceConnection connection,
        CancellationToken cancellationToken = default)
    {
        (LivePlayback Playback, long Version, long QueueVersion)? copy
            = await WithExistingSessionAsync<(LivePlayback, long, long)?>(
                connection.UserId,
                session => Task.FromResult<(LivePlayback, long, long)?>(
                    session.DeviceOf(connection.ConnectionId) is null
                        ? null
                        : (session.Playback, session.Version, session.QueueVersion)),
                null,
                cancellationToken);
        if (copy is not { } live)
        {
            return new DeviceQueueResult(CommandStatus.NotRegistered, null);
        }

        IReadOnlyList<PlaybackStateItem> items = live.Playback.Items;
        Dictionary<Guid, TrackRow> tracks = items.Count == 0
            ? []
            : (await ListTracksAsync(
                    connection.UserId,
                    [.. items.Select(item => item.TrackId).Distinct()],
                    cancellationToken))
                .ToDictionary(track => track.Id);
        DeviceQueueView queue = new(
            live.Version,
            live.QueueVersion,
            live.Playback.CurrentIndex,
            live.Playback.Shuffle,
            live.Playback.Source,
            [.. items.Select((item, position) => new DeviceQueueEntry(
                position,
                item.TrackId,
                item.Autoplay,
                tracks.GetValueOrDefault(item.TrackId)))]);
        return new DeviceQueueResult(CommandStatus.Accepted, queue);
    }

    public async Task AbortSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        foreach (Guid userId in _sessions.Keys)
        {
            await WithExistingSessionAsync(
                userId,
                session => AbortConnectionsAsync(
                    session,
                    entry => entry.SessionId == sessionId,
                    cancellationToken),
                false,
                cancellationToken);
        }
    }

    public Task AbortUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return WithExistingSessionAsync(
            userId,
            session => AbortConnectionsAsync(session, _ => true, cancellationToken),
            false,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetConnectedSessionIdsAsync(
        CancellationToken cancellationToken = default)
    {
        HashSet<Guid> sessionIds = [];
        foreach (Guid userId in _sessions.Keys)
        {
            await WithExistingSessionAsync(
                userId,
                session =>
                {
                    sessionIds.UnionWith(session.Connections.Values.Select(entry => entry.SessionId));
                    return Task.FromResult(true);
                },
                false,
                cancellationToken);
        }

        return sessionIds;
    }

    public async Task RemoveIdleSessionsAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset limit = timeProvider.GetUtcNow() - DeviceLimits.IdleSessionLifetime;
        foreach (KeyValuePair<Guid, DeviceSession> entry in _sessions)
        {
            DeviceSession session = entry.Value;
            await session.Lock.WaitAsync(cancellationToken);
            try
            {
                if (!session.Removed
                    && session.Connections.Count == 0
                    && session.EmptySince is { } since
                    && since <= limit)
                {
                    session.Removed = true;
                    _sessions.TryRemove(entry);
                }
            }
            finally
            {
                session.Lock.Release();
            }
        }
    }

    /// <summary>
    /// The live part of the devices of the profile, for the list of its devices.
    /// </summary>
    internal Task<LiveDevices> GetLiveDevicesAsync(Guid userId, CancellationToken cancellationToken)
    {
        return WithExistingSessionAsync(
            userId,
            session => Task.FromResult(LiveOf(session)),
            LiveDevices.None,
            cancellationToken);
    }

    /// <summary>
    /// Gives a device of the profile a name, or removes it (<c>null</c>), and tells the open devices.
    /// </summary>
    internal Task<Device?> RenameAsync(
        Guid userId,
        string deviceId,
        string? customName,
        CancellationToken cancellationToken)
    {
        return WithSessionAsync(
            userId,
            async session =>
            {
                Device? device;
                await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                {
                    device = await scope.ServiceProvider.GetRequiredService<IDeviceRepository>()
                        .RenameAsync(userId, deviceId, customName, cancellationToken);
                }

                if (device is not null)
                {
                    if (session.Devices.TryGetValue(deviceId, out LiveDevice? live))
                    {
                        live.Name = device.CustomName ?? device.ReportedName;
                    }

                    await NotifyDevicesChangedAsync(session, cancellationToken);
                }

                return device;
            },
            cancellationToken);
    }

    /// <summary>
    /// Forgets a closed device of the profile and tells the open devices. Runs under the lock of the profile, so the
    /// device cannot open in between.
    /// </summary>
    internal Task<DeviceDeleteStatus> ForgetAsync(Guid userId, string deviceId, CancellationToken cancellationToken)
    {
        return WithSessionAsync(
            userId,
            async session =>
            {
                if (session.Devices.ContainsKey(deviceId))
                {
                    return DeviceDeleteStatus.Online;
                }

                bool deleted;
                await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
                {
                    deleted = await scope.ServiceProvider.GetRequiredService<IDeviceRepository>()
                        .DeleteAsync(userId, deviceId, cancellationToken);
                }

                if (!deleted)
                {
                    return DeviceDeleteStatus.NotFound;
                }

                await NotifyDevicesChangedAsync(session, cancellationToken);
                return DeviceDeleteStatus.Deleted;
            },
            cancellationToken);
    }

    private static LiveDevices LiveOf(DeviceSession session)
    {
        return new LiveDevices(
            session.Devices.Values.ToDictionary(device => device.Id, device => device.RemoteControl),
            session.ActiveDeviceId);
    }

    private static LiveDevice? FindTarget(DeviceSession session, string deviceId)
    {
        // Only the devices of the profile of the caller: a device of another profile is not found.
        return session.Devices.GetValueOrDefault(deviceId);
    }

    private static CommandResult Accepted(DeviceSession session)
    {
        return Status(session, CommandStatus.Accepted);
    }

    private static CommandResult Status(DeviceSession session, CommandStatus status)
    {
        return new CommandResult(status, session.Version, session.QueueVersion, []);
    }

    private static IReadOnlyList<DeviceValidationError> ValidatePositions(DeviceCommand command, int queueLength)
    {
        List<DeviceValidationError> errors = [];
        if (command.Type == DeviceCommandType.ClearQueue)
        {
            return errors;
        }

        if (command.Index is { } index && index >= queueLength)
        {
            errors.Add(new DeviceValidationError("index", DeviceValidationCode.OutOfRange, 0, queueLength - 1));
        }

        if (command.Type == DeviceCommandType.MoveQueueEntry
            && command.ToIndex is { } toIndex
            && toIndex >= queueLength)
        {
            errors.Add(new DeviceValidationError("toIndex", DeviceValidationCode.OutOfRange, 0, queueLength - 1));
        }

        return errors;
    }

    private static void AddIfInvalid(DeviceValidationError? error, List<DeviceValidationError> errors)
    {
        if (error is not null)
        {
            errors.Add(error);
        }
    }

    private async Task<T> WithSessionAsync<T>(
        Guid userId,
        Func<DeviceSession, Task<T>> work,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            DeviceSession session = _sessions.GetOrAdd(
                userId,
                static (id, now) => new DeviceSession(id, now),
                timeProvider.GetUtcNow());
            await session.Lock.WaitAsync(cancellationToken);
            try
            {
                if (!session.Removed)
                {
                    return await work(session);
                }
            }
            finally
            {
                session.Lock.Release();
            }
        }
    }

    private async Task<T> WithExistingSessionAsync<T>(
        Guid userId,
        Func<DeviceSession, Task<T>> work,
        T fallback,
        CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(userId, out DeviceSession? session))
        {
            return fallback;
        }

        await session.Lock.WaitAsync(cancellationToken);
        try
        {
            return session.Removed ? fallback : await work(session);
        }
        finally
        {
            session.Lock.Release();
        }
    }

    private async Task<bool> AbortConnectionsAsync(
        DeviceSession session,
        Func<ConnectionEntry, bool> predicate,
        CancellationToken cancellationToken)
    {
        string[] connectionIds = [.. session.Connections
            .Where(entry => predicate(entry.Value))
            .Select(entry => entry.Key)];
        foreach (string connectionId in connectionIds)
        {
            // Treated as closed at once, so the connection can do nothing until it is gone.
            await RemoveConnectionAsync(session, connectionId, cancellationToken);
            await notifier.AbortAsync(connectionId, cancellationToken);
        }

        return connectionIds.Length > 0;
    }

    private async Task RemoveConnectionAsync(
        DeviceSession session,
        string connectionId,
        CancellationToken cancellationToken)
    {
        if (!session.Connections.Remove(connectionId, out ConnectionEntry? entry))
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (entry.DeviceId is { } deviceId
            && await RemoveDeviceAsync(session, deviceId, connectionId, now, cancellationToken))
        {
            await NotifyDevicesChangedAsync(session, cancellationToken);
        }

        if (session.Connections.Count == 0)
        {
            session.EmptySince = now;
        }
    }

    /// <summary>
    /// Closes the device if this connection runs it, and notes its last activity. Returns whether it was closed.
    /// </summary>
    private async Task<bool> RemoveDeviceAsync(
        DeviceSession session,
        string deviceId,
        string connectionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!session.Devices.TryGetValue(deviceId, out LiveDevice? device) || device.ConnectionId != connectionId)
        {
            return false;
        }

        session.Devices.Remove(deviceId);
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDeviceRepository>()
            .TouchAsync(session.UserId, deviceId, now, cancellationToken);
        return true;
    }

    private async Task TouchIfDueAsync(
        DeviceSession session,
        LiveDevice device,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (device.TouchedAt is { } touchedAt && now - touchedAt < DeviceLimits.TouchInterval)
        {
            return;
        }

        device.TouchedAt = now;
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDeviceRepository>()
            .TouchAsync(session.UserId, device.Id, now, cancellationToken);
    }

    private async Task NotifyDevicesChangedAsync(DeviceSession session, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> connectionIds = session.DeviceConnectionIds();
        if (connectionIds.Count == 0)
        {
            return;
        }

        await notifier.DevicesChangedAsync(
            connectionIds,
            await LoadDevicesAsync(session, cancellationToken),
            cancellationToken);
    }

    private async Task<IReadOnlyList<DeviceView>> LoadDevicesAsync(
        DeviceSession session,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IReadOnlyList<Device> devices = await scope.ServiceProvider.GetRequiredService<IDeviceRepository>()
            .ListAsync(session.UserId, cancellationToken);
        return DeviceViews.Build(devices, LiveOf(session));
    }

    private async Task SaveStateAsync(
        Guid userId,
        LivePlayback playback,
        int position,
        LiveDevice target,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        PlaybackStateSaveStatus status = await scope.ServiceProvider.GetRequiredService<IPlaybackStateService>()
            .SaveAsync(
                userId,
                new SavePlaybackStateCommand(
                    playback.Items,
                    playback.CurrentIndex,
                    position,
                    playback.Shuffle,
                    playback.OriginalOrder,
                    playback.Repeat,
                    playback.Source,
                    target.Id,
                    target.Name,
                    now),
                cancellationToken);
        if (status != PlaybackStateSaveStatus.Saved)
        {
            logger.LogWarning(
                "The playback state of profile {UserId} was not saved on hand-over: {Status}",
                userId,
                status);
        }
    }

    private async Task<(IReadOnlyList<TrackRow> Tracks, int StartIndex)> LoadTracksAsync(
        Guid userId,
        IReadOnlyList<Guid> trackIds,
        int startIndex,
        CancellationToken cancellationToken)
    {
        var found = (await ListTracksAsync(userId, [.. trackIds.Distinct()], cancellationToken))
            .ToDictionary(track => track.Id);
        RemainingQueue remaining = PlaybackStateRules.Remaining(
            trackIds.Count,
            startIndex,
            [],
            [],
            position => found.ContainsKey(trackIds[position]));
        return (
            [.. remaining.Positions.Select(position => found[trackIds[position]])],
            Math.Max(remaining.CurrentIndex, 0));
    }

    private async Task<IReadOnlyList<TrackRow>> ListTracksAsync(
        Guid userId,
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ITrackRepository>()
            .ListByIdsAsync(trackIds, userId, cancellationToken);
    }
}
