using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A field of the tags that the review inbox checks; a track lists those its file lacks.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<MetadataTagDto>))]
public enum MetadataTagDto
{
    Title,
    Artist,
    Album,
    Year,
    TrackNumber,
    Genre,
}
