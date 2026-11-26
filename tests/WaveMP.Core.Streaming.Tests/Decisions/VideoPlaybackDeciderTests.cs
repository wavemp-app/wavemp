using AwesomeAssertions;
using WaveMP.Core.Streaming.Decisions;
using WaveMP.Core.Streaming.Formats;
using WaveMP.Core.Streaming.Sources;

namespace WaveMP.Core.Streaming.Tests.Decisions;

[TestFixture]
public class VideoPlaybackDeciderTests
{
    private static readonly VideoCapabilities _browser = new(
        new HashSet<VideoContainer> { VideoContainer.Mp4, VideoContainer.Webm },
        new HashSet<VideoCodec> { VideoCodec.H264, VideoCodec.Vp9 },
        new HashSet<AudioCodec> { AudioCodec.Aac, AudioCodec.Opus });

    private static readonly VideoSource _h264Aac = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "/media/clip.mp4",
        VideoContainer.Mp4,
        VideoCodec.H264,
        AudioCodec.Aac,
        true,
        "yuv420p",
        1920,
        1080,
        48000,
        VideoContentTypes.Mp4,
        1000,
        DateTimeOffset.UnixEpoch,
        "tag",
        TimeSpan.FromMinutes(3),
        true);

    [Test]
    public void Decide_SupportedContainerAndCodecs_PlaysDirectly()
    {
        VideoPlaybackDecider.Decide(_h264Aac, _browser).Should().Be(VideoPlaybackDecision.DirectPlay);
    }

    [Test]
    public void Decide_VideoReadByAnOlderScan_IsTranscodedAsUnknown()
    {
        VideoPlaybackDecider.Decide(_h264Aac with { MetadataComplete = false }, _browser)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.FormatUnknown));
    }

    [Test]
    public void Decide_UnknownContainerWithAPlayablePicture_IsRemuxed()
    {
        VideoPlaybackDecider.Decide(_h264Aac with { Container = null }, _browser)
            .Should().Be(VideoPlaybackDecision.Remux(TranscodeReason.FormatUnknown, RemuxAudio.Copy));
    }

    [Test]
    public void Decide_UnknownContainerAndPicture_IsTranscodedAsUnknown()
    {
        VideoPlaybackDecider.Decide(_h264Aac with { Container = null, VideoCodec = null }, _browser)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.FormatUnknown));
    }

    [Test]
    public void Decide_UnsupportedContainerWithAPlayablePicture_IsRemuxedWithTheSound()
    {
        VideoPlaybackDecider.Decide(_h264Aac with { Container = VideoContainer.Matroska }, _browser)
            .Should().Be(VideoPlaybackDecision.Remux(TranscodeReason.ContainerUnsupported, RemuxAudio.Copy));
    }

    [Test]
    public void Decide_UnsupportedContainerAndSound_IsRemuxedWithAac()
    {
        VideoSource matroska = _h264Aac with { Container = VideoContainer.Matroska, AudioCodec = AudioCodec.Ac3 };

        VideoPlaybackDecider.Decide(matroska, _browser)
            .Should().Be(VideoPlaybackDecision.Remux(TranscodeReason.ContainerUnsupported, RemuxAudio.Aac));
    }

    [Test]
    public void Decide_UnsupportedContainerWithoutSound_IsRemuxedWithoutSound()
    {
        VideoSource silent = _h264Aac with { Container = VideoContainer.Mov, HasAudio = false, AudioCodec = null };

        VideoPlaybackDecider.Decide(silent, _browser)
            .Should().Be(VideoPlaybackDecision.Remux(TranscodeReason.ContainerUnsupported, RemuxAudio.None));
    }

    [Test]
    public void Decide_UnsupportedContainerOfAPictureMp4CannotHold_IsTranscoded()
    {
        VideoSource vp9 = _h264Aac with { Container = VideoContainer.Matroska, VideoCodec = VideoCodec.Vp9 };

        VideoPlaybackDecider.Decide(vp9, _browser)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.ContainerUnsupported));
    }

    [Test]
    public void Decide_UnsupportedContainerForAClientWithoutMp4_IsTranscoded()
    {
        VideoCapabilities webmOnly = _browser with { Containers = new HashSet<VideoContainer> { VideoContainer.Webm } };

        VideoPlaybackDecider.Decide(_h264Aac with { Container = VideoContainer.Matroska }, webmOnly)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.ContainerUnsupported));
    }

    [Test]
    public void Decide_UnsupportedContainerAndSoundForAClientWithoutAac_IsTranscoded()
    {
        VideoSource matroska = _h264Aac with { Container = VideoContainer.Matroska, AudioCodec = AudioCodec.Vorbis };
        VideoCapabilities opusOnly = _browser with { AudioCodecs = new HashSet<AudioCodec> { AudioCodec.Opus } };

        VideoPlaybackDecider.Decide(matroska, opusOnly)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.ContainerUnsupported));
    }

    [Test]
    public void Decide_UnsupportedContainerWithH264Of10Bit_IsTranscoded()
    {
        VideoSource tenBit = _h264Aac with { Container = VideoContainer.Matroska, PixelFormat = "yuv420p10le" };

        VideoPlaybackDecider.Decide(tenBit, _browser)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.ContainerUnsupported));
    }

    [Test]
    public void Decide_HevcInMatroskaForAClientThatPlaysHevc_IsRemuxed()
    {
        VideoSource hevc = _h264Aac with { Container = VideoContainer.Matroska, VideoCodec = VideoCodec.Hevc };
        VideoCapabilities safari = _browser with
        {
            VideoCodecs = new HashSet<VideoCodec> { VideoCodec.H264, VideoCodec.Hevc },
        };

        VideoPlaybackDecider.Decide(hevc, safari)
            .Should().Be(VideoPlaybackDecision.Remux(TranscodeReason.ContainerUnsupported, RemuxAudio.Copy));
    }

    [Test]
    public void Decide_UnsupportedOrUnknownVideoCodec_IsTranscoded()
    {
        VideoPlaybackDecider.Decide(_h264Aac with { VideoCodec = VideoCodec.Hevc }, _browser)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.VideoCodecUnsupported));
        VideoPlaybackDecider.Decide(_h264Aac with { VideoCodec = null }, _browser)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.VideoCodecUnsupported));
    }

    [TestCase("yuv420p10le")]
    [TestCase("yuv444p")]
    [TestCase(null)]
    public void Decide_H264OtherThan8Bit420_IsTranscoded(string? pixelFormat)
    {
        VideoPlaybackDecider.Decide(_h264Aac with { PixelFormat = pixelFormat }, _browser)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.VideoCodecUnsupported));
    }

    [Test]
    public void Decide_Vp9With10Bit_PlaysDirectly()
    {
        VideoSource vp9 = _h264Aac with
        {
            Container = VideoContainer.Webm,
            VideoCodec = VideoCodec.Vp9,
            AudioCodec = AudioCodec.Opus,
            PixelFormat = "yuv420p10le",
        };

        VideoPlaybackDecider.Decide(vp9, _browser).Should().Be(VideoPlaybackDecision.DirectPlay);
    }

    [Test]
    public void Decide_UnsupportedOrUnknownAudioCodec_IsRemuxedWithAac()
    {
        VideoPlaybackDecider.Decide(_h264Aac with { AudioCodec = AudioCodec.Ac3 }, _browser)
            .Should().Be(VideoPlaybackDecision.Remux(TranscodeReason.AudioCodecUnsupported, RemuxAudio.Aac));
        VideoPlaybackDecider.Decide(_h264Aac with { AudioCodec = null }, _browser)
            .Should().Be(VideoPlaybackDecision.Remux(TranscodeReason.AudioCodecUnsupported, RemuxAudio.Aac));
    }

    [Test]
    public void Decide_UnsupportedAudioCodecOfAWebmVideo_IsTranscoded()
    {
        VideoSource webm = _h264Aac with
        {
            Container = VideoContainer.Webm,
            VideoCodec = VideoCodec.Vp9,
            AudioCodec = AudioCodec.Vorbis,
        };

        VideoPlaybackDecider.Decide(webm, _browser)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.AudioCodecUnsupported));
    }

    [Test]
    public void Decide_VideoWithoutSound_IgnoresTheAudioCodecs()
    {
        VideoSource silent = _h264Aac with { HasAudio = false, AudioCodec = null };

        VideoPlaybackDecider.Decide(silent, _browser with { AudioCodecs = new HashSet<AudioCodec>() })
            .Should().Be(VideoPlaybackDecision.DirectPlay);
    }

    [Test]
    public void Decide_ClientWithoutCapabilities_AlwaysTranscodes()
    {
        VideoPlaybackDecider.Decide(_h264Aac, VideoCapabilities.None)
            .Should().Be(VideoPlaybackDecision.Transcode(TranscodeReason.ContainerUnsupported));
    }
}
