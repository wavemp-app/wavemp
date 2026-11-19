using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// How much a problem of the library matters: Problem should be fixed, Hint is only a hint (videos without audio,
/// songs without video).
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LibraryIssueSeverityDto>))]
public enum LibraryIssueSeverityDto
{
    Problem,
    Hint,
}
