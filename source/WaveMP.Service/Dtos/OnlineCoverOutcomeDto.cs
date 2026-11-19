using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Where an applied cover was stored.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OnlineCoverOutcomeDto>))]
public enum OnlineCoverOutcomeDto
{
    /// <summary>
    /// No cover was applied.
    /// </summary>
    None,

    /// <summary>
    /// As picture in the media folder, named after the cover rules (e.g. <c>cover.jpg</c> in the folder of the album
    /// or <c>Song.jpg</c> next to the song); a picture of the same name was replaced.
    /// </summary>
    MediaFolder,

    /// <summary>
    /// In the data folder, because the media folder cannot be written; only WaveMP shows it.
    /// </summary>
    DataFolder,

    /// <summary>
    /// Neither the media folder nor the data folder could be written.
    /// </summary>
    NotSaved,
}
