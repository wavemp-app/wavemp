using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// How lyrics follow the music.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LyricsTypeDto>))]
public enum LyricsTypeDto
{
    /// <summary>
    /// Every line has the time it starts.
    /// </summary>
    LineSynchronized,

    /// <summary>
    /// Every line has the time it starts, and lines with words have the times of their words (enhanced LRC).
    /// </summary>
    WordSynchronized,

    /// <summary>
    /// Plain lines without times.
    /// </summary>
    Static,
}
