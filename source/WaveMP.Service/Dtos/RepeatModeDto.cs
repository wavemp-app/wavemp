using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Repeat mode of a player.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<RepeatModeDto>))]
public enum RepeatModeDto
{
    /// <summary>
    /// The queue plays once.
    /// </summary>
    Off,

    /// <summary>
    /// The queue starts again after its last entry.
    /// </summary>
    All,

    /// <summary>
    /// The current song plays again and again.
    /// </summary>
    One,
}
