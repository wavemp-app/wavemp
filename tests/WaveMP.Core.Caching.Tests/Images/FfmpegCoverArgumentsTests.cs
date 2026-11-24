using AwesomeAssertions;
using WaveMP.Core.Caching.Images;

namespace WaveMP.Core.Caching.Tests.Images;

[TestFixture]
public class FfmpegCoverArgumentsTests
{
    [TestCase("image/jpeg", "jpeg_pipe")]
    [TestCase("image/png", "png_pipe")]
    [TestCase("image/webp", "webp_pipe")]
    [TestCase("image/gif", "gif")]
    [TestCase("image/bmp", "bmp_pipe")]
    [TestCase("image/svg+xml", null)]
    public void DemuxerOf_ContentType_ChoosesTheDemuxerOfTheFormat(string contentType, string? demuxer)
    {
        FfmpegCoverArguments.DemuxerOf(contentType).Should().Be(demuxer);
    }

    [Test]
    public void Build_TwoTargets_SplitsTheInputAndWritesOneWebPPerTarget()
    {
        IReadOnlyList<string> arguments = FfmpegCoverArguments.Build(
            "jpeg_pipe",
            [new CoverEncodeTarget(96, "small.webp"), new CoverEncodeTarget(300, "medium.webp")],
            80);

        arguments.Should().Equal(
            "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
            "-f", "jpeg_pipe", "-i", "pipe:0",
            "-filter_complex",
            "[0:v]split=2[s0][s1]"
            + ";[s0]scale='min(iw,96)':'min(ih,96)':force_original_aspect_ratio=decrease:flags=lanczos[o0]"
            + ";[s1]scale='min(iw,300)':'min(ih,300)':force_original_aspect_ratio=decrease:flags=lanczos[o1]",
            "-map", "[o0]", "-frames:v", "1", "-map_metadata", "-1", "-c:v", "libwebp", "-quality", "80",
            "-f", "webp", "small.webp",
            "-map", "[o1]", "-frames:v", "1", "-map_metadata", "-1", "-c:v", "libwebp", "-quality", "80",
            "-f", "webp", "medium.webp");
    }

    [Test]
    public void Build_SquareTarget_CropsTheCenterBeforeScaling()
    {
        IReadOnlyList<string> arguments = FfmpegCoverArguments.Build(
            "png_pipe",
            [new CoverEncodeTarget(96, "small.webp", Square: true), new CoverEncodeTarget(300, "medium.webp")],
            80);

        arguments[10].Should().Be(
            "[0:v]split=2[s0][s1]"
            + ";[s0]crop='min(iw,ih)':'min(iw,ih)',scale='min(iw,96)':'min(ih,96)'"
            + ":force_original_aspect_ratio=decrease:flags=lanczos[o0]"
            + ";[s1]scale='min(iw,300)':'min(ih,300)':force_original_aspect_ratio=decrease:flags=lanczos[o1]");
    }

    [Test]
    public void Build_CropTarget_CropsTheClampedSectionBeforeScaling()
    {
        IReadOnlyList<string> arguments = FfmpegCoverArguments.Build(
            "png_pipe",
            [new CoverEncodeTarget(96, "small.webp", Crop: new CoverCrop(0.25, 1.5, 0.5))],
            80);

        arguments[10].Should().Be(
            "[0:v]split=1[s0]"
            + ";[s0]crop='max(1,trunc(min(iw,ih)*0.5))':'max(1,trunc(min(iw,ih)*0.5))'"
            + ":'min(max(0,trunc(iw*0.25)),iw-ow)':'min(max(0,trunc(ih*1)),ih-oh)',"
            + "scale='min(iw,96)':'min(ih,96)':force_original_aspect_ratio=decrease:flags=lanczos[o0]");
    }

    [TestCase(double.NaN, 0.0001)]
    [TestCase(-1, 0.0001)]
    [TestCase(0, 0.0001)]
    [TestCase(2, 1)]
    public void SectionCrop_SizeOutsideItsRange_IsClamped(double size, double expected)
    {
        string filter = FfmpegCoverArguments.SectionCrop(new CoverCrop(0, 0, size));

        filter.Should().StartWith(
            $"crop='max(1,trunc(min(iw,ih)*{expected.ToString(System.Globalization.CultureInfo.InvariantCulture)}))'");
    }

    [TestCase(0, 0, 1, true)]
    [TestCase(1, 1, 0.5, true)]
    [TestCase(-0.1, 0, 1, false)]
    [TestCase(0, 1.1, 1, false)]
    [TestCase(0, 0, 0, false)]
    [TestCase(0, 0, double.PositiveInfinity, false)]
    public void CoverCrop_IsValid_ChecksTheRanges(double x, double y, double size, bool valid)
    {
        new CoverCrop(x, y, size).IsValid.Should().Be(valid);
    }

    [Test]
    public void Build_WithoutTargets_Throws()
    {
        Action build = () => FfmpegCoverArguments.Build("png_pipe", [], 80);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }
}
