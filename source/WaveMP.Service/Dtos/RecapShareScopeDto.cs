using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Which shared recaps to list.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<RecapShareScopeDto>))]
public enum RecapShareScopeDto
{
    /// <summary>
    /// The recaps the other profiles shared.
    /// </summary>
    Received,

    /// <summary>
    /// The recaps the current profile shared.
    /// </summary>
    Own,
}
