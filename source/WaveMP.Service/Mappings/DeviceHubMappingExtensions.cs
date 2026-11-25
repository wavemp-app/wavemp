using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Devices.Validations;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

/// <summary>
/// Maps the messages of the real-time connection of the devices (asyncapi.yaml). Members a client left out or set to
/// null are passed on as they are; the coordinator checks them and answers with validation codes.
/// </summary>
public static class DeviceHubMappingExtensions
{
    public static RegisterDeviceCommand ToCommand(this RegisterDeviceDto request)
    {
        return new RegisterDeviceCommand(
            request.DeviceId,
            request.Name,
            request.Type.ToEntity(),
            request.RemoteControl);
    }

    public static PublishPlaybackCommand ToCommand(this PublishPlaybackDto request)
    {
        LivePlaybackInputDto? playback = request.Playback;
        return new PublishPlaybackCommand(request.Claim, playback is null ? null! : ToInput(playback));
    }

    public static DeviceCommand ToCommand(this DeviceCommandDto request)
    {
        return new DeviceCommand(
            request.TargetDeviceId,
            request.Type.ToEntity(),
            request.ExpectedQueueVersion,
            request.PositionMilliseconds,
            request.Volume,
            request.Muted,
            request.Enabled,
            request.Repeat?.ToEntity(),
            request.TrackIds,
            request.StartIndex,
            ToSource(request.Source),
            request.Shuffle,
            request.Index,
            request.ToIndex);
    }

    public static TransferPlaybackCommand ToCommand(this TransferPlaybackDto request)
    {
        return new TransferPlaybackCommand(request.TargetDeviceId, request.Play, request.ExpectedVersion);
    }

    public static RegisterResultDto ToDto(this RegisterResult result)
    {
        return new RegisterResultDto
        {
            Status = result.IsRegistered ? RegisterStatusDto.Registered : RegisterStatusDto.Invalid,
            Snapshot = result.Snapshot?.ToDto(),
            Errors = result.Errors.ToDtos(),
        };
    }

    public static DeviceSessionSnapshotDto ToDto(this DeviceSessionSnapshot snapshot)
    {
        return new DeviceSessionSnapshotDto
        {
            DeviceId = snapshot.DeviceId,
            Devices = snapshot.Devices.ToDtos(),
            Playback = snapshot.Playback.ToDto(),
        };
    }

    public static DeviceListDto ToListDto(this IReadOnlyList<DeviceView> devices)
    {
        return new DeviceListDto { Devices = devices.ToDtos() };
    }

    public static LivePlaybackDto ToDto(this LivePlaybackView playback)
    {
        return new LivePlaybackDto
        {
            Version = playback.Version,
            QueueVersion = playback.QueueVersion,
            ActiveDeviceId = playback.ActiveDeviceId,
            Status = playback.Status.ToDto(),
            CurrentIndex = playback.CurrentIndex,
            CurrentTrackId = playback.CurrentTrackId,
            QueueLength = playback.QueueLength,
            PositionMilliseconds = playback.PositionMilliseconds,
            DurationMilliseconds = playback.DurationMilliseconds,
            Shuffle = playback.Shuffle,
            Repeat = playback.Repeat.ToDto(),
            Source = playback.Source?.ToDto(),
            Volume = playback.Volume,
            Muted = playback.Muted,
            Crossfade = playback.Crossfade,
            Autoplay = playback.Autoplay,
            Handover = playback.Handover is { } handover
                ? new PlaybackHandoverDto { FromDeviceId = handover.FromDeviceId, Play = handover.Play }
                : null,
        };
    }

    public static ReceivedCommandDto ToDto(this ReceivedCommand command)
    {
        return new ReceivedCommandDto
        {
            FromDeviceId = command.FromDeviceId,
            Type = command.Type.ToDto(),
            Version = command.Version,
            ExpectedQueueVersion = command.ExpectedQueueVersion,
            PositionMilliseconds = command.PositionMilliseconds,
            Volume = command.Volume,
            Muted = command.Muted,
            Enabled = command.Enabled,
            Repeat = command.Repeat?.ToDto(),
            Tracks = command.Tracks is { } tracks ? [.. tracks.Select(track => track.ToDto())] : null,
            StartIndex = command.StartIndex,
            Source = command.Source?.ToDto(),
            Shuffle = command.Shuffle,
            Index = command.Index,
            ToIndex = command.ToIndex,
        };
    }

