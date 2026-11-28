namespace WaveMP.Core.Streaming.Sources;

/// <summary>
/// Which file a client wants to hear of a track.
/// </summary>
public enum SoundSource
{
    /// <summary>
    /// The audio file; the sound of the video for tracks without one. What clients play while they show the cover or
    /// the visualization.
    /// </summary>
    Audio,

    /// <summary>
    /// The sound of the video, complete with intro and outro; the audio file for videos without sound. What clients
    /// play while they show the video.
    /// </summary>
    Video,
}
