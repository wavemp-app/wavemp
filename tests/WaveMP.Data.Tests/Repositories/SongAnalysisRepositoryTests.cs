using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class SongAnalysisRepositoryTests : LibraryDatabaseTests
{
    private const int Version = 3;

    protected override string DatabaseName => "wavemp_tests_data_song_analyses";

    [Test]
    public async Task ListTracksToAnalyzeAsync_NothingAnalyzed_ListsTracksWithAudioOrVideoNewestFirst()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<Guid> tracks = await new SongAnalysisRepository(CreateDbContext())
            .ListTracksToAnalyzeAsync(Version, 10, []);

        tracks.Should().BeEquivalentTo([library.Ballad.Id, library.Closing.Id, library.Opening.Id]);
        tracks[0].Should().Be(library.Ballad.Id);
        (await new SongAnalysisRepository(CreateDbContext()).CountTracksToAnalyzeAsync(Version)).Should().Be(3);
    }

    [Test]
    public async Task ListTracksToAnalyzeAsync_CurrentAnalyses_LeavesThoseTracksOut()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        await AnalyzeAsync(library.Opening.Id, Version);
        await AnalyzeAsync(library.Closing.Id, Version);

        IReadOnlyList<Guid> tracks = await new SongAnalysisRepository(CreateDbContext())
            .ListTracksToAnalyzeAsync(Version, 10, []);

        tracks.Should().Equal(library.Ballad.Id);
    }

    [Test]
    public async Task ListTracksToAnalyzeAsync_OlderVersionChangedFileOrExcluded_ListsAndLeavesOutAsExpected()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        await AnalyzeAsync(library.Opening.Id, Version - 1);
        await AnalyzeAsync(library.Closing.Id, Version);
        await using (WaveMpDbContext db = CreateDbContext())
        {
            await db.MediaFiles.Where(file => file.Extension == "mkv")
                .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.Size, 123));
        }

        IReadOnlyList<Guid> tracks = await new SongAnalysisRepository(CreateDbContext())
            .ListTracksToAnalyzeAsync(Version, 10, [library.Ballad.Id]);

        tracks.Should().BeEquivalentTo([library.Opening.Id, library.Closing.Id]);
    }

    [Test]
    public async Task SaveAsync_AgainForTheSameFile_ReplacesTheAnalysis()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        MediaFile video = await PrimaryAsync(library.Closing.Id, MediaFileKind.Video);
        SongAnalysisRepository repository = new(CreateDbContext());
        await repository.SaveAsync([Analysis(video, Version, new SongPiece(4000, 0, 100_000))]);

        SongPiece[] pieces = [new SongPiece(2000, 0, 50_000), new SongPiece(60_000, 55_000, 1000)];

        await new SongAnalysisRepository(CreateDbContext()).SaveAsync([Analysis(video, Version, pieces)]);

        SongAnalysisTrackRow? row = await new SongAnalysisRepository(CreateDbContext())
            .FindTrackAsync(library.Closing.Id);
        row!.Video!.Analysis!.Pieces.Should().Equal(pieces);
        row.Audio!.Analysis.Should().BeNull();
        row.VideoSegment.StartMilliseconds.Should().Be(2000);
        (await CreateDbContext().MediaFileAnalyses.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task SaveAsync_FileRemovedInTheMeantime_LeavesItOut()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        MediaFile audio = await PrimaryAsync(library.Opening.Id, MediaFileKind.Audio);
        MediaFile gone = await PrimaryAsync(library.Ballad.Id, MediaFileKind.Audio);
        await using (WaveMpDbContext db = CreateDbContext())
        {
            await db.MediaFiles.Where(file => file.Id == gone.Id).ExecuteDeleteAsync();
        }

        await new SongAnalysisRepository(CreateDbContext())
            .SaveAsync([Analysis(audio, Version), Analysis(gone, Version)]);

        (await CreateDbContext().MediaFileAnalyses.Select(analysis => analysis.MediaFileId).ToListAsync())
            .Should().Equal(audio.Id);
    }

    [Test]
    public async Task FindTrackAsync_TrackWithCorrections_ReturnsFilesAndCorrections()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        await new TrackRepository(CreateDbContext())
            .SetManualSongSegmentAsync(library.Closing.Id, MediaFileKind.Video, -500, 200_000);

        SongAnalysisTrackRow? row = await new SongAnalysisRepository(CreateDbContext())
            .FindTrackAsync(library.Closing.Id);

        row!.Audio!.Extension.Should().Be("flac");
        row.Video!.Extension.Should().Be("mkv");
        (row.VideoSongStartMilliseconds, row.VideoSongEndMilliseconds).Should().Be((-500, 200_000));
        row.VideoSegment.Manual.Should().BeTrue();
        (await new SongAnalysisRepository(CreateDbContext()).FindTrackAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Test]
    public async Task MarkOutdatedAsync_OneTrack_MarksOnlyItsAnalysesAndKeepsTheCorrections()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        await AnalyzeAsync(library.Opening.Id, Version);
        await AnalyzeAsync(library.Closing.Id, Version);
        await new TrackRepository(CreateDbContext())
            .SetManualSongSegmentAsync(library.Closing.Id, MediaFileKind.Video, 1000, null);

        int marked = await new SongAnalysisRepository(CreateDbContext()).MarkOutdatedAsync(library.Closing.Id);

        marked.Should().Be(2);
        (await new SongAnalysisRepository(CreateDbContext())
                .ListTracksToAnalyzeAsync(Version, 10, [library.Ballad.Id]))
            .Should().Equal(library.Closing.Id);
        (await CreateDbContext().Tracks.Where(track => track.Id == library.Closing.Id)
                .Select(track => track.VideoSongStartMilliseconds)
                .SingleAsync())
            .Should().Be(1000);
        (await new SongAnalysisRepository(CreateDbContext()).MarkOutdatedAsync(null)).Should().Be(3);
    }

    [Test]
    public async Task FindNoticesAsync_UnsureMismatchCorrectedAndConfident_ReportsOnlyThoseThatNeedALook()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        SongAnalysisRepository repository = new(CreateDbContext());
        MediaFile closingAudio = await PrimaryAsync(library.Closing.Id, MediaFileKind.Audio);
        MediaFile closingVideo = await PrimaryAsync(library.Closing.Id, MediaFileKind.Video);
        MediaFile opening = await PrimaryAsync(library.Opening.Id, MediaFileKind.Audio);
        MediaFile ballad = await PrimaryAsync(library.Ballad.Id, MediaFileKind.Audio);
        await repository.SaveAsync(
        [
            Change(Analysis(closingAudio, Version), analysis => analysis.Mismatch = true),
            Change(
                Analysis(closingVideo, Version, new SongPiece(3000, 0, 100_000)),
                analysis => analysis.Confidence = 0.3),
            Change(
                Analysis(opening, Version, new SongPiece(3000, 0, 100_000)),
                analysis => analysis.Confidence = 0.4),
            Analysis(ballad, Version, new SongPiece(3000, 0, 100_000)),
        ]);
        await new TrackRepository(CreateDbContext())
            .SetManualSongSegmentAsync(library.Opening.Id, MediaFileKind.Audio, 3000, null);

        IReadOnlyDictionary<Guid, SongSegmentNotice> notices = await new SongAnalysisRepository(CreateDbContext())
            .FindNoticesAsync([library.Closing.Id, library.Opening.Id, library.Ballad.Id, library.Clip.Id]);

        notices.Should().BeEquivalentTo(new Dictionary<Guid, SongSegmentNotice>
        {
            [library.Closing.Id] = SongSegmentNotice.Mismatch,
        });
    }

    private async Task AnalyzeAsync(Guid trackId, int version)
    {
        await using WaveMpDbContext db = CreateDbContext();
        List<MediaFile> files = await db.MediaFiles
            .Where(file => file.TrackId == trackId
                && file.IsPrimary
                && (file.Kind == MediaFileKind.Audio || file.Kind == MediaFileKind.Video))
            .ToListAsync();
        await new SongAnalysisRepository(CreateDbContext())
            .SaveAsync([.. files.Select(file => Analysis(file, version))]);
    }

    private async Task<MediaFile> PrimaryAsync(Guid trackId, MediaFileKind kind)
    {
        await using WaveMpDbContext db = CreateDbContext();
        return await db.MediaFiles.SingleAsync(file => file.TrackId == trackId && file.IsPrimary && file.Kind == kind);
    }

    private static MediaFileAnalysis Change(MediaFileAnalysis analysis, Action<MediaFileAnalysis> change)
    {
        change(analysis);
        return analysis;
    }

    private static MediaFileAnalysis Analysis(MediaFile file, int version, params SongPiece[] pieces)
    {
        return new MediaFileAnalysis
        {
            MediaFileId = file.Id,
            Version = version,
            FileSize = file.Size,
            FileModifiedAt = file.ModifiedAt,
            AnalyzedAt = DateTimeOffset.UnixEpoch,
            Status = SongAnalysisStatus.Analyzed,
            Method = pieces.Length == 0 ? SongSegmentMethod.WholeFile : SongSegmentMethod.Alignment,
            Confidence = pieces.Length == 0 ? null : 0.9,
            Pieces = [.. pieces],
            LoudnessLufs = -12.5,
        };
    }
}
