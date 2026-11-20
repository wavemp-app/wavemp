using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Length of the steps of the timeline of the statistics.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<TimelineGranularityDto>))]
public enum TimelineGranularityDto
{
    Day,

    /// <summary>
    /// Weeks from Monday to Sunday.
    /// </summary>
    Week,

    Month,

    Year,
}
