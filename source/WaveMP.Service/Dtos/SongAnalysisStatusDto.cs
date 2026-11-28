using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether the song analysis could read a file.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SongAnalysisStatusDto>))]
public enum SongAnalysisStatusDto
{
    Analyzed,

    /// <summary>
    /// The file has no sound.
    /// </summary>
    NoSound,

    /// <summary>
    /// The sound could not be read, e.g. a damaged file; the analysis tries again after the file changed.
    /// </summary>
    Failed,
}
