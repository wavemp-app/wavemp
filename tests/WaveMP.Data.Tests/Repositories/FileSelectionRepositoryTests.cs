using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class FileSelectionRepositoryTests : LibraryDatabaseTests
{
    protected override string DatabaseName => "wavemp_tests_library_file_selections";

    [Test]
    public async Task ReplaceAsync_NewSelection_AddsUpdatesAndRemovesRows()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Track");
        MediaFile flac = seed.File("Track.flac", MediaFileKind.Audio, track);
        MediaFile mp3 = seed.File("Track.mp3", MediaFileKind.Audio, track, isPrimary: false);
        MediaFile video = seed.File("Track.mkv", MediaFileKind.Video, track);
        await SaveAsync(seed);
        FileSelectionRepository repository = new(CreateDbContext());

        (await repository.ReplaceAsync(track.Id, [Selection(track, FileSelectionCategory.Audio, flac)]))
            .Should().BeTrue();
        TrackFileSelection[] replaced =
        [
            Selection(track, FileSelectionCategory.Audio, mp3),
            Selection(track, FileSelectionCategory.Video, video),
        ];
        (await repository.ReplaceAsync(track.Id, replaced)).Should().BeTrue();

        TrackFiles files = (await repository.LoadTrackAsync(track.Id))!;
        files.Files.Select(file => file.FileName).Should().Equal("Track.flac", "Track.mkv", "Track.mp3");
        files.Selections.Select(selection => (selection.Category, selection.MediaFileId, selection.RelativePath))
            .Should().BeEquivalentTo([
                (FileSelectionCategory.Audio, mp3.Id, "Track.mp3"),
                (FileSelectionCategory.Video, video.Id, "Track.mkv"),
            ]);

        (await repository.ReplaceAsync(track.Id, [])).Should().BeTrue();
        await using WaveMpDbContext db = CreateDbContext();
        (await db.TrackFileSelections.AnyAsync()).Should().BeFalse();
    }

    [Test]
    public async Task ReplaceAsync_UnknownTrack_ReturnsFalse()
    {
        FileSelectionRepository repository = new(CreateDbContext());

        (await repository.ReplaceAsync(Guid.NewGuid(), [])).Should().BeFalse();
        (await repository.LoadTrackAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Test]
    public async Task ListAsync_TrackWithMissingSelection_ShowsItsStatesAndCounts()
    {
        LibrarySeed seed = new();
        Album album = seed.Album("Album");
        Track track = seed.Track("Track", album: album);
        seed.Track("Plain");
        MediaFile picture = seed.File("Track.jpg", MediaFileKind.Image, track);
        seed.TrackCover(track, picture);
        seed.File("Track.flac", MediaFileKind.Audio, track);
        seed.File("Track.mp3", MediaFileKind.Audio, track, isPrimary: false);
        await SaveAsync(seed);
        await using (WaveMpDbContext db = CreateDbContext())
        {
            db.TrackFileSelections.Add(Selection(track, FileSelectionCategory.Cover, picture, isMissing: true));
            await db.SaveChangesAsync();
        }

        FileSelectionRepository repository = new(CreateDbContext());

        Page<TrackFileSummaryRow> page = await repository.ListAsync(new FileSelectionListQuery(
            0,
            10,
            FileSelectionSortField.Title,
            SortDirection.Asc,
            FileSelectionFilter.Missing));

        page.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            TrackId = track.Id,
            AlbumTitle = "Album",
            AudioCount = 2,
            VideoCount = 0,
            CoverCount = 1,
            Audio = FileSelectionState.Automatic,
            Video = FileSelectionState.Automatic,
            Cover = FileSelectionState.Missing,
        });
    }

    private static TrackFileSelection Selection(
        Track track,
        FileSelectionCategory category,
        MediaFile file,
        bool isMissing = false)
    {
        return new TrackFileSelection
        {
            TrackId = track.Id,
            Category = category,
            MediaFileId = file.Id,
            RelativePath = file.RelativePath,
            IsMissing = isMissing,
            SelectedAt = LibrarySeed.DefaultTime,
        };
    }
}
