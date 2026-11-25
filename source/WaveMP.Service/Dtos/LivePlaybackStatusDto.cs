using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What the player of the active device does.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LivePlaybackStatusDto>))]
public enum LivePlaybackStatusDto
{
    /// <summary>
    /// Nothing is loaded.
    /// </summary>
    Idle,

    /// <summary>
    /// A song is loading.
    /// </summary>
    Loading,

    /// <summary>
    /// A song plays.
    /// </summary>
    Playing,

    /// <summary>
    /// A song is paused.
    /// </summary>
    Paused,
}
