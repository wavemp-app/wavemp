using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sort key of the overview of the file selection. Path sorts by folder and file name; FileCount by the total number
/// of audio files, video files and covers.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<FileSelectionSortDto>))]
public enum FileSelectionSortDto
{
    Path,
    Title,
    FileCount,
}
