using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sort key of the review inbox. FirstSeenAt: when the track was found first (date added); Artist and Album keep the
/// tracks of an album in the order of disc and track number.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<TrackReviewSortDto>))]
public enum TrackReviewSortDto
{
    FirstSeenAt,
    Artist,
    Album,
    Title,
}
