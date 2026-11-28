using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// How the song analysis found the song in a file.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SongSegmentMethodDto>))]
public enum SongSegmentMethodDto
{
    /// <summary>
    /// The whole file is the song: nothing to cut, not analyzed yet or nothing found.
    /// </summary>
    WholeFile,

    /// <summary>
    /// Aligned with the other recording of the track (the audio file); the most reliable method. Also finds parts the
    /// video cut out.
    /// </summary>
    Alignment,

    /// <summary>
    /// Aligned the onsets of the voice with the times of synchronized lyrics.
    /// </summary>
    Lyrics,

    /// <summary>
    /// Guessed from silence between intro, song and outro; the least reliable method, only a hint.
    /// </summary>
    Structure,
}