    public static DeviceQueueResultDto ToDto(this DeviceQueueResult result)
    {
        return new DeviceQueueResultDto { Status = result.Status.ToDto(), Queue = result.Queue?.ToDto() };
    }

    public static DeviceQueueDto ToDto(this DeviceQueueView queue)
    {
        return new DeviceQueueDto
        {
            Version = queue.Version,
            QueueVersion = queue.QueueVersion,
            CurrentIndex = queue.CurrentIndex,
            Shuffle = queue.Shuffle,
            Source = queue.Source?.ToDto(),
            Items = [.. queue.Items.Select(entry => new DeviceQueueEntryDto
            {
                Position = entry.Position,
                TrackId = entry.TrackId,
                Autoplay = entry.Autoplay,
                Track = entry.Track?.ToDto(),
            })],
        };
    }

    public static CommandResultDto ToDto(this CommandResult result)
    {
        return new CommandResultDto
        {
            Status = result.Status.ToDto(),
            Version = result.Version,
            QueueVersion = result.QueueVersion,
            Errors = result.Errors.ToDtos(),
        };
    }

    public static PublishResultDto ToDto(this PublishResult result)
    {
        return new PublishResultDto
        {
            Status = result.Status.ToDto(),
            Version = result.Version,
            QueueVersion = result.QueueVersion,
            Errors = result.Errors.ToDtos(),
        };
    }

    public static IReadOnlyList<ValidationErrorDto> ToDtos(this IReadOnlyList<DeviceValidationError> errors)
    {
        return [.. errors.Select(error => new ValidationErrorDto
        {
            Field = error.Field,
            Code = error.Code.ToDto(),
            Min = error.Min is { } min ? checked((int)min) : null,
            Max = error.Max is { } max ? checked((int)max) : null,
        })];
    }

    public static PlaybackSourceDto ToDto(this PlaybackSource source)
    {
        return new PlaybackSourceDto { Kind = source.Kind.ToDto(), Id = source.Id };
    }

    public static LivePlaybackStatusDto ToDto(this LivePlaybackStatus status)
    {
        return status switch
        {
            LivePlaybackStatus.Idle => LivePlaybackStatusDto.Idle,
            LivePlaybackStatus.Loading => LivePlaybackStatusDto.Loading,
            LivePlaybackStatus.Playing => LivePlaybackStatusDto.Playing,
            LivePlaybackStatus.Paused => LivePlaybackStatusDto.Paused,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown playback status."),
        };
    }

    public static LivePlaybackStatus ToEntity(this LivePlaybackStatusDto status)
    {
        return status switch
        {
            LivePlaybackStatusDto.Idle => LivePlaybackStatus.Idle,
            LivePlaybackStatusDto.Loading => LivePlaybackStatus.Loading,
            LivePlaybackStatusDto.Playing => LivePlaybackStatus.Playing,
            LivePlaybackStatusDto.Paused => LivePlaybackStatus.Paused,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown playback status."),
        };
    }

    public static DeviceCommandTypeDto ToDto(this DeviceCommandType type)
    {
        return type switch
        {
            DeviceCommandType.Play => DeviceCommandTypeDto.Play,
            DeviceCommandType.Pause => DeviceCommandTypeDto.Pause,
            DeviceCommandType.Stop => DeviceCommandTypeDto.Stop,
            DeviceCommandType.Next => DeviceCommandTypeDto.Next,
            DeviceCommandType.Previous => DeviceCommandTypeDto.Previous,
            DeviceCommandType.Seek => DeviceCommandTypeDto.Seek,
            DeviceCommandType.SetVolume => DeviceCommandTypeDto.SetVolume,
            DeviceCommandType.SetShuffle => DeviceCommandTypeDto.SetShuffle,
            DeviceCommandType.SetRepeat => DeviceCommandTypeDto.SetRepeat,
            DeviceCommandType.SetCrossfade => DeviceCommandTypeDto.SetCrossfade,
            DeviceCommandType.SetAutoplay => DeviceCommandTypeDto.SetAutoplay,
            DeviceCommandType.PlayTracks => DeviceCommandTypeDto.PlayTracks,
            DeviceCommandType.AddToQueue => DeviceCommandTypeDto.AddToQueue,
            DeviceCommandType.PlayNext => DeviceCommandTypeDto.PlayNext,
            DeviceCommandType.PlayQueueEntry => DeviceCommandTypeDto.PlayQueueEntry,
            DeviceCommandType.RemoveQueueEntry => DeviceCommandTypeDto.RemoveQueueEntry,
            DeviceCommandType.MoveQueueEntry => DeviceCommandTypeDto.MoveQueueEntry,
            DeviceCommandType.ClearQueue => DeviceCommandTypeDto.ClearQueue,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown command."),
        };
    }

