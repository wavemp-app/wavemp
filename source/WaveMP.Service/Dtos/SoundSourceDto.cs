using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Which recording of a track a playback of its sound plays.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SoundSourceDto>))]
public enum SoundSourceDto
{
    /// <summary>
    /// The audio file; the sound of the video for tracks without one. The cover and the visualization play it.
    /// </summary>
    Audio,

    /// <summary>
    /// The sound of the video with its intro and outro, transcoded to AAC; the audio file for tracks without a video
    /// or with a video without sound. The video view plays it, so picture and sound belong together.
    /// </summary>
    Video,
}
