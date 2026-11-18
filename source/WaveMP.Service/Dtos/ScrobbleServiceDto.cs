using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// An online service that played songs are reported to (scrobbling).
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ScrobbleServiceDto>))]
public enum ScrobbleServiceDto
{
    /// <summary>
    /// Last.fm, connected through its web authentication: the client opens <c>authorizationUrl</c> with its callback
    /// URL as parameter <c>cb</c>; Last.fm sends the user back to the callback URL with the parameter <c>token</c>,
    /// which the client passes to ConnectMyScrobblingService.
    /// </summary>
    LastFm,

    /// <summary>
    /// ListenBrainz, connected with the personal user token from the settings of ListenBrainz.
    /// </summary>
    ListenBrainz,
}
