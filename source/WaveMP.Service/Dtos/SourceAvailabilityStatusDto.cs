using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether the files of a media source can be organized: Available, ReadOnly (the folder cannot be written, e.g. a
/// read-only mount), Missing (the folder does not exist) or Inactive (the source is switched off).
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SourceAvailabilityStatusDto>))]
public enum SourceAvailabilityStatusDto
{
    Available,

    ReadOnly,

    Missing,

    Inactive,
}
