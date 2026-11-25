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
public class LibraryIssueRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _firstRun = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _secondRun = _firstRun.AddHours(1);

    protected override string DatabaseName => "wavemp_tests_library_issues";

    [Test]
    public async Task ListTracksAsync_Tracks_ReportCoversAndTheMissingTagsOfTheFileTheTrackPlays()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Artist");
        Album withCover = seed.Album("With Cover", artist);
        Album viaOtherTrack = seed.Album("Via Other Track", artist);
        Album withoutCover = seed.Album("Without Cover", artist);
        Track albumCover = seed.Track("Album Cover", artist, withCover, duration: TimeSpan.FromMinutes(3));
        Track sibling = seed.Track("Sibling", artist, viaOtherTrack);
        Track ownCover = seed.Track("Own Cover", artist, viaOtherTrack);
        Track noCover = seed.Track("No Cover", artist, withoutCover);
        Track videoOnly = seed.Track("Video Only", hasAudio: false, hasVideo: true);
        Track noFile = seed.Track("No File");
        seed.AlbumCover(withCover, seed.File("With Cover/cover.jpg", MediaFileKind.Image, isPrimary: false));
        seed.TrackCover(ownCover, seed.File("Via Other Track/Own Cover.jpg", MediaFileKind.Image, ownCover));
        Tags(seed.File("With Cover/Album Cover.mp3", MediaFileKind.Audio, albumCover), "T", "A", "B");
        Tags(seed.File("With Cover/Album Cover.mkv", MediaFileKind.Video, albumCover), null, null, null);
        Tags(seed.File("Via Other Track/Sibling.mp3", MediaFileKind.Audio, sibling), "T", null, null);
        Tags(seed.File("Via Other Track/Old.mp3", MediaFileKind.Audio, sibling, isPrimary: false), "T", "A", "B");
        Tags(seed.File("Via Other Track/Own Cover.mp3", MediaFileKind.Audio, ownCover), null, "A", "B");
        Tags(seed.File("Without Cover/No Cover.mp3", MediaFileKind.Audio, noCover), "T", "A", null);
        Tags(seed.File("Video Only.mkv", MediaFileKind.Video, videoOnly), "T", "A", "B");
        await SaveAsync(seed);
        LibraryIssueRepository repository = new(CreateDbContext());

        IReadOnlyList<LibraryCheckTrackRow> rows = await repository.ListTracksAsync();

        rows.Should().HaveCount(6);
        Row(rows, albumCover).Should().BeEquivalentTo(new LibraryCheckTrackRow(
            albumCover.Id,
            "Album Cover",
            artist.Id,
            withCover.Id,
            TimeSpan.FromMinutes(3),
            true,
            false,
            true,
            []));
        Row(rows, sibling).HasCover.Should().BeFalse("a track never shows the cover of another track");
        Row(rows, ownCover).HasCover.Should().BeTrue();
        Row(rows, sibling).MissingTags.Should().Equal(MetadataTag.Artist, MetadataTag.Album);
        Row(rows, ownCover).MissingTags.Should().Equal(MetadataTag.Title);
        Row(rows, noCover).HasCover.Should().BeFalse();
        Row(rows, noCover).MissingTags.Should().Equal(MetadataTag.Album);
        Row(rows, videoOnly).MissingTags.Should().BeEmpty("the tags of the video count for a track without audio");
        Row(rows, videoOnly).HasCover.Should().BeFalse();
        Row(rows, noFile).MissingTags.Should().Equal(MetadataTag.Title, MetadataTag.Artist, MetadataTag.Album);
    }

    [Test]
    public async Task ListTracksAsync_OnlineCovers_CountAsCoversOfTheirTrackOnly()
    {
        LibrarySeed seed = new();
        Album album = seed.Album("Album");
        Track first = seed.Track("First", album: album);
        Track second = seed.Track("Second", album: album);
        Track single = seed.Track("Single");
        Track without = seed.Track("Without");
        await SaveAsync(seed);
        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            dbContext.OnlineCovers.AddRange(OnlineCover(trackId: first.Id), OnlineCover(trackId: single.Id));
            await dbContext.SaveChangesAsync();
        }

        LibraryIssueRepository repository = new(CreateDbContext());

        IReadOnlyList<LibraryCheckTrackRow> rows = await repository.ListTracksAsync();

        Row(rows, first).HasCover.Should().BeTrue();
        Row(rows, second).HasCover.Should().BeFalse("a track never shows the cover of another track");
        Row(rows, single).HasCover.Should().BeTrue();
        Row(rows, without).HasCover.Should().BeFalse();
    }

    [Test]
    public async Task ListUnreadableFilesAsync_FilesWithIssues_AreListedWithTheirTrack()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Track");
        MediaFile broken = seed.File("B/Broken.mp3", MediaFileKind.Audio, track, issue: MediaFileIssue.Unreadable);
        MediaFile picture = seed.File("A/Picture.jpg", MediaFileKind.Image, issue: MediaFileIssue.Unreadable);
        seed.File("C/Fine.mp3", MediaFileKind.Audio, track, isPrimary: false);
        MediaFile tags = seed.File("C/Tags.mp3", MediaFileKind.Audio, issue: MediaFileIssue.TagsUnreadable);
        await SaveAsync(seed);
        LibraryIssueRepository repository = new(CreateDbContext());

        IReadOnlyList<LibraryCheckFileRow> rows = await repository.ListUnreadableFilesAsync();

        rows.Should().Equal(
            new LibraryCheckFileRow(picture.Id, null, MediaFileIssue.Unreadable),
            new LibraryCheckFileRow(broken.Id, track.Id, MediaFileIssue.Unreadable),
            new LibraryCheckFileRow(tags.Id, null, MediaFileIssue.TagsUnreadable));
    }

    [Test]
    public async Task ReplaceAsync_FirstRun_StoresTheProblemsAndTheTimeOfTheRun()
    {
        LibrarySeed seed = new();
        Track first = seed.Track("First");
        Track second = seed.Track("Second");
        MediaFile file = seed.File("Broken.mp3", MediaFileKind.Audio, first, issue: MediaFileIssue.ProbeFailed);
        await SaveAsync(seed);
        await ClearChecksAsync();
        LibraryIssueRepository repository = new(CreateDbContext());
        (await repository.FindLastCheckAsync()).Should().BeNull();

        LibraryIssueChanges changes = await repository.ReplaceAsync(
            [
                new DetectedLibraryIssue(LibraryIssueType.MissingCover, first.Id),
                new DetectedLibraryIssue(
                    LibraryIssueType.MissingTags,
                    second.Id,
                    MissingTags: [MetadataTag.Artist, MetadataTag.Album]),
                new DetectedLibraryIssue(LibraryIssueType.PossibleDuplicate, first.Id, RelatedTrackId: second.Id),
                new DetectedLibraryIssue(
                    LibraryIssueType.UnreadableFile,
                    first.Id,
                    file.Id,
                    MediaFileIssue.ProbeFailed),
            ],
            2,
            _firstRun);

        changes.Should().Be(new LibraryIssueChanges(4, 0, 0));
        (await repository.FindLastCheckAsync()).Should().BeEquivalentTo(
            new { Id = LibraryCheck.SingletonId, CheckedAt = _firstRun, TrackCount = 2 });
        await using WaveMpDbContext dbContext = CreateDbContext();
        List<LibraryIssue> stored = await dbContext.LibraryIssues.ToListAsync();
        stored.Should().HaveCount(4).And.AllSatisfy(issue => issue.DetectedAt.Should().Be(_firstRun));
        stored.Single(issue => issue.Type == LibraryIssueType.MissingTags).MissingTags
            .Should().Equal(MetadataTag.Artist, MetadataTag.Album);
        stored.Single(issue => issue.Type == LibraryIssueType.PossibleDuplicate).RelatedTrackId.Should().Be(second.Id);
        stored.Single(issue => issue.Type == LibraryIssueType.UnreadableFile)
            .Should().BeEquivalentTo(new { MediaFileId = (Guid?)file.Id, FileIssue = MediaFileIssue.ProbeFailed });
    }

    [Test]
    public async Task ReplaceAsync_SecondRun_KeepsFoundProblemsUpdatesTheirDetailsAndRemovesTheOthers()
    {
        LibrarySeed seed = new();
        Track first = seed.Track("First");
        Track second = seed.Track("Second");
        Track third = seed.Track("Third");
        await SaveAsync(seed);
        LibraryIssueRepository repository = new(CreateDbContext());
        await repository.ReplaceAsync(
            [
                new DetectedLibraryIssue(LibraryIssueType.MissingCover, first.Id),
                new DetectedLibraryIssue(LibraryIssueType.MissingTags, second.Id, MissingTags: [MetadataTag.Title]),
                new DetectedLibraryIssue(LibraryIssueType.UnknownAlbum, third.Id),
            ],
            3,
            _firstRun);
        Guid keptId = await IdOfAsync(LibraryIssueType.MissingTags, second.Id);

        LibraryIssueChanges changes = await repository.ReplaceAsync(
            [
                new DetectedLibraryIssue(
                    LibraryIssueType.MissingTags,
                    second.Id,
                    MissingTags: [MetadataTag.Title, MetadataTag.Album]),
                new DetectedLibraryIssue(LibraryIssueType.UnknownAlbum, third.Id),
                new DetectedLibraryIssue(LibraryIssueType.MissingCover, third.Id),
            ],
            3,
            _secondRun);

        changes.Should().Be(new LibraryIssueChanges(1, 1, 2));
        await using WaveMpDbContext dbContext = CreateDbContext();
        List<LibraryIssue> stored = await dbContext.LibraryIssues.OrderBy(issue => issue.Type).ToListAsync();
        stored.Select(issue => (issue.Type, issue.TrackId, issue.DetectedAt)).Should().BeEquivalentTo(
        [
            (LibraryIssueType.MissingCover, (Guid?)third.Id, _secondRun),
            (LibraryIssueType.MissingTags, (Guid?)second.Id, _firstRun),
            (LibraryIssueType.UnknownAlbum, (Guid?)third.Id, _firstRun),
        ]);
        LibraryIssue kept = stored.Single(issue => issue.Type == LibraryIssueType.MissingTags);
        kept.Id.Should().Be(keptId);
        kept.MissingTags.Should().Equal(MetadataTag.Title, MetadataTag.Album);
        (await repository.FindLastCheckAsync())!.CheckedAt.Should().Be(_secondRun);
    }

    [Test]
    public async Task ReplaceAsync_TracksAndFilesRemovedMeanwhile_AreSkipped()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Track");
        await SaveAsync(seed);
        LibraryIssueRepository repository = new(CreateDbContext());

        LibraryIssueChanges changes = await repository.ReplaceAsync(
            [
                new DetectedLibraryIssue(LibraryIssueType.MissingCover, Guid.NewGuid()),
                new DetectedLibraryIssue(
                    LibraryIssueType.UnreadableFile,
                    null,
                    Guid.NewGuid(),
                    MediaFileIssue.Unreadable),
                new DetectedLibraryIssue(LibraryIssueType.PossibleDuplicate, track.Id, RelatedTrackId: Guid.NewGuid()),
            ],
            1,
            _firstRun);

        changes.Should().Be(new LibraryIssueChanges(1, 0, 0));
        await using WaveMpDbContext dbContext = CreateDbContext();
        LibraryIssue stored = await dbContext.LibraryIssues.SingleAsync();
        stored.Should().BeEquivalentTo(new
        {
            Type = LibraryIssueType.PossibleDuplicate,
            TrackId = (Guid?)track.Id,
            RelatedTrackId = (Guid?)null,
        });
    }

    [Test]
    public async Task DeletingTracks_RemovesTheirProblemsAndClearsTheRelatedTrack()
    {
        LibrarySeed seed = new();
        Track kept = seed.Track("Kept");
        Track removed = seed.Track("Removed");
        await SaveAsync(seed);
        LibraryIssueRepository repository = new(CreateDbContext());
        await repository.ReplaceAsync(
            [
                new DetectedLibraryIssue(LibraryIssueType.PossibleDuplicate, kept.Id, RelatedTrackId: removed.Id),
                new DetectedLibraryIssue(LibraryIssueType.PossibleDuplicate, removed.Id, RelatedTrackId: kept.Id),
            ],
            2,
            _firstRun);

        await using WaveMpDbContext dbContext = CreateDbContext();
        await dbContext.Tracks.Where(track => track.Id == removed.Id).ExecuteDeleteAsync();

        LibraryIssue left = await dbContext.LibraryIssues.SingleAsync();
        left.TrackId.Should().Be(kept.Id);
        left.RelatedTrackId.Should().BeNull();
    }

    [Test]
    public async Task ListAsync_Problems_AreListedWithTrackFileAndDetailsByPath()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Artist");
        Album album = seed.Album("Album", artist);
        Track first = seed.Track("First", artist, album, duration: TimeSpan.FromSeconds(200));
        Track second = seed.Track("Second", artist, album, duration: TimeSpan.FromSeconds(201));
        MediaFile picture = seed.File("Album/broken.jpg", MediaFileKind.Image, issue: MediaFileIssue.Unreadable);
        await SaveAsync(seed);
        LibraryIssueRepository repository = new(CreateDbContext());
        await repository.ReplaceAsync(
            [
                new DetectedLibraryIssue(LibraryIssueType.PossibleDuplicate, second.Id, RelatedTrackId: first.Id),
                new DetectedLibraryIssue(LibraryIssueType.UnreadableFile, null, picture.Id, MediaFileIssue.Unreadable),
                new DetectedLibraryIssue(LibraryIssueType.MissingTags, first.Id, MissingTags: [MetadataTag.Title]),
                new DetectedLibraryIssue(LibraryIssueType.MissingCover, first.Id),
            ],
            2,
            _firstRun);

        Page<LibraryIssueRow> page = await repository.ListAsync(
            new LibraryIssueListQuery(0, 10, LibraryIssueSortField.Path, SortDirection.Asc));

        page.Total.Should().Be(4);
        // The base names of the seed start with a counter: "0001 First", "0002 Second"; the file is "broken.jpg".
        page.Items.Select(item => item.Type).Should().Equal(
            LibraryIssueType.MissingCover,
            LibraryIssueType.MissingTags,
            LibraryIssueType.PossibleDuplicate,
            LibraryIssueType.UnreadableFile);
        LibraryIssueRow duplicate = page.Items[2];
        duplicate.Track.Should().BeEquivalentTo(new
        {
            second.Id,
            Title = "Second",
            ArtistName = "Artist",
            AlbumTitle = "Album",
            AlbumArtistName = "Artist",
            SourceId = MediaSource.DefaultId,
            DirectoryPath = "Album",
            Duration = (TimeSpan?)TimeSpan.FromSeconds(201),
            HasAudio = true,
        });
        duplicate.RelatedTrack!.Id.Should().Be(first.Id);
        duplicate.File.Should().BeNull();
        page.Items[1].MissingTags.Should().Equal(MetadataTag.Title);
        LibraryIssueRow unreadable = page.Items[3];
        unreadable.Track.Should().BeNull();
        unreadable.File.Should().BeEquivalentTo(new
        {
            picture.Id,
            RelativePath = "Album/broken.jpg",
            Kind = MediaFileKind.Image,
            SourceId = MediaSource.DefaultId,
        });
        unreadable.FileIssue.Should().Be(MediaFileIssue.Unreadable);
    }

    [Test]
    public async Task ListAsync_TypesAndSearch_FilterTheProblems()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Needle Artist");
        Track first = seed.Track("First", artist);
        Track second = seed.Track("Second");
        MediaFile file = seed.File("Folder/needle.mp3", MediaFileKind.Audio, issue: MediaFileIssue.Unreadable);
        await SaveAsync(seed);
        LibraryIssueRepository repository = new(CreateDbContext());
        await repository.ReplaceAsync(
            [
                new DetectedLibraryIssue(LibraryIssueType.MissingCover, first.Id),
                new DetectedLibraryIssue(LibraryIssueType.MissingCover, second.Id),
                new DetectedLibraryIssue(LibraryIssueType.UnknownAlbum, first.Id),
                new DetectedLibraryIssue(LibraryIssueType.UnreadableFile, null, file.Id, MediaFileIssue.Unreadable),
            ],
            2,
            _firstRun);

        Page<LibraryIssueRow> covers = await repository.ListAsync(new LibraryIssueListQuery(
            0, 10, LibraryIssueSortField.Title, SortDirection.Asc, [LibraryIssueType.MissingCover]));
        Page<LibraryIssueRow> needle = await repository.ListAsync(new LibraryIssueListQuery(
            0, 10, LibraryIssueSortField.Path, SortDirection.Asc, Search: "NEEDLE"));

        covers.Items.Select(item => item.Track!.Title).Should().Equal("First", "Second");
        needle.Items.Select(item => item.Type).Should().BeEquivalentTo(
        [
            LibraryIssueType.MissingCover,
            LibraryIssueType.UnknownAlbum,
            LibraryIssueType.UnreadableFile,
        ]);
    }

    [Test]
    public async Task CountAsync_Problems_AreCountedPerType()
    {
        LibrarySeed seed = new();
        Track first = seed.Track("First");
        Track second = seed.Track("Second");
        await SaveAsync(seed);
        LibraryIssueRepository repository = new(CreateDbContext());
        await repository.ReplaceAsync(
            [
                new DetectedLibraryIssue(LibraryIssueType.MissingCover, first.Id),
                new DetectedLibraryIssue(LibraryIssueType.MissingCover, second.Id),
                new DetectedLibraryIssue(LibraryIssueType.AudioWithoutVideo, second.Id),
            ],
            2,
            _firstRun);

        IReadOnlyDictionary<LibraryIssueType, int> counts = await repository.CountAsync();

        counts.Should().BeEquivalentTo(new Dictionary<LibraryIssueType, int>
        {
            [LibraryIssueType.MissingCover] = 2,
            [LibraryIssueType.AudioWithoutVideo] = 1,
        });
    }

    private static void Tags(MediaFile file, string? title, string? artist, string? album)
    {
        file.Tags = new MediaFileTags { Title = title, Artist = artist, Album = album };
    }

    private static LibraryCheckTrackRow Row(IReadOnlyList<LibraryCheckTrackRow> rows, Track track)
    {
        return rows.Single(row => row.TrackId == track.Id);
    }

    private static OnlineCover OnlineCover(Guid? albumId = null, Guid? trackId = null)
    {
        return new OnlineCover
        {
            Id = Guid.CreateVersion7(),
            AlbumId = albumId,
            TrackId = trackId,
            FileName = Guid.NewGuid().ToString("N") + ".jpg",
            MimeType = "image/jpeg",
            Size = 100,
            ReleaseId = Guid.NewGuid(),
            CreatedAt = LibrarySeed.DefaultTime,
        };
    }

    private async Task ClearChecksAsync()
    {
        await using WaveMpDbContext dbContext = CreateDbContext();
        await dbContext.LibraryChecks.ExecuteDeleteAsync();
    }

    private async Task<Guid> IdOfAsync(LibraryIssueType type, Guid trackId)
    {
        await using WaveMpDbContext dbContext = CreateDbContext();
        return await dbContext.LibraryIssues
            .Where(issue => issue.Type == type && issue.TrackId == trackId)
            .Select(issue => issue.Id)
            .SingleAsync();
    }
}
