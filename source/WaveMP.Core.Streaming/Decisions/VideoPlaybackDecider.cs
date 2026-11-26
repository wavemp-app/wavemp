using WaveMP.Core.Streaming.Formats;
using WaveMP.Core.Streaming.Sources;

namespace WaveMP.Core.Streaming.Decisions;

/// <summary>
/// Decides between direct play, a remux and transcoding of a video from its container and codecs and what the client
/// plays. The checks run from the outside in: container, picture, sound. A video whose picture the client plays but
/// whose container (or sound) it does not is remuxed into MP4: the picture is copied, which costs a fraction of the
/// time of a transcode and keeps its quality.
/// </summary>
public static class VideoPlaybackDecider
{
    /// <summary>
    /// Codecs of the picture a remux copies into MP4.
    /// </summary>
    public static readonly IReadOnlySet<VideoCodec> RemuxVideoCodecs =
        new HashSet<VideoCodec>([VideoCodec.H264, VideoCodec.Hevc, VideoCodec.Av1]);

    /// <summary>
    /// Codecs of the sound a remux copies into MP4; other sound is converted to AAC.
    /// </summary>
    public static readonly IReadOnlySet<AudioCodec> RemuxAudioCodecs = new HashSet<AudioCodec>(
        [AudioCodec.Aac, AudioCodec.Mp3, AudioCodec.Opus, AudioCodec.Flac, AudioCodec.Ac3, AudioCodec.Eac3]);

    public static VideoPlaybackDecision Decide(VideoSource source, VideoCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(capabilities);

        // Read by an older scan, the sound and the pixel format are unknown: HLS is safe.
        if (!source.MetadataComplete)
        {
            return VideoPlaybackDecision.Transcode(TranscodeReason.FormatUnknown);
        }

        bool picturePlays = source.VideoCodec is { } videoCodec
            && capabilities.VideoCodecs.Contains(videoCodec)
            && (videoCodec != VideoCodec.H264 || VideoFormats.IsEightBit420(source.PixelFormat));
        if (source.Container is not { } container || !capabilities.Containers.Contains(container))
        {
            TranscodeReason reason = source.Container is null
                ? TranscodeReason.FormatUnknown
                : TranscodeReason.ContainerUnsupported;
            return picturePlays && RemuxAudioOf(source, capabilities) is { } audio
                ? VideoPlaybackDecision.Remux(reason, audio)
                : VideoPlaybackDecision.Transcode(reason);
        }

        if (!picturePlays)
        {
            return VideoPlaybackDecision.Transcode(TranscodeReason.VideoCodecUnsupported);
        }

        if (source.HasAudio
            && (source.AudioCodec is not { } audioCodec || !capabilities.AudioCodecs.Contains(audioCodec)))
        {
            return RemuxAudioOf(source, capabilities) is { } audio
                ? VideoPlaybackDecision.Remux(TranscodeReason.AudioCodecUnsupported, audio)
                : VideoPlaybackDecision.Transcode(TranscodeReason.AudioCodecUnsupported);
        }

        return VideoPlaybackDecision.DirectPlay;
    }

    /// <summary>
    /// What a remux into MP4 does with the sound for the client, or <c>null</c> if the client cannot play a remux: it
    /// needs MP4 and the codec of the picture in MP4, and plays the sound as it is or AAC.
    /// </summary>
    public static RemuxAudio? RemuxAudioOf(VideoSource source, VideoCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(capabilities);
        if (!capabilities.Containers.Contains(VideoContainer.Mp4)
            || source.VideoCodec is not { } videoCodec
            || !RemuxVideoCodecs.Contains(videoCodec))
        {
            return null;
        }

        if (!source.HasAudio)
        {
            return RemuxAudio.None;
        }

        if (source.AudioCodec is { } audioCodec
            && RemuxAudioCodecs.Contains(audioCodec)
            && capabilities.AudioCodecs.Contains(audioCodec))
        {
            return RemuxAudio.Copy;
        }

        return capabilities.AudioCodecs.Contains(AudioCodec.Aac) ? RemuxAudio.Aac : null;
    }
}
