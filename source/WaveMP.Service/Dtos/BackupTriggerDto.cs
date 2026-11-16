using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// What created an export of the database.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<BackupTriggerDto>))]
public enum BackupTriggerDto
{
    /// <summary>
    /// The daily schedule.
    /// </summary>
    Schedule,

    /// <summary>
    /// An admin, through the API.
    /// </summary>
    Manual,

    /// <summary>
    /// The start of a new version, before it changed the database.
    /// </summary>
    Migration,
}
