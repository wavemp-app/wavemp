using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Analysis.Decoding;
using WaveMP.Core.Analysis.Features;
using WaveMP.Core.Analysis.Services;
using WaveMP.Core.Analysis.Tests.Sounds;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Services;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Analysis.Tests.Services;

[TestFixture]
public class SongAnalyzerTests
{
    private static readonly float[] _album = SyntheticSound.Melody(60, 11);
    private static readonly double[] _lines = [2.0, 5.3, 9.1, 12.0, 16.4, 19.8, 24.6, 27.2, 31.9, 35.5, 38.1, 42.7];

    private readonly Guid _trackId = Guid.NewGuid();

    private TemporaryDirectory _directory = null!;
    private ISongAnalysisRepository _repository = null!;
    private ISoundReader _reader = null!;
    private ILyricsRepository _lyricsRepository = null!;
    private ILyricsService _lyrics = null!;
    private ILyricsDownloader _downloader = null!;
    private LibrarySettings _settings = null!;
    private List<MediaFileAnalysis> _saved = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = new TemporaryDirectory();
        _repository = Substitute.For<ISongAnalysisRepository>();
        _saved = [];
        _repository.SaveAsync(
                Arg.Do<IReadOnlyCollection<MediaFileAnalysis>>(analyses => _saved.AddRange(analyses)),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _reader = Substitute.For<ISoundReader>();
        _lyricsRepository = Substitute.For<ILyricsRepository>();
        _lyricsRepository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(LyricsRow(download: null));
        _lyrics = Substitute.For<ILyricsService>();
        _lyrics.GetAsync(_trackId, Arg.Any<CancellationToken>()).Returns(LyricsResult.NoLyrics);
        _downloader = Substitute.For<ILyricsDownloader>();
        _downloader.DownloadAsync(_trackId, Arg.Any<LyricsDownloadMode>(), Arg.Any<CancellationToken>())
            .Returns(new LyricsDownloadResult(LyricsDownloadOutcome.SavedToDatabase));
        _downloader.FindCandidateAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(new LyricsCandidate(LyricsDownloadOutcome.NotFound));
        _downloader.SaveCandidateAsync(_trackId, Arg.Any<LrclibRecord>(), Arg.Any<CancellationToken>())
            .Returns(new LyricsDownloadResult(LyricsDownloadOutcome.SavedToDatabase));
        _settings = new LibrarySettings { DownloadLyrics = true };
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Dispose();
    }

