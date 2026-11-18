using WaveMP.Core.Scrobbling.Connections;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class ScrobblingMappingExtensions
{
    public static ScrobblingServiceDto ToDto(this ScrobbleServiceState state)
    {
        ScrobbleConnection? connection = state.Connection;
        return new ScrobblingServiceDto
        {
            Service = state.Service.ToDto(),
            IsAvailable = state.IsAvailable,
            Status = connection?.Status switch
            {
                null => ScrobbleConnectionStatusDto.NotConnected,
                ScrobbleConnectionStatus.AuthorizationFailed => ScrobbleConnectionStatusDto.AuthorizationFailed,
                _ => ScrobbleConnectionStatusDto.Connected,
            },
            AccountName = connection?.AccountName,
            ConnectedAt = connection?.ConnectedAt,
            LastScrobbledAt = connection?.LastScrobbledAt,
            QueuedScrobbles = state.QueuedCount,
            AuthorizationUrl = state.AuthorizationUrl,
        };
    }

    public static ScrobblingDto ToDto(this IReadOnlyList<ScrobbleServiceState> states)
    {
        return new ScrobblingDto { Services = [.. states.Select(state => state.ToDto())] };
    }

    public static ScrobbleServiceDto ToDto(this ScrobbleService service)
    {
        return service switch
        {
            ScrobbleService.LastFm => ScrobbleServiceDto.LastFm,
            ScrobbleService.ListenBrainz => ScrobbleServiceDto.ListenBrainz,
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null),
        };
    }

    public static ScrobbleService ToEntity(this ScrobbleServiceDto service)
    {
        return service switch
        {
            ScrobbleServiceDto.LastFm => ScrobbleService.LastFm,
            ScrobbleServiceDto.ListenBrainz => ScrobbleService.ListenBrainz,
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, null),
        };
    }
}
