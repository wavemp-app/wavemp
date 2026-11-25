using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Hubs;

/// <summary>
/// The real-time connection of the devices of a profile (asyncapi.yaml, ASP.NET Core SignalR with the JSON protocol).
/// Every open instance of WaveMP registers as a device; the active device publishes the state of its player, the
/// others show it and send commands to it or move playback over. A connection only sees and controls the devices of
/// the profile of its session. The hub is thin: it maps the messages and leaves every rule to
/// <see cref="IDeviceCoordinator"/>. The method names are the targets of the invocations.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.DeviceHub)]
public sealed class DeviceHub(IDeviceCoordinator coordinator, DeviceHubConnections connections)
    : Hub<IDeviceHubClient>
{
    private DeviceConnection Connection => new(
        Context.User!.GetUserId(),
        Context.User!.GetSessionId(),
        Context.ConnectionId);

    public override Task OnConnectedAsync()
    {
        DeviceConnection connection = Connection;
        connections.Add(Context, connection.UserId, connection.SessionId);
        return Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        // The connection is gone, so its token is cancelled already; the device must be closed anyway.
        await coordinator.DisconnectAsync(Connection, CancellationToken.None);
    }

    /// <summary>
    /// Registers the device of the connection; the first call of every connection.
    /// </summary>
    [HubMethodName("Register")]
    public async Task<RegisterResultDto> RegisterAsync(RegisterDeviceDto? request)
    {
        if (request is null)
        {
            return new RegisterResultDto
            {
                Status = RegisterStatusDto.Invalid,
                Snapshot = null,
                Errors = [MissingArgument()],
            };
        }

        try
        {
            RegisterResult result = await coordinator.RegisterAsync(
                Connection,
                request.ToCommand(),
                Context.ConnectionAborted);
            return result.ToDto();
        }
        catch (UserNotFoundException)
        {
            // The profile was deleted after the session was checked.
            Context.Abort();
            throw new HubException(DeviceHubDefaults.SessionEndedError);
        }
    }

    /// <summary>
    /// The device of the connection allows or forbids remote control, for the running page only.
    /// </summary>
    [HubMethodName("SetRemoteControl")]
    public async Task<CommandResultDto> SetRemoteControlAsync(SetRemoteControlDto? request)
    {
        if (request is null)
        {
            return InvalidCommand(MissingArgument());
        }

        return (await coordinator.SetRemoteControlAsync(Connection, request.Allowed, Context.ConnectionAborted))
            .ToDto();
    }

    /// <summary>
    /// The device of the connection publishes the state of its player.
    /// </summary>
    [HubMethodName("PublishPlayback")]
    public async Task<PublishResultDto> PublishPlaybackAsync(PublishPlaybackDto? request)
    {
        ValidationErrorDto? missing = request is null
            ? MissingArgument()
            : request.Playback?.Queue?.Source is { Kind: null }
                ? Required("playback.queue.source.kind")
                : null;
        if (missing is not null)
        {
            return new PublishResultDto
            {
                Status = PublishStatusDto.Invalid,
                Version = 0,
                QueueVersion = 0,
                Errors = [missing],
            };
        }

        return (await coordinator.PublishAsync(Connection, request!.ToCommand(), Context.ConnectionAborted)).ToDto();
    }

    /// <summary>
    /// Passes a command on to the active device of the profile, if it allows remote control.
    /// </summary>
    [HubMethodName("SendCommand")]
    public async Task<CommandResultDto> SendCommandAsync(DeviceCommandDto? request)
    {
        ValidationErrorDto? missing = request is null
            ? MissingArgument()
            : request.Source is { Kind: null }
                ? Required("source.kind")
                : null;
        if (missing is not null)
        {
            return InvalidCommand(missing);
        }

        return (await coordinator.SendCommandAsync(Connection, request!.ToCommand(), Context.ConnectionAborted))
            .ToDto();
    }

    /// <summary>
    /// Moves playback to another device of the profile, or to the device of the connection, at the same position.
    /// </summary>
    [HubMethodName("TransferPlayback")]
    public async Task<CommandResultDto> TransferPlaybackAsync(TransferPlaybackDto? request)
    {
        if (request is null)
        {
            return InvalidCommand(MissingArgument());
        }

        return (await coordinator.TransferAsync(Connection, request.ToCommand(), Context.ConnectionAborted)).ToDto();
    }

    /// <summary>
    /// The live queue of the profile with its songs.
    /// </summary>
    [HubMethodName("GetQueue")]
    public async Task<DeviceQueueResultDto> GetQueueAsync()
    {
        return (await coordinator.GetQueueAsync(Connection, Context.ConnectionAborted)).ToDto();
    }

    /// <summary>
    /// The argument of the call is null; like a request without a body, the field is empty.
    /// </summary>
    private static ValidationErrorDto MissingArgument()
    {
        return Required(string.Empty);
    }

    private static ValidationErrorDto Required(string field)
    {
        return new ValidationErrorDto { Field = field, Code = ValidationErrorCodeDto.Required };
    }

    private static CommandResultDto InvalidCommand(ValidationErrorDto error)
    {
        return new CommandResultDto
        {
            Status = CommandStatusDto.Invalid,
            Version = 0,
            QueueVersion = 0,
            Errors = [error],
        };
    }
}
