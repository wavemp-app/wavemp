namespace WaveMP.Core.Transcoding.Profiles;

/// <summary>
/// What an HLS stream of a track carries.
/// </summary>
public enum HlsMedia
{
    /// <summary>
    /// Only the sound: of the audio file or, for tracks with a video only, of the video.
    /// </summary>
    Audio,

    /// <summary>
    /// The video with its sound.
    /// </summary>
    Video,

    /// <summary>
    /// Only the sound of the video, also for tracks with an audio file (the audio file for a video without sound):
    /// what a client plays while it shows the video. Transcoded like <see cref="Audio"/>.
    /// </summary>
    VideoSound,
}
