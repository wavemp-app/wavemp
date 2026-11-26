namespace WaveMP.Core.Streaming.Decisions;

/// <summary>
/// What a remux of a video does with its sound: the picture is always copied.
/// </summary>
public enum RemuxAudio
{
    /// <summary>
    /// The video has no sound.
    /// </summary>
    None,

    /// <summary>
    /// The sound is copied as it is; the client plays its codec.
    /// </summary>
    Copy,

    /// <summary>
    /// The sound is converted to AAC, which every client of MP4 plays; converting sound is quick.
    /// </summary>
    Aac,
}
