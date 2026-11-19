using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sort key of the problems of the library. Path: media source, folder and file name; Artist: artist, album, disc and
/// track number; Title: title of the track; DetectedAt: when the check found the problem first.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LibraryIssueSortDto>))]
public enum LibraryIssueSortDto
{
    Path,
    Artist,
    Title,
    DetectedAt,
}