    [Test]
    public async Task AnalyzeAsync_AudioAndVideo_AlignsThemFirstAndThenLooksUpTheLyrics()
    {
        MediaFile audio = GivenFile(MediaFileKind.Audio, "Song.flac", _album);
        MediaFile video = GivenFile(
            MediaFileKind.Video,
            "Song.mkv",
            SyntheticSound.Gain(
                SyntheticSound.Concat(
                    SyntheticSound.Noise(6, 1),
                    SyntheticSound.Slice(_album, 0, 30),
                    SyntheticSound.Slice(_album, 40, 60),
                    SyntheticSound.Noise(5, 2)),
                1.8f),
            loudness: -9);
        GivenTrack(audio, video);

        SongAnalysisResult result = await CreateAnalyzer().AnalyzeAsync(_trackId);

        result.Should().Be(new SongAnalysisResult(SongAnalysisOutcome.Analyzed));
        MediaFileAnalysis audioResult = _saved.Single(analysis => analysis.MediaFileId == audio.Id);
        MediaFileAnalysis videoResult = _saved.Single(analysis => analysis.MediaFileId == video.Id);
        (audioResult.Method, audioResult.Pieces.Count, audioResult.Mismatch)
            .Should().Be((SongSegmentMethod.WholeFile, 0, false));
        videoResult.Method.Should().Be(SongSegmentMethod.Alignment);
        videoResult.Confidence.Should().BeGreaterThan(0.6);
        videoResult.Pieces.Should().HaveCount(2);
        videoResult.Pieces[0].FileStartMilliseconds.Should().BeCloseTo(6000, 120);
        videoResult.Pieces[1].SongStartMilliseconds.Should().BeCloseTo(40_000, 200);
        (videoResult.LoudnessLufs, videoResult.Version, videoResult.FileSize)
            .Should().Be(((double?)-9, Options.AnalysisOptions.Version, video.Size));
        Received.InOrder(() =>
        {
            _repository.SaveAsync(Arg.Any<IReadOnlyCollection<MediaFileAnalysis>>(), Arg.Any<CancellationToken>());
            _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic, Arg.Any<CancellationToken>());
        });
        await _downloader.DidNotReceiveWithAnyArgs().FindCandidateAsync(default, default);
    }

    [Test]
    public async Task AnalyzeAsync_AudioAndOtherVersionAsVideo_MarksBothAsMismatch()
    {
        MediaFile audio = GivenFile(MediaFileKind.Audio, "Song.flac", _album);
        MediaFile video = GivenFile(
            MediaFileKind.Video,
            "Song.mkv",
            SyntheticSound.Concat(SyntheticSound.Noise(5, 3), SyntheticSound.Melody(70, 77)));
        GivenTrack(audio, video);

        SongAnalysisResult result = await CreateAnalyzer().AnalyzeAsync(_trackId);

        result.NeedsReview.Should().BeTrue();
        _saved.Should().HaveCount(2).And.OnlyContain(analysis => analysis.Mismatch);
        _saved.Single(analysis => analysis.MediaFileId == video.Id).Method
            .Should().NotBe(SongSegmentMethod.Alignment);
    }

    [Test]
    public async Task AnalyzeAsync_TrackAlreadyLookedUp_DoesNotLookUpTheLyricsAgain()
    {
        GivenTrack(
            GivenFile(MediaFileKind.Audio, "Song.flac", _album),
            GivenFile(MediaFileKind.Video, "Song.mkv", _album));
        _lyricsRepository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(LyricsRow(new LyricsDownloadRow(LyricsDownloadStatus.NotFound, null, DateTimeOffset.UnixEpoch)));

        await CreateAnalyzer().AnalyzeAsync(_trackId);

        await _downloader.DidNotReceiveWithAnyArgs().DownloadAsync(default, default, default);
    }

    [Test]
    public async Task AnalyzeAsync_OnlyAVideo_LooksUpTheLyricsFirstAndAlignsThem()
    {
        MediaFile video = GivenFile(
            MediaFileKind.Video,
            "Song.mkv",
            SyntheticSound.Bursts(65, _lines.Select(line => line + 10)));
        GivenTrack(null, video);
        LrclibRecord record = new() { Id = 7, Duration = 48, SyncedLyrics = Lrc(_lines) };
        _downloader.FindCandidateAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(new LyricsCandidate(LyricsDownloadOutcome.NotNeeded, record));

        SongAnalysisResult result = await CreateAnalyzer().AnalyzeAsync(_trackId);

        result.Outcome.Should().Be(SongAnalysisOutcome.Analyzed);
        MediaFileAnalysis analysis = _saved.Single();
        analysis.Method.Should().Be(SongSegmentMethod.Lyrics);
        analysis.Pieces.Single().FileStartMilliseconds.Should().BeCloseTo(10_000, 100);
        analysis.Pieces.Single().DurationMilliseconds.Should().BeCloseTo(48_000, 100);
        Received.InOrder(() =>
        {
            _downloader.FindCandidateAsync(_trackId, Arg.Any<CancellationToken>());
            _repository.SaveAsync(Arg.Any<IReadOnlyCollection<MediaFileAnalysis>>(), Arg.Any<CancellationToken>());
            _downloader.SaveCandidateAsync(_trackId, record, Arg.Any<CancellationToken>());
        });
        await _downloader.DidNotReceiveWithAnyArgs().DownloadAsync(default, default, default);
    }

    [Test]
    public async Task AnalyzeAsync_OnlyAnAudioFileWithOwnSynchronizedLyrics_AlignsThemWithoutLookingUp()
    {
        MediaFile audio = GivenFile(
            MediaFileKind.Audio,
            "Song.flac",
            SyntheticSound.Bursts(60, _lines.Select(line => line + 8)));
        GivenTrack(audio, null);
        var ownLyrics = new Lyrics.Documents.TrackLyrics(
            Lyrics.Parsers.LrcParser.Parse(Lrc(_lines)),
            Lyrics.Documents.LyricsSource.File);
        _lyrics.GetAsync(_trackId, Arg.Any<CancellationToken>()).Returns(LyricsResult.Found(ownLyrics));

        await CreateAnalyzer().AnalyzeAsync(_trackId);

        MediaFileAnalysis analysis = _saved.Single();
        analysis.Method.Should().Be(SongSegmentMethod.Lyrics);
        analysis.Pieces.Single().FileStartMilliseconds.Should().BeCloseTo(8000, 100);
        await _downloader.DidNotReceiveWithAnyArgs().FindCandidateAsync(default, default);
        await _downloader.DidNotReceiveWithAnyArgs().DownloadAsync(default, default, default);
    }

    [Test]
    public async Task AnalyzeAsync_OnlyAVideoWithoutLyrics_FallsBackToTheStructureAndRecordsTheLookup()
    {
        MediaFile video = GivenFile(
            MediaFileKind.Video,
            "Song.mkv",
            SyntheticSound.Concat(
                SyntheticSound.Noise(5, 4),
                SyntheticSound.Silence(1),
                SyntheticSound.Melody(60, 5),
                SyntheticSound.Silence(1),
                SyntheticSound.Noise(4, 6)));
        GivenTrack(null, video);

        SongAnalysisResult result = await CreateAnalyzer().AnalyzeAsync(_trackId);

        MediaFileAnalysis analysis = _saved.Single();
        analysis.Method.Should().Be(SongSegmentMethod.Structure);
        analysis.Confidence.Should().BeLessThan(0.6);
        result.NeedsReview.Should().BeTrue();
        Received.InOrder(() =>
        {
            _downloader.FindCandidateAsync(_trackId, Arg.Any<CancellationToken>());
            _repository.SaveAsync(Arg.Any<IReadOnlyCollection<MediaFileAnalysis>>(), Arg.Any<CancellationToken>());
            _downloader.DownloadAsync(_trackId, LyricsDownloadMode.Automatic, Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task AnalyzeAsync_DownloadOfLyricsSwitchedOff_NeverAsksLrclib()
    {
        _settings = new LibrarySettings { DownloadLyrics = false };
        GivenTrack(null, GivenFile(MediaFileKind.Video, "Song.mkv", SyntheticSound.Melody(30, 8)));

        await CreateAnalyzer().AnalyzeAsync(_trackId);

        _saved.Should().ContainSingle();
        await _downloader.DidNotReceiveWithAnyArgs().FindCandidateAsync(default, default);
        await _downloader.DidNotReceiveWithAnyArgs().DownloadAsync(default, default, default);
    }

    [Test]
    public async Task AnalyzeAsync_VideoWithoutSound_StoresItAsNoSound()
    {
        MediaFile video = GivenFile(MediaFileKind.Video, "Song.mkv", null);
        GivenTrack(null, video);
        _reader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SoundReading.NoSound);

        SongAnalysisResult result = await CreateAnalyzer().AnalyzeAsync(_trackId);

        result.NeedsReview.Should().BeFalse();
        (_saved.Single().Status, _saved.Single().Method)
            .Should().Be((SongAnalysisStatus.NoSound, SongSegmentMethod.WholeFile));
    }

    [Test]
    public async Task AnalyzeAsync_UnknownTrack_ReturnsTrackNotFound()
    {
        SongAnalysisResult result = await CreateAnalyzer().AnalyzeAsync(_trackId);

        result.Outcome.Should().Be(SongAnalysisOutcome.TrackNotFound);
        await _repository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    private SongAnalyzer CreateAnalyzer()
    {
        IMediaFileLocator locator = Substitute.For<IMediaFileLocator>();
        locator.TryGetFullPath(Arg.Any<Guid>(), Arg.Any<string>(), out Arg.Any<string>())
            .Returns(call =>
            {
                call[2] = _directory.Combine((string)call[1]);
                return true;
            });
        ISettingsSnapshot<LibrarySettings> settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        settings.Current.Returns(_ => _settings);
        return new SongAnalyzer(
            _repository,
            _reader,
            locator,
            _lyricsRepository,
            _lyrics,
            _downloader,
            settings,
            TimeProvider.System,
            NullLogger<SongAnalyzer>.Instance);
    }

    private void GivenTrack(MediaFile? audio, MediaFile? video)
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(new SongAnalysisTrackRow(_trackId, audio, video));
    }

    private MediaFile GivenFile(MediaFileKind kind, string name, float[]? sound, double? loudness = -14)
    {
        File.WriteAllBytes(_directory.Combine(name), [1]);
        if (sound is not null)
        {
            _reader.ReadAsync(_directory.Combine(name), Arg.Any<CancellationToken>()).Returns(new SoundReading(
                SongAnalysisStatus.Analyzed,
                SoundFeatures.FromSamples(sound),
                loudness));
        }

        return new MediaFile
        {
            Id = Guid.NewGuid(),
            SourceId = MediaSource.DefaultId,
            RelativePath = name,
            DirectoryPath = string.Empty,
            FileName = name,
            BaseName = "Song",
            NormalizedBaseName = "SONG",
            Extension = Path.GetExtension(name).TrimStart('.'),
            Kind = kind,
            Size = 1000 + name.Length,
            ModifiedAt = DateTimeOffset.UnixEpoch,
            IsPrimary = true,
            IsLossless = false,
            HasEmbeddedPicture = false,
            IsWritable = true,
            MetadataVersion = 2,
            FirstSeenAt = DateTimeOffset.UnixEpoch,
            ScannedAt = DateTimeOffset.UnixEpoch,
            Duration = TimeSpan.FromSeconds(sound?.Length / (double)SoundFeatures.SampleRate ?? 10),
        };
    }

    private LyricsTrackRow LyricsRow(LyricsDownloadRow? download)
    {
        return new LyricsTrackRow(
            _trackId,
            MediaSource.DefaultId,
            string.Empty,
            "Song",
            "Song",
            "Artist",
            null,
            TimeSpan.FromSeconds(60),
            null,
            "Song.flac",
            null,
            download);
    }

    private static string Lrc(IEnumerable<double> lines)
    {
        return string.Join('\n', lines.Select((line, index) => string.Create(
            CultureInfo.InvariantCulture,
            $"[{(int)(line / 60):00}:{line % 60:00.00}]Line {index + 1}")));
    }
}
