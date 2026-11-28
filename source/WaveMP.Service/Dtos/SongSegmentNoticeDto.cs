using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Why the song segment of a track needs a look of the admin.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SongSegmentNoticeDto>))]
public enum SongSegmentNoticeDto
{
    /// <summary>
    /// The analysis found intro or outro, but is not sure; the players keep the whole file until the admin corrects
    /// it.
    /// </summary>
    LowConfidence,

    /// <summary>
    /// The audio file and the video are different recordings (e.g. a live version or a remix), so the players do not
    /// map positions between them.
    /// </summary>
    Mismatch,
}
