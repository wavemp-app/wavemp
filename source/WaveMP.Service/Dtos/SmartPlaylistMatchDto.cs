using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// How the rules of a smart playlist are combined.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SmartPlaylistMatchDto>))]
public enum SmartPlaylistMatchDto
{
    /// <summary>
    /// A song has to meet every rule.
    /// </summary>
    All,

    /// <summary>
    /// A song has to meet at least one rule.
    /// </summary>
    Any,
}
