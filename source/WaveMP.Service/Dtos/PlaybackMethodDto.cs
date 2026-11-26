using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// How a client plays a track.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<PlaybackMethodDto>))]
public enum PlaybackMethodDto
{
    /// <summary>
    /// The client plays the file as it is from <c>streamUrl</c> (protocol <c>File</c>).
    /// </summary>
    DirectPlay,

    /// <summary>
    /// The client cannot play the file; the server converts it with ffmpeg into the HLS stream of <c>streamUrl</c>
    /// (protocol <c>Hls</c>).
    /// </summary>
    Transcode,

    /// <summary>
    /// Videos only: the client plays the codec of the picture but not the container (or not the codec of the sound);
    /// the server copies the picture into an MP4 file without converting it, delivered from <c>streamUrl</c> as a file
    /// (protocol <c>File</c>, <c>video/mp4</c>). The first request waits until the copy is ready.
    /// </summary>
    Remux,
}
