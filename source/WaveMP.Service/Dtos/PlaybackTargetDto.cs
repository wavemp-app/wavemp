using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Who plays the stream of a playback: the client itself or a casting device the client hands the URLs to.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<PlaybackTargetDto>))]
public enum PlaybackTargetDto
{
    /// <summary>
    /// The client itself, e.g. an audio or video element of the web app.
    /// </summary>
    Local,

    /// <summary>
    /// A casting device (Chromecast, AirPlay): the stream URL stays valid longer (the duration of the track plus a
    /// longer buffer), and the answer also carries a signed URL of the cover.
    /// </summary>
    Cast,
}
