namespace WaveMP.Core.Streaming.Decisions;

/// <summary>
/// Whether a client plays a video directly, as a remuxed MP4 file or transcoded.
/// </summary>
/// <param name="Method">Direct play, remux or transcoding.</param>
/// <param name="Reason">Why the video cannot play as it is; <c>null</c> for direct play.</param>
/// <param name="Audio">What the remux does with the sound; <c>null</c> unless the method is
/// <see cref="PlaybackMethod.Remux"/>.</param>
public sealed record VideoPlaybackDecision(PlaybackMethod Method, TranscodeReason? Reason, RemuxAudio? Audio = null)
{
    public static VideoPlaybackDecision DirectPlay { get; } = new(PlaybackMethod.DirectPlay, null);

    public static VideoPlaybackDecision Transcode(TranscodeReason reason)
    {
        return new VideoPlaybackDecision(PlaybackMethod.Transcode, reason);
    }

    public static VideoPlaybackDecision Remux(TranscodeReason reason, RemuxAudio audio)
    {
        return new VideoPlaybackDecision(PlaybackMethod.Remux, reason, audio);
    }
}
