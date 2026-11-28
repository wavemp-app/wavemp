using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Analysis.Options;
using WaveMP.Core.Analysis.Services;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Analysis.Tests.Services;

[TestFixture]
public class SongSegmentServiceTests
{
    private readonly Guid _trackId = Guid.NewGuid();

    private ISongAnalysisRepository _repository = null!;
    private ITrackRepository _tracks = null!;
    private IJobService _jobs = null!;
    private SongSegmentService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ISongAnalysisRepository>();
        _tracks = Substitute.For<ITrackRepository>();
        _tracks.SetManualSongSegmentAsync(
                Arg.Any<Guid>(),
                Arg.Any<MediaFileKind>(),
                Arg.Any<int?>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        _jobs = Substitute.For<IJobService>();
        _jobs.StartRunAsync(Arg.Any<JobRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(JobRunStartResult.Failure(JobRunStartError.AlreadyActive));
        _service = new SongSegmentService(_repository, _tracks, _jobs, NullLogger<SongSegmentService>.Instance);
    }

    [Test]
    public async Task GetAsync_AnalyzedVideoWithCorrectedAudio_ReturnsDetectedAndEffectiveSegments()
    {
        MediaFile audio = File(MediaFileKind.Audio, analysis: null);
        MediaFile video = File(MediaFileKind.Video, Analysis(new SongPiece(5000, 0, 180_000)));
        GivenTrack(new SongAnalysisTrackRow(_trackId, audio, video, AudioSongStartMilliseconds: 1200));

        TrackSongSegments? segments = await _service.GetAsync(_trackId);

        segments!.Audio!.Pending.Should().BeTrue();
        segments.Audio.ManualStartMilliseconds.Should().Be(1200);
        segments.Audio.Effective.StartMilliseconds.Should().Be(1200);
        segments.Audio.Effective.Manual.Should().BeTrue();
        segments.Video!.Pending.Should().BeFalse();
        segments.Video.Detected.StartMilliseconds.Should().Be(5000);
        segments.Video.Effective.Should().BeEquivalentTo(segments.Video.Detected);
    }

    [Test]
    public async Task GetAsync_ChangedFile_IsPending()
    {
        MediaFile video = File(MediaFileKind.Video, Analysis(new SongPiece(5000, 0, 180_000)));
        video.Size += 1;
        GivenTrack(new SongAnalysisTrackRow(_trackId, null, video));

        TrackSongSegments? segments = await _service.GetAsync(_trackId);

        segments!.Video!.Pending.Should().BeTrue();
    }

    [Test]
    public async Task SetAsync_ValidCorrection_StoresItForTheKind()
    {
        GivenTrack(new SongAnalysisTrackRow(_trackId, null, File(MediaFileKind.Video, null)));

        SongSegmentUpdate result = await _service.SetAsync(_trackId, MediaFileKind.Video, -2000, 200_000);

        result.Status.Should().Be(SongSegmentUpdateStatus.Saved);
        await _tracks.Received(1).SetManualSongSegmentAsync(
            _trackId,
            MediaFileKind.Video,
            -2000,
            200_000,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetAsync_Nulls_RemovesTheCorrection()
    {
        GivenTrack(new SongAnalysisTrackRow(_trackId, File(MediaFileKind.Audio, null), null, 100, 2000));

        SongSegmentUpdate result = await _service.SetAsync(_trackId, MediaFileKind.Audio, null, null);

        result.Status.Should().Be(SongSegmentUpdateStatus.Saved);
        await _tracks.Received(1).SetManualSongSegmentAsync(
            _trackId,
            MediaFileKind.Audio,
            null,
            null,
            Arg.Any<CancellationToken>());
    }

    [TestCase(-600_001, null, "startMilliseconds")]
    [TestCase(240_000, null, "startMilliseconds")]
    [TestCase(null, 240_001, "endMilliseconds")]
    [TestCase(10_000, 10_000, "endMilliseconds")]
    [TestCase(null, 0, "endMilliseconds")]
    public async Task SetAsync_OutsideTheFile_IsInvalidWithoutStoringIt(int? start, int? end, string field)
    {
        GivenTrack(new SongAnalysisTrackRow(_trackId, null, File(MediaFileKind.Video, null)));

        SongSegmentUpdate result = await _service.SetAsync(_trackId, MediaFileKind.Video, start, end);

        (result.Status, result.InvalidField).Should().Be((SongSegmentUpdateStatus.Invalid, field));
        await _tracks.DidNotReceiveWithAnyArgs().SetManualSongSegmentAsync(default, default, default, default, default);
    }

    [Test]
    public async Task SetAsync_TrackWithoutFileOfTheKind_ReturnsNoFile()
    {
        GivenTrack(new SongAnalysisTrackRow(_trackId, File(MediaFileKind.Audio, null), null));

        SongSegmentUpdate result = await _service.SetAsync(_trackId, MediaFileKind.Video, 1000, null);

        result.Status.Should().Be(SongSegmentUpdateStatus.NoFile);
    }

    [Test]
    public async Task SetAsync_UnknownTrack_ReturnsTrackNotFound()
    {
        SongSegmentUpdate result = await _service.SetAsync(_trackId, MediaFileKind.Video, 1000, null);

        result.Status.Should().Be(SongSegmentUpdateStatus.TrackNotFound);
    }

    [Test]
    public async Task AnalyzeAgainAsync_KnownTrack_MarksItsAnalysesAndStartsTheAnalysisButKeepsCorrections()
    {
        GivenTrack(new SongAnalysisTrackRow(_trackId, File(MediaFileKind.Audio, null), null, 100, 2000));

        bool started = await _service.AnalyzeAgainAsync(_trackId);

        started.Should().BeTrue();
        Received.InOrder(() =>
        {
            _repository.MarkOutdatedAsync(_trackId, Arg.Any<CancellationToken>());
            _jobs.StartRunAsync(
                new JobRunRequest(JobIds.SongAnalysis, JobRunTrigger.Manual),
                Arg.Any<CancellationToken>());
        });
        await _tracks.DidNotReceiveWithAnyArgs().SetManualSongSegmentAsync(default, default, default, default, default);
    }

    [Test]
    public async Task AnalyzeAllAgainAsync_Always_MarksAllAnalysesAndStartsTheAnalysis()
    {
        _repository.MarkOutdatedAsync(null, Arg.Any<CancellationToken>()).Returns(42);

        int renewed = await _service.AnalyzeAllAgainAsync();

        renewed.Should().Be(42);
        await _jobs.Received(1).StartRunAsync(
            new JobRunRequest(JobIds.SongAnalysis, JobRunTrigger.Manual),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AnalyzeAgainAsync_UnknownTrack_ReturnsFalse()
    {
        (await _service.AnalyzeAgainAsync(_trackId)).Should().BeFalse();
        await _jobs.DidNotReceiveWithAnyArgs().StartRunAsync(default!, default);
    }

    private void GivenTrack(SongAnalysisTrackRow row)
    {
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(row);
    }

    private static MediaFileAnalysis Analysis(params SongPiece[] pieces)
    {
        return new MediaFileAnalysis
        {
            MediaFileId = Guid.Empty,
            Version = AnalysisOptions.Version,
            FileSize = 1000,
            FileModifiedAt = DateTimeOffset.UnixEpoch,
            AnalyzedAt = DateTimeOffset.UnixEpoch,
            Status = SongAnalysisStatus.Analyzed,
            Method = SongSegmentMethod.Alignment,
            Confidence = 0.9,
            Pieces = [.. pieces],
        };
    }

    private static MediaFile File(MediaFileKind kind, MediaFileAnalysis? analysis)
    {
        return new MediaFile
        {
            Id = Guid.NewGuid(),
            SourceId = MediaSource.DefaultId,
            RelativePath = "Song",
            DirectoryPath = string.Empty,
            FileName = "Song",
            BaseName = "Song",
            NormalizedBaseName = "SONG",
            Extension = kind == MediaFileKind.Audio ? "flac" : "mkv",
            Kind = kind,
            Size = 1000,
            ModifiedAt = DateTimeOffset.UnixEpoch,
            IsPrimary = true,
            IsLossless = false,
            HasEmbeddedPicture = false,
            IsWritable = true,
            MetadataVersion = 2,
            FirstSeenAt = DateTimeOffset.UnixEpoch,
            ScannedAt = DateTimeOffset.UnixEpoch,
            Duration = TimeSpan.FromMinutes(4),
            Analysis = analysis,
        };
    }
}
