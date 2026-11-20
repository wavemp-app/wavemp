using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// How the period of a recap was chosen. The days of a shared recap are fixed when it is shared.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<RecapPeriodDto>))]
public enum RecapPeriodDto
{
    /// <summary>
    /// The last month.
    /// </summary>
    LastMonth,

    /// <summary>
    /// The last three months.
    /// </summary>
    LastThreeMonths,

    /// <summary>
    /// The last twelve months.
    /// </summary>
    LastYear,

    /// <summary>
    /// The current calendar year up to today.
    /// </summary>
    CurrentYear,

    /// <summary>
    /// All plays.
    /// </summary>
    AllTime,

    /// <summary>
    /// Days the profile chose.
    /// </summary>
    Custom,
}
