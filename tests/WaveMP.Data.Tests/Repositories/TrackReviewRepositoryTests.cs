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
public class TrackReviewRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _reviewedAt = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    protected override string DatabaseName => "wavemp_tests_library_track_reviews";

    [Test]
    public async Task MarkReviewedAsync_UnreviewedTracks_StoresTimeAndProfileAndChangesTheCounts()
    {
        LibrarySeed seed = new();
        User admin = seed.User();
        Track first = seed.Track("First");
        Track second = seed.Track("Second");
        seed.Track("Third");
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());
        (await repository.CountAsync()).Should().Be(new TrackReviewCounts(3, 0));

        int changed = await repository.MarkReviewedAsync([first.Id, second.Id, Guid.NewGuid()], admin.Id, _reviewedAt);

        changed.Should().Be(2);
        (await repository.FindAsync(first.Id)).Should().Be(
            new TrackReviewStateRow(first.Id, _reviewedAt, admin.Id, admin.Name));
        (await repository.CountAsync()).Should().Be(new TrackReviewCounts(1, 2));
    }

    [Test]
    public async Task MarkReviewedAsync_ReviewedTrack_KeepsItsTimeAndProfile()
    {
        LibrarySeed seed = new();
        User first = seed.User();
        User second = seed.User();
        Track track = seed.Track("Track");
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());
        await repository.MarkReviewedAsync([track.Id], first.Id, _reviewedAt);

        int changed = await repository.MarkReviewedAsync([track.Id], second.Id, _reviewedAt.AddHours(1));

        changed.Should().Be(0);
        (await repository.FindAsync(track.Id)).Should().Be(
            new TrackReviewStateRow(track.Id, _reviewedAt, first.Id, first.Name));
    }

    [Test]
    public async Task MarkReviewedAsync_UnknownProfile_StoresTheTimeOnly()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Track");
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());

        int changed = await repository.MarkReviewedAsync([track.Id], Guid.NewGuid(), _reviewedAt);

        changed.Should().Be(1);
        (await repository.FindAsync(track.Id)).Should().Be(new TrackReviewStateRow(track.Id, _reviewedAt, null, null));
    }

    [Test]
    public async Task MarkUnreviewedAsync_ReviewedTrack_PutsItBackIntoTheInbox()
    {
        LibrarySeed seed = new();
        User admin = seed.User();
        Track reviewed = seed.Track("Reviewed");
        Track unreviewed = seed.Track("Unreviewed");
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());
        await repository.MarkReviewedAsync([reviewed.Id], admin.Id, _reviewedAt);

        int changed = await repository.MarkUnreviewedAsync([reviewed.Id, unreviewed.Id]);

        changed.Should().Be(1);
        (await repository.FindAsync(reviewed.Id)).Should().Be(new TrackReviewStateRow(reviewed.Id, null, null, null));
        (await repository.CountAsync()).Should().Be(new TrackReviewCounts(2, 0));
    }

    [Test]
    public async Task FindAsync_UnknownTrack_ReturnsNull()
    {
        TrackReviewRepository repository = new(CreateDbContext());

        (await repository.FindAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Test]
    public async Task DeleteProfile_ReviewedTrack_KeepsTheTimeWithoutTheProfile()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        Track track = seed.Track("Track");
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());
        await repository.MarkReviewedAsync([track.Id], user.Id, _reviewedAt);

        await using (WaveMpDbContext db = CreateDbContext())
        {
            await db.Users.Where(item => item.Id == user.Id).ExecuteDeleteAsync();
        }

        (await repository.FindAsync(track.Id)).Should().Be(new TrackReviewStateRow(track.Id, _reviewedAt, null, null));
    }

    [Test]
    public async Task ListAsync_Status_ListsOnlyTracksOfTheStatusWithTheirReviewer()
    {
        LibrarySeed seed = new();
        User admin = seed.User();
        Track reviewed = seed.Track("Reviewed");
        Track unreviewed = seed.Track("Unreviewed");
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());
        await repository.MarkReviewedAsync([reviewed.Id], admin.Id, _reviewedAt);

        Page<TrackReviewRow> open = await repository.ListAsync(Query(TrackReviewSortField.Title));
        Page<TrackReviewRow> done = await repository.ListAsync(
            Query(TrackReviewSortField.Title) with { Status = TrackReviewStatus.Reviewed });

        open.Items.Select(item => item.TrackId).Should().Equal(unreviewed.Id);
        open.Items[0].Review.IsReviewed.Should().BeFalse();
        done.Items.Should().ContainSingle().Which.Review.Should().Be(
            new TrackReviewStateRow(reviewed.Id, _reviewedAt, admin.Id, admin.Name));
    }

    [Test]
    public async Task ListAsync_TagsOfTheFiles_NamesTheMissingFields()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        Album album = seed.Album("Album", artist);
        Track complete = seed.Track("Complete", artist, album, number: 1);
        seed.File("Album/Complete.flac", MediaFileKind.Audio, complete).Tags = CompleteTags();
        // Only the primary audio file counts; the lower quality duplicate has no tags.
        seed.File("Album/Complete.mp3", MediaFileKind.Audio, complete, isPrimary: false);
        Track partial = seed.Track("Partial", artist, album, number: 2);
        seed.File("Album/Partial.mp3", MediaFileKind.Audio, partial).Tags = new MediaFileTags
        {
            Title = "Partial",
            Artist = "Band",
            Year = 2001,
        };
        Track videoOnly = seed.Track("Clip", hasAudio: false, hasVideo: true);
        seed.File("Clip.mp4", MediaFileKind.Video, videoOnly).Tags = CompleteTags();
        Track untagged = seed.Track("Untagged");
        seed.File("Untagged.mp3", MediaFileKind.Audio, untagged);
        MediaFile picture = seed.File("Album/cover.jpg", MediaFileKind.Image);
        seed.AlbumCover(album, picture);
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());

        Page<TrackReviewRow> page = await repository.ListAsync(Query(TrackReviewSortField.Title));

        page.Items.Select(item => (item.Title, item.MissingTags)).Should().BeEquivalentTo(
            [
                ("Clip", (IReadOnlyList<MetadataTag>)[]),
                ("Complete", []),
                ("Partial", [MetadataTag.Album, MetadataTag.TrackNumber, MetadataTag.Genre]),
                ("Untagged", Enum.GetValues<MetadataTag>()),
            ],
            options => options.WithStrictOrdering());
        TrackReviewRow first = page.Items.Single(item => item.Title == "Complete");
        first.Should().BeEquivalentTo(new
        {
            ArtistName = "Band",
            AlbumTitle = "Album",
            AlbumArtistName = "Band",
            DirectoryPath = "Album",
            HasAudio = true,
            HasVideo = false,
            HasCover = true,
        });
        page.Items.Single(item => item.Title == "Untagged").HasCover.Should().BeFalse();
    }

    [TestCase(true, new[] { "Partial", "Untagged" })]
    [TestCase(false, new[] { "Complete" })]
    public async Task ListAsync_MissingTags_FiltersByTheTags(bool missing, string[] expected)
    {
        LibrarySeed seed = new();
        Track complete = seed.Track("Complete");
        seed.File("Complete.flac", MediaFileKind.Audio, complete).Tags = CompleteTags();
        Track partial = seed.Track("Partial");
        seed.File("Partial.flac", MediaFileKind.Audio, partial).Tags = new MediaFileTags { Title = "Partial" };
        Track untagged = seed.Track("Untagged");
        seed.File("Untagged.flac", MediaFileKind.Audio, untagged);
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());

        Page<TrackReviewRow> page = await repository.ListAsync(
            Query(TrackReviewSortField.Title) with { MissingTags = missing });

        page.Items.Select(item => item.Title).Should().Equal(expected);
        page.Total.Should().Be(expected.Length);
    }

    [Test]
    public async Task ListAsync_Search_FindsTitleArtistAlbumAndPath()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Needle Band");
        Album album = seed.Album("Haystack");
        seed.Track("By Title Needle");
        seed.Track("By Artist", artist);
        seed.Track("On Album", album: album);
        seed.Track("Other");
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());

        Page<TrackReviewRow> byText = await repository.ListAsync(
            Query(TrackReviewSortField.Title) with { Search = "needle" });
        Page<TrackReviewRow> byAlbum = await repository.ListAsync(
            Query(TrackReviewSortField.Title) with { Search = "HAYSTACK" });

        byText.Items.Select(item => item.Title).Should().Equal("By Artist", "By Title Needle");
        byAlbum.Items.Select(item => item.Title).Should().Equal("On Album");
    }

    [Test]
    public async Task ListAsync_SortKeys_SortTheTracks()
    {
        LibrarySeed seed = new();
        Artist alpha = seed.Artist("Alpha");
        Artist beta = seed.Artist("Beta");
        Album zulu = seed.Album("Zulu", alpha);
        Album yankee = seed.Album("Yankee", beta);
        seed.Track("Second", alpha, zulu, number: 2, firstSeenAt: LibrarySeed.DefaultTime.AddDays(1));
        seed.Track("First", alpha, zulu, number: 1, firstSeenAt: LibrarySeed.DefaultTime.AddDays(1));
        seed.Track("Ballad", beta, yankee, number: 1, firstSeenAt: LibrarySeed.DefaultTime.AddDays(2));
        seed.Track("Old", firstSeenAt: LibrarySeed.DefaultTime);
        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());

        async Task<string[]> TitlesAsync(TrackReviewSortField sort, SortDirection direction)
        {
            Page<TrackReviewRow> page = await repository.ListAsync(Query(sort) with { Direction = direction });
            return [.. page.Items.Select(item => item.Title)];
        }

        (await TitlesAsync(TrackReviewSortField.FirstSeenAt, SortDirection.Desc))
            .Should().Equal("Ballad", "First", "Second", "Old");
        (await TitlesAsync(TrackReviewSortField.Artist, SortDirection.Asc))
            .Should().Equal("First", "Second", "Ballad", "Old");
        (await TitlesAsync(TrackReviewSortField.Album, SortDirection.Asc))
            .Should().Equal("Ballad", "First", "Second", "Old");
        (await TitlesAsync(TrackReviewSortField.Title, SortDirection.Desc))
            .Should().Equal("Second", "Old", "First", "Ballad");
    }

    [Test]
    public async Task ListAsync_Paging_ReturnsThePageAndTheTotal()
    {
        LibrarySeed seed = new();
        foreach (string title in new[] { "A", "B", "C", "D", "E" })
        {
            seed.Track(title);
        }

        await SaveAsync(seed);
        TrackReviewRepository repository = new(CreateDbContext());

        Page<TrackReviewRow> page = await repository.ListAsync(
            Query(TrackReviewSortField.Title) with { Offset = 2, Limit = 2 });

        page.Items.Select(item => item.Title).Should().Equal("C", "D");
        page.Total.Should().Be(5);
    }

    private static TrackReviewListQuery Query(TrackReviewSortField sort)
    {
        return new TrackReviewListQuery(0, 50, sort, SortDirection.Asc);
    }

    private static MediaFileTags CompleteTags()
    {
        return new MediaFileTags
        {
            Title = "Title",
            Artist = "Band",
            Album = "Album",
            Year = 2001,
            TrackNumber = 1,
            Genre = "Rock",
        };
    }
}
