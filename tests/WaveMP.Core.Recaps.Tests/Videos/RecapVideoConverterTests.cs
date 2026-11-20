using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Recaps.Options;
using WaveMP.Core.Recaps.Videos;
using WaveMP.Core.Transcoding.Options;
using WaveMP.Core.Transcoding.Processes;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Recaps.Tests.Videos;

[TestFixture]
public class RecapVideoConverterTests
{
    private static readonly byte[] _webm = [0x1A, 0x45, 0xDF, 0xA3, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14];

    private TemporaryDirectory _folder = null!;
    private IFfmpegCommandRunner _ffmpeg = null!;
    private List<string> _arguments = null!;
    private Func<IReadOnlyList<string>, FfmpegCommandResult> _run = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = new TemporaryDirectory();
        _ffmpeg = Substitute.For<IFfmpegCommandRunner>();
        _arguments = [];
        _run = arguments =>
        {
            File.Exists(arguments[4]).Should().BeTrue("the upload is written before ffmpeg runs");
            File.WriteAllBytes(arguments[^1], [1, 2, 3]);
            return new FfmpegCommandResult(true, 0, string.Empty);
        };
        _ffmpeg.RunAsync(default!, default, default).ReturnsForAnyArgs(call =>
        {
            IReadOnlyList<string> arguments = call.Arg<IReadOnlyList<string>>();
            _arguments.AddRange(arguments);
            return _run(arguments);
        });
    }

    [TearDown]
    public void TearDown()
    {
        _folder.Dispose();
    }

    [Test]
    public async Task ConvertAsync_RecordedVideo_ReturnsTheMp4AndDeletesTheUpload()
    {
        RecapVideoResult result = await CreateConverter().ConvertAsync(new MemoryStream(_webm));

        result.Status.Should().Be(RecapVideoStatus.Converted);
        result.Path.Should().EndWith(".mp4");
        File.ReadAllBytes(result.Path!).Should().Equal(1, 2, 3);
        Directory.GetFiles(VideoPath).Should().Equal(result.Path);
        _arguments.Should().Contain("ultrafast");
    }

    [Test]
    public async Task ConvertAsync_TooLargeVideo_ReturnsTooLargeAndKeepsNothing()
    {
        RecapVideoResult result = await CreateConverter(maxBytes: 8).ConvertAsync(new MemoryStream(_webm));

        result.Status.Should().Be(RecapVideoStatus.TooLarge);
        Directory.GetFiles(VideoPath).Should().BeEmpty();
        await _ffmpeg.DidNotReceiveWithAnyArgs().RunAsync(default!, default, default);
    }

    [Test]
    public async Task ConvertAsync_NoVideo_ReturnsUnsupportedFormatAndKeepsNothing()
    {
        RecapVideoResult result =
            await CreateConverter().ConvertAsync(new MemoryStream("not a video at all"u8.ToArray()));

        result.Status.Should().Be(RecapVideoStatus.UnsupportedFormat);
        Directory.GetFiles(VideoPath).Should().BeEmpty();
        await _ffmpeg.DidNotReceiveWithAnyArgs().RunAsync(default!, default, default);
    }

    [Test]
    public async Task ConvertAsync_FfmpegFails_ReturnsUnreadableAndKeepsNothing()
    {
        _run = arguments =>
        {
            File.WriteAllBytes(arguments[^1], [1]);
            return new FfmpegCommandResult(true, 1, "Invalid data");
        };

        RecapVideoResult result = await CreateConverter().ConvertAsync(new MemoryStream(_webm));

        result.Status.Should().Be(RecapVideoStatus.Unreadable);
        Directory.GetFiles(VideoPath).Should().BeEmpty();
    }

    [Test]
    public async Task ConvertAsync_FfmpegMissing_ReturnsUnavailable()
    {
        _run = _ => FfmpegCommandResult.NotStarted;

        RecapVideoResult result = await CreateConverter().ConvertAsync(new MemoryStream(_webm));

        result.Status.Should().Be(RecapVideoStatus.Unavailable);
        Directory.GetFiles(VideoPath).Should().BeEmpty();
    }

    [Test]
    public async Task ConvertAsync_Canceled_KeepsNothing()
    {
        _run = _ => throw new OperationCanceledException();

        Func<Task> convert = () => CreateConverter().ConvertAsync(new MemoryStream(_webm));

        await convert.Should().ThrowAsync<OperationCanceledException>();
        Directory.GetFiles(VideoPath).Should().BeEmpty();
    }

    private string VideoPath => _folder.Combine("recap-videos");

    private RecapVideoConverter CreateConverter(long maxBytes = 1024)
    {
        return new RecapVideoConverter(
            _ffmpeg,
            Microsoft.Extensions.Options.Options.Create(new RecapOptions
            {
                VideoPath = VideoPath,
                MaxVideoBytes = maxBytes,
            }),
            Microsoft.Extensions.Options.Options.Create(new TranscodingOptions { X264Preset = "ultrafast" }),
            NullLogger<RecapVideoConverter>.Instance);
    }
}
