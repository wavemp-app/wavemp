using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class PlaybackStateMappingExtensions
{
    public static PlaybackStateDto ToDto(this PlaybackStateView state)
    {
        return new PlaybackStateDto
        {
            Items = [.. state.Items.Select(item => new PlaybackStateItemDto
            {
                Track = item.Track.ToDto(),
                Autoplay = item.Autoplay,
            })],
            CurrentIndex = state.CurrentIndex,
            PositionMilliseconds = state.PositionMilliseconds,
            Shuffle = state.Shuffle,
            OriginalOrder = state.OriginalOrder,
            Repeat = state.Repeat.ToDto(),
            Source = state.Source is { } source
                ? new PlaybackSourceDto { Kind = source.Kind.ToDto(), Id = source.Id }
                : null,
            Device = new PlaybackDeviceDto { Id = state.DeviceId, Name = state.DeviceName },
            ChangedAt = state.ChangedAt,
        };
    }

    /// <summary>
    /// The command of a validated request: missing optional values get their defaults.
    /// </summary>
    public static SavePlaybackStateCommand ToCommand(this UpdatePlaybackStateDto request)
    {
        return new SavePlaybackStateCommand(
            [.. request.Items.Select(item => new PlaybackStateItem(item.TrackId, item.Autoplay ?? false))],
            request.CurrentIndex ?? -1,
            request.PositionMilliseconds ?? 0,
            request.Shuffle ?? false,
            request.OriginalOrder ?? [],
            (request.Repeat ?? RepeatModeDto.Off).ToEntity(),
            request.Source is { Kind: { } kind } source ? new PlaybackSource(kind.ToEntity(), source.Id) : null,
            request.Device.Id,
            request.Device.Name,
            request.ChangedAt);
    }

    public static RepeatModeDto ToDto(this RepeatMode mode)
    {
        return mode switch
        {
            RepeatMode.Off => RepeatModeDto.Off,
            RepeatMode.All => RepeatModeDto.All,
            RepeatMode.One => RepeatModeDto.One,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown repeat mode."),
        };
    }

    public static RepeatMode ToEntity(this RepeatModeDto mode)
    {
        return mode switch
        {
            RepeatModeDto.Off => RepeatMode.Off,
            RepeatModeDto.All => RepeatMode.All,
            RepeatModeDto.One => RepeatMode.One,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown repeat mode."),
        };
    }

    public static PlaybackSourceKindDto ToDto(this PlaybackSourceKind kind)
    {
        return kind switch
        {
            PlaybackSourceKind.Album => PlaybackSourceKindDto.Album,
            PlaybackSourceKind.Artist => PlaybackSourceKindDto.Artist,
            PlaybackSourceKind.Playlist => PlaybackSourceKindDto.Playlist,
            PlaybackSourceKind.SmartPlaylist => PlaybackSourceKindDto.SmartPlaylist,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown source."),
        };
    }

    public static PlaybackSourceKind ToEntity(this PlaybackSourceKindDto kind)
    {
        return kind switch
        {
            PlaybackSourceKindDto.Album => PlaybackSourceKind.Album,
            PlaybackSourceKindDto.Artist => PlaybackSourceKind.Artist,
            PlaybackSourceKindDto.Playlist => PlaybackSourceKind.Playlist,
            PlaybackSourceKindDto.SmartPlaylist => PlaybackSourceKind.SmartPlaylist,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown source."),
        };
    }
}
