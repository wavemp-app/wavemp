using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.Analysis.Decoding;
using WaveMP.Core.Analysis.Detection;
using WaveMP.Core.Analysis.Options;
using WaveMP.Core.Analysis.Tests.Sounds;
using WaveMP.Core.Transcoding.Options;
using WaveMP.Data.Entities;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Analysis.Tests.Decoding;

/// <summary>
/// Reads synthetic media made with ffmpeg: the album version of a song (a sequence of tones) and its music video, which
/// is longer and louder and has an intro, an outro and a part cut out.
/// </summary>
[TestFixture]
public class FfmpegSoundReaderTests
{
    private TemporaryDirectory _directory = null!;
    private string _album = null!;
    private string _video = null!;

    [OneTimeSetUp]
    public async Task CreateMediaAsync()
    {
        _directory = new TemporaryDirectory();
        _album = _directory.Combine("Song.flac");
        _video = _directory.Combine("Song.mkv");
        await SongMedia.CreateAlbumAsync(_album);
        await SongMedia.CreateVideoAsync(_album, _video);
    }

    [OneTimeTearDown]
    public void RemoveMedia()
    {
        _directory.Dispose();
    }

    [Test]
    public async Task ReadAsync_AudioFile_ReadsItsSoundAndLoudness()
    {
        SoundReading reading = await CreateReader().ReadAsync(_album);

        reading.Status.Should().Be(SongAnalysisStatus.Analyzed);
        reading.Features!.DurationSeconds.Should().BeApproximately(SongMedia.AlbumSeconds, 0.1);
        reading.LoudnessLufs.Should().BeInRange(-30, -5);
    }

    [Test]
    public async Task ReadAsync_LouderVideo_MeasuresItLouder()
    {
        Readings readings = await ReadBothAsync();

        readings.Video.Status.Should().Be(SongAnalysisStatus.Analyzed);
        readings.Video.Features!.DurationSeconds.Should().BeApproximately(SongMedia.VideoSeconds, 0.2);
        (readings.Video.LoudnessLufs - readings.Audio.LoudnessLufs).Should().BeGreaterThan(2);
    }

    [Test]
    public async Task ReadAsync_AlbumVersionAndVideo_AlignsTheSongWithTheVideo()
    {
        Readings readings = await ReadBothAsync();

        RecordingAlignment alignment = RecordingAligner.Align(readings.Audio.Features!, readings.Video.Features!)!;

        alignment.Matches.Should().BeTrue();
        alignment.Confidence.Should().BeGreaterThan(0.6);
        alignment.Pieces.Should().HaveCount(2);
        int cut = SongMedia.CutFromSeconds * 1000;
        alignment.Pieces[0].FileStartMilliseconds.Should().BeCloseTo(SongMedia.IntroSeconds * 1000, 150);
        alignment.Pieces[0].SongStartMilliseconds.Should().BeCloseTo(0, 150);
        alignment.Pieces[0].DurationMilliseconds.Should().BeCloseTo(cut, 300);
        alignment.Pieces[1].SongStartMilliseconds.Should().BeCloseTo(SongMedia.CutToSeconds * 1000, 300);
        alignment.Pieces[1].FileStartMilliseconds.Should().BeCloseTo((SongMedia.IntroSeconds * 1000) + cut, 300);
        (alignment.Pieces[1].SongStartMilliseconds + alignment.Pieces[1].DurationMilliseconds)
            .Should().BeCloseTo(SongMedia.AlbumSeconds * 1000, 150);
    }

    [Test]
    public async Task ReadAsync_VideoWithoutSound_ReturnsNoSound()
    {
        string path = _directory.Combine("Silent.mp4");
        await TestMediaGenerator.CreateVideoAsync(path, VideoFormat.Mp4, 160, 90, withAudio: false, durationSeconds: 2);

        SoundReading reading = await CreateReader().ReadAsync(path);

        reading.Should().Be(SoundReading.NoSound);
    }

    [Test]
    public async Task ReadAsync_DamagedFile_ReturnsFailed()
    {
        string path = _directory.Combine("Damaged.mp3");
        TestMediaGenerator.CreateCorruptFile(path);

        SoundReading reading = await CreateReader().ReadAsync(path);

        reading.Should().Be(SoundReading.Failed);
    }

    [Test]
    public async Task ReadAsync_FfmpegMissing_ReturnsFailed()
    {
        SoundReading reading = await CreateReader(_directory.Combine("no-ffmpeg")).ReadAsync(_album);

        reading.Should().Be(SoundReading.Failed);
    }

    [Test]
    public void Arguments_Always_ReadOnlyTheStartOfLongFilesWithOneThread()
    {
        IReadOnlyList<string> arguments = FfmpegSoundReader.Arguments("/media/Song.flac", TimeSpan.FromMinutes(20));

        arguments.Should().ContainInConsecutiveOrder("-t", "1200");
        arguments.Should().ContainInConsecutiveOrder("-threads", "1");
        arguments.Should().ContainInConsecutiveOrder("-i", "/media/Song.flac");
        arguments[^1].Should().Be("pipe:1");
    }

    [TestCase("[Parsed_ebur128_1 @ 0x1] Summary:\n\n  Integrated loudness:\n    I:         -14.2 LUFS\n", -14.2)]
    [TestCase("[Parsed_ebur128_1 @ 0x1] Summary:\n  Integrated loudness:\n    I:         -70.0 LUFS\n", null)]
    [TestCase("no summary", null)]
    public void ParseLoudness_ErrorOutput_ReadsTheIntegratedLoudness(string output, double? expected)
    {
        FfmpegSoundReader.ParseLoudness(output).Should().Be(expected);
    }

    private async Task<Readings> ReadBothAsync()
    {
        FfmpegSoundReader reader = CreateReader();
        return new Readings(await reader.ReadAsync(_album), await reader.ReadAsync(_video));
    }

    private static FfmpegSoundReader CreateReader(string? ffmpegPath = null)
    {
        return new FfmpegSoundReader(
            Microsoft.Extensions.Options.Options.Create(
                new TranscodingOptions { FfmpegPath = ffmpegPath ?? FfmpegTool.FfmpegPath }),
            Microsoft.Extensions.Options.Options.Create(new AnalysisOptions()),
            NullLogger<FfmpegSoundReader>.Instance);
    }

    private sealed record Readings(SoundReading Audio, SoundReading Video);
}