    public static DeviceCommandType ToEntity(this DeviceCommandTypeDto type)
    {
        return type switch
        {
            DeviceCommandTypeDto.Play => DeviceCommandType.Play,
            DeviceCommandTypeDto.Pause => DeviceCommandType.Pause,
            DeviceCommandTypeDto.Stop => DeviceCommandType.Stop,
            DeviceCommandTypeDto.Next => DeviceCommandType.Next,
            DeviceCommandTypeDto.Previous => DeviceCommandType.Previous,
            DeviceCommandTypeDto.Seek => DeviceCommandType.Seek,
            DeviceCommandTypeDto.SetVolume => DeviceCommandType.SetVolume,
            DeviceCommandTypeDto.SetShuffle => DeviceCommandType.SetShuffle,
            DeviceCommandTypeDto.SetRepeat => DeviceCommandType.SetRepeat,
            DeviceCommandTypeDto.SetCrossfade => DeviceCommandType.SetCrossfade,
            DeviceCommandTypeDto.SetAutoplay => DeviceCommandType.SetAutoplay,
            DeviceCommandTypeDto.PlayTracks => DeviceCommandType.PlayTracks,
            DeviceCommandTypeDto.AddToQueue => DeviceCommandType.AddToQueue,
            DeviceCommandTypeDto.PlayNext => DeviceCommandType.PlayNext,
            DeviceCommandTypeDto.PlayQueueEntry => DeviceCommandType.PlayQueueEntry,
            DeviceCommandTypeDto.RemoveQueueEntry => DeviceCommandType.RemoveQueueEntry,
            DeviceCommandTypeDto.MoveQueueEntry => DeviceCommandType.MoveQueueEntry,
            DeviceCommandTypeDto.ClearQueue => DeviceCommandType.ClearQueue,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown command."),
        };
    }

    public static CommandStatusDto ToDto(this CommandStatus status)
    {
        return status switch
        {
            CommandStatus.Accepted => CommandStatusDto.Accepted,
            CommandStatus.NotRegistered => CommandStatusDto.NotRegistered,
            CommandStatus.DeviceNotFound => CommandStatusDto.DeviceNotFound,
            CommandStatus.RemoteControlNotAllowed => CommandStatusDto.RemoteControlNotAllowed,
            CommandStatus.NotActive => CommandStatusDto.NotActive,
            CommandStatus.Conflict => CommandStatusDto.Conflict,
            CommandStatus.Invalid => CommandStatusDto.Invalid,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown command status."),
        };
    }

    public static PublishStatusDto ToDto(this PublishStatus status)
    {
        return status switch
        {
            PublishStatus.Accepted => PublishStatusDto.Accepted,
            PublishStatus.NotRegistered => PublishStatusDto.NotRegistered,
            PublishStatus.NotActive => PublishStatusDto.NotActive,
            PublishStatus.Invalid => PublishStatusDto.Invalid,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown publish status."),
        };
    }

    private static IReadOnlyList<DeviceDto> ToDtos(this IReadOnlyList<DeviceView> devices)
    {
        return [.. devices.Select(device => device.ToDto())];
    }

    private static LivePlaybackInput ToInput(LivePlaybackInputDto playback)
    {
        LiveQueueInputDto? queue = playback.Queue;
        return new LivePlaybackInput(
            playback.Status.ToEntity(),
            queue is null
                ? null
                : new LiveQueueInput(
                    queue.Items is null
                        ? null!
                        : [.. queue.Items.Select(item => item is null
                            ? null!
                            : new PlaybackStateItem(item.TrackId, item.Autoplay))],
                    queue.Shuffle,
                    queue.OriginalOrder ?? [],
                    ToSource(queue.Source)),
            playback.CurrentIndex,
            playback.PositionMilliseconds,
            playback.DurationMilliseconds,
            playback.Repeat.ToEntity(),
            playback.Volume,
            playback.Muted,
            playback.Crossfade,
            playback.Autoplay);
    }

    /// <summary>
    /// The source of a list; a source without a kind is checked before (see <c>DeviceHub</c>).
    /// </summary>
    private static PlaybackSource? ToSource(PlaybackSourceDto? source)
    {
        return source is { Kind: { } kind } ? new PlaybackSource(kind.ToEntity(), source.Id) : null;
    }
}
