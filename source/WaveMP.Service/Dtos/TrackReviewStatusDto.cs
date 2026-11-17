using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether an admin checked the metadata of a track. Every track starts Unreviewed, also tracks found by a scan later.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<TrackReviewStatusDto>))]
public enum TrackReviewStatusDto
{
    Unreviewed,
    Reviewed,
}
