using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Streaming.Decisions;
using WaveMP.Core.Streaming.Formats;
using WaveMP.Core.Streaming.Sources;
using WaveMP.Core.Transcoding.Accelerators;
using WaveMP.Core.Transcoding.Options;
using WaveMP.Core.Transcoding.Processes;
using WaveMP.Core.Transcoding.Remuxes;
using WaveMP.Core.Transcoding.Tests.Fakes;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Transcoding.Tests.Remuxes;

[TestFixture]
public class VideoRemuxServiceTests
{
    private static readonly Guid _trackId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private TemporaryDirectory _folder = null!;
    private FolderCacheStore _cache = null!;
    private IStreamService _streams = null!;
    private IHardwareAccelerationService _hardware = null!;
    private IFfmpegCommandRunner _runner = null!;
    private TranscodingOptions _options = null!;
    private VideoRemuxer _remuxer = null!;
    private VideoRemuxService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = new TemporaryDirectory();
        _cache = new FolderCacheStore(_folder.Combine("cache"));
        _streams = Substitute.For<IStreamService>();
        _hardware = Substitute.For<IHardwareAccelerationService>();
        _hardware.Current.Returns(new HardwareAccelerationStatus(
            HardwareAccelerationState.Unavailable,
            HardwareAccelerator.None,
            null,
            HardwareUnavailableReason.NoDevice,
            true,
            DateTimeOffset.UnixEpoch));
        _options = new TranscodingOptions { FfmpegPath = FfmpegTool.FfmpegPath };
        _runner = new FfmpegCommandRunner(
            new FfmpegProcessFactory(Microsoft.Extensions.Options.Options.Create(_options)));
        CreateService();
    }

    [TearDown]
    public void TearDown()
    {
        _remuxer.Dispose();
        _folder.Dispose();
    }

    [Test]
    public async Task GetAsync_H264InMatroska_CopiesThePictureIntoMp4()
    {
        VideoSource source = await CreateVideoAsync("Song.mkv", VideoFormat.Mkv);

        RemuxResult result = await _service.GetAsync(_trackId, RemuxAudio.Copy);

        result.Status.Should().Be(RemuxStatus.Ready);
        result.ETag.Should().Be(RemuxKeys.Of(source, RemuxAudio.Copy));
        result.LastModified.Should().Be(source.LastModified);
        string copy = _folder.Combine("out/remux.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        await using (Stream content = result.Stream!)
        await using (FileStream file = File.Create(copy))
        {
            await content.CopyToAsync(file);
        }

        string probe = await ProbeAsync(copy);
        probe.Should().Contain("mov,mp4").And.Contain("h264").And.Contain("aac");
    }

    [Test]
    public async Task GetAsync_SecondRequest_ReadsTheCache()
    {
        await CreateVideoAsync("Song.mkv", VideoFormat.Mkv);
        (await _service.GetAsync(_trackId, RemuxAudio.Copy)).Stream!.Dispose();
        IFfmpegCommandRunner runner = Substitute.For<IFfmpegCommandRunner>();
        _runner = runner;
        CreateService();

        RemuxResult result = await _service.GetAsync(_trackId, RemuxAudio.Copy);

        result.Status.Should().Be(RemuxStatus.Ready);
        result.Stream!.Dispose();
        await runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default, default);
    }

    [Test]
    public async Task GetAsync_ParallelRequests_RemuxOnce()
    {
        VideoSource source = Source(_folder.Combine("media/Song.mkv"));
        _streams.GetVideoSourceAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(VideoSourceResult.Found(source));
        TaskCompletionSource<FfmpegCommandResult> done = new();
        IFfmpegCommandRunner runner = Substitute.For<IFfmpegCommandRunner>();
        runner.RunAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                File.WriteAllBytes(((IReadOnlyList<string>)call[0])[^1], [1, 2, 3]);
                return done.Task;
            });
        _runner = runner;
        CreateService();

        Task<RemuxResult> first = _service.GetAsync(_trackId, RemuxAudio.Copy);
        Task<RemuxResult> second = _service.GetAsync(_trackId, RemuxAudio.Copy);
        await WaitUntilAsync(() => runner.ReceivedCalls().Any());
        done.SetResult(new FfmpegCommandResult(true, 0, string.Empty));

        RemuxResult[] results = await Task.WhenAll(first, second);

        results.Should().AllSatisfy(result => result.Status.Should().Be(RemuxStatus.Ready));
        foreach (RemuxResult result in results)
        {
            result.Stream!.Dispose();
        }

        runner.ReceivedCalls().Should().ContainSingle();
    }

    [Test]
    public async Task GetAsync_FileFfmpegCannotRemux_FailsAndIsTranscodedFromNowOn()
    {
        string path = _folder.Combine("media/Broken.mkv");
        TestMediaGenerator.CreateCorruptFile(path);
        VideoSource source = Source(path);
        _streams.GetVideoSourceAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(VideoSourceResult.Found(source));
        _service.CanRemux(source, RemuxAudio.Copy).Should().BeTrue();

        RemuxResult result = await _service.GetAsync(_trackId, RemuxAudio.Copy);

        result.Status.Should().Be(RemuxStatus.Failed);
        _service.CanRemux(source, RemuxAudio.Copy).Should().BeFalse();
        _service.CanRemux(source, RemuxAudio.Aac).Should().BeTrue("another sound is another remux");
        Directory.EnumerateFiles(_cache.TemporaryRoot).Should().BeEmpty();
    }

    [Test]
    public async Task GetAsync_TrackWithoutVideo_ReturnsNoVideo()
    {
        _streams.GetVideoSourceAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(VideoSourceResult.Of(StreamSourceStatus.NoVideo));

        RemuxResult result = await _service.GetAsync(_trackId, RemuxAudio.Copy);

        result.Status.Should().Be(RemuxStatus.NoVideo);
    }

    [Test]
    public async Task GetAsync_CacheUnavailable_ReturnsUnavailable()
    {
        VideoSource source = Source(_folder.Combine("media/Song.mkv"));
        _streams.GetVideoSourceAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(VideoSourceResult.Found(source));
        _cache.IsAvailable = false;

        RemuxResult result = await _service.GetAsync(_trackId, RemuxAudio.Copy);

        result.Status.Should().Be(RemuxStatus.Unavailable);
        _service.CanRemux(source, RemuxAudio.Copy).Should().BeFalse();
    }

    [Test]
    public void Keys_ChangeWithTheFileVersionAndTheSound()
    {
        VideoSource source = Source("/media/Song.mkv");

        string key = RemuxKeys.Of(source, RemuxAudio.Copy);

        key.Should().MatchRegex("^[0-9a-f]{32}$");
        RemuxKeys.Of(source, RemuxAudio.Aac).Should().NotBe(key);
        RemuxKeys.Of(source with { Length = 2 }, RemuxAudio.Copy).Should().NotBe(key);
        RemuxKeys.Of(source with { LastModified = DateTimeOffset.UnixEpoch.AddDays(1) }, RemuxAudio.Copy)
            .Should().NotBe(key);
        RemuxKeys.NameOf(key).Should().Be($"remux/{key}.mp4");
    }

    private void CreateService()
    {
        _remuxer?.Dispose();
        _remuxer = new VideoRemuxer(
            _runner,
            _cache,
            _hardware,
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<VideoRemuxer>.Instance);
        _service = new VideoRemuxService(_streams, _remuxer, _cache);
    }

    private async Task<VideoSource> CreateVideoAsync(string name, VideoFormat format)
    {
        string path = _folder.Combine($"media/{name}");
        await TestMediaGenerator.CreateVideoAsync(path, format, 320, 240, codec: TestVideoCodec.H264);
        VideoSource source = Source(path);
        _streams.GetVideoSourceAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(VideoSourceResult.Found(source));
        return source;
    }

    private static VideoSource Source(string path)
    {
        return new VideoSource(
            _trackId,
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            path,
            VideoContainer.Matroska,
            VideoCodec.H264,
            AudioCodec.Aac,
            true,
            "yuv420p",
            320,
            240,
            44100,
            VideoContentTypes.Matroska,
            File.Exists(path) ? new FileInfo(path).Length : 1,
            DateTimeOffset.UnixEpoch,
            "tag",
            TimeSpan.FromSeconds(1),
            true);
    }

    private static async Task<string> ProbeAsync(string path)
    {
        ProcessStartInfo startInfo = new(FfmpegTool.FfprobePath)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in (string[])
                 ["-v", "error", "-show_entries", "stream=codec_name:format=format_name", "-of", "csv=p=0", path])
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        process.ExitCode.Should().Be(0);
        return output;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > end)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }
}
