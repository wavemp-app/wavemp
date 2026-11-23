using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class LibraryScanRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _day = LibrarySeed.DefaultTime;

    protected override string DatabaseName => "wavemp_tests_library_scan";

    [Test]
    public async Task GetFolderSnapshotsAsync_Folder_ReturnsOnlyItsOwnFiles()
    {
        await SeedFilesAsync();

        IReadOnlyList<MediaFileSnapshot> snapshots = await CreateRepository().GetFolderSnapshotsAsync(MediaSource.DefaultId, "Band/Album");

        snapshots.Select(snapshot => snapshot.RelativePath)
            .Should().BeEquivalentTo("Band/Album/01 - One.mp3", "Band/Album/cover.jpg");
        snapshots.Should().AllSatisfy(snapshot => snapshot.DirectoryPath.Should().Be("Band/Album"));
    }

    [Test]
    public async Task GetFolderSnapshotsAsync_MediaFolder_ReturnsTheFilesOfTheRootOnly()
    {
        await SeedFilesAsync();

        IReadOnlyList<MediaFileSnapshot> snapshots = await CreateRepository().GetFolderSnapshotsAsync(MediaSource.DefaultId, string.Empty);

        snapshots.Select(snapshot => snapshot.RelativePath).Should().Equal("Loose.mp3");
    }

    [Test]
    public async Task GetFolderSnapshotsAsync_UnknownFolder_ReturnsNothing()
    {
        await SeedFilesAsync();

        IReadOnlyList<MediaFileSnapshot> snapshots = await CreateRepository().GetFolderSnapshotsAsync(MediaSource.DefaultId, "Band/Alb");

        snapshots.Should().BeEmpty();
    }

    [Test]
    public async Task GetFolderSnapshotsAsync_SameFolderInAnotherSource_ReturnsOnlyTheFilesOfTheSource()
    {
        LibrarySeed seed = new();
        MediaSource other = seed.Source();
        seed.File("Band/Album/01 - One.mp3", MediaFileKind.Audio);
        seed.File("Band/Album/01 - One.mp3", MediaFileKind.Audio, source: other);
        seed.File("Band/Album/02 - Two.mp3", MediaFileKind.Audio, source: other);
        await SaveAsync(seed);

        IReadOnlyList<MediaFileSnapshot> snapshots =
            await CreateRepository().GetFolderSnapshotsAsync(other.Id, "Band/Album");

        snapshots.Select(snapshot => snapshot.RelativePath)
            .Should().BeEquivalentTo("Band/Album/01 - One.mp3", "Band/Album/02 - Two.mp3");
    }

    [Test]
    public async Task CountFilesAndDeleteDirectories_OfASource_LeaveTheOtherSourcesAlone()
    {
        LibrarySeed seed = new();
        MediaSource other = seed.Source();
        seed.File("Band/Song.mp3", MediaFileKind.Audio);
        seed.File("Band/Song.mp3", MediaFileKind.Audio, source: other);
        seed.File("Band/Other.mp3", MediaFileKind.Audio, source: other);
        await SaveAsync(seed);
        LibraryScanRepository repository = CreateRepository();

        int before = await repository.CountFilesAsync(other.Id);
        await repository.DeleteDirectoriesAsync(other.Id, ["Band"], _day);

        before.Should().Be(2);
        (await CreateRepository().CountFilesAsync(other.Id)).Should().Be(0);
        (await CreateRepository().CountFilesAsync(MediaSource.DefaultId)).Should().Be(1);
    }

    [Test]
    public async Task DeleteDirectoriesAsync_SameFilesElsewhere_RecordsAMoveToTheirAlbumAndDeletesTheFolder()
    {
        LibrarySeed seed = new();
        MediaSource other = seed.Source();
        Album old = seed.Album("Hits");
        Album renamed = seed.Album("Hits");
        Album moved = seed.Album("Hits");
        SongIn(seed, old, "Hits/01.mp3", 1000);
        SongIn(seed, old, "Hits/02.mp3", 2000);
        SongIn(seed, old, "Hits/03.mp3", 3000);
        Track video = SongIn(seed, old, "Hits/04.mkv", 4000, MediaFileKind.Video);
        seed.File("Hits/04.mp3", MediaFileKind.Audio, video, size: 5000);
        SongIn(seed, renamed, "Hits 2020/01.mp3", 1000);
        SongIn(seed, renamed, "Hits 2020/02.mp3", 2000);
        Track movedVideo = SongIn(seed, moved, "Sampler/Hits/04.mkv", 4000, MediaFileKind.Video, other);
        seed.File("Sampler/Hits/04.mp3", MediaFileKind.Audio, movedVideo, size: 5000);
        SongIn(seed, moved, "Sampler/Hits/03.mp3", 3000, source: other);
        await SaveAsync(seed);
        await AddMovesAsync((old, moved, 1));
        DateTimeOffset now = _day.AddDays(30);

        (int Files, int Tracks) deleted =
            await CreateRepository().DeleteDirectoriesAsync(MediaSource.DefaultId, ["Hits"], now);

        // A song is counted once, also when its audio and its video are found.
        deleted.Should().Be((5, 4));
        (await MovesAsync()).Should().BeEquivalentTo([(old.Id, renamed.Id, 2, now), (old.Id, moved.Id, 3, now)]);
        WaveMpDbContext db = CreateDbContext();
        (await db.Tracks.CountAsync(track => track.AlbumId == old.Id)).Should().Be(0);
    }

    [Test]
    public async Task DeleteDirectoriesAsync_OtherSizeNameOrAlbum_RecordsNoMove()
    {
        LibrarySeed seed = new();
        Album old = seed.Album("Hits");
        Album deletedToo = seed.Album("Hits");
        Album other = seed.Album("Hits");
        SongIn(seed, old, "Hits/01.mp3", 1000);
        SongIn(seed, old, "Hits/02.mp3", 2000);
        SongIn(seed, old, "Hits/03.mp3", 3000);
        SongIn(seed, old, "Hits/04.mp3", 4000);
        SongIn(seed, old, "Hits/05.mp3", 5000);
        SongIn(seed, old, "Hits/06.mp3", 6000);
        SongIn(seed, other, "Hits 2020/01.mp3", 1001);
        SongIn(seed, other, "Hits 2020/02.MP3", 2000);
        SongIn(seed, other, "Hits 2020/03.mp3", 3000, MediaFileKind.Video);
        Track copy = SongIn(seed, other, "Hits 2020/04.flac", 4000);
        seed.File("Hits 2020/04.mp3", MediaFileKind.Audio, copy, isPrimary: false, size: 4000);
        SongIn(seed, deletedToo, "Hits Old/05.mp3", 5000);
        SongIn(seed, old, "Hits Copy/06.mp3", 6000);
        await SaveAsync(seed);

        await CreateRepository().DeleteDirectoriesAsync(MediaSource.DefaultId, ["Hits", "Hits Old"], _day);

        (await MovesAsync()).Should().BeEmpty();
        WaveMpDbContext db = CreateDbContext();
        (await db.Tracks.Select(track => track.DirectoryPath).Distinct().ToListAsync())
            .Should().BeEquivalentTo("Hits 2020", "Hits Copy");
    }

    [Test]
    public async Task GetSnapshotsAsync_FilesOfATrack_CarryTheTrackAndWhetherItPlaysThem()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Song", album: seed.Album("Album"));
        MediaFile primary = seed.File("Album/Song.flac", MediaFileKind.Audio, track);
        MediaFile duplicate = seed.File("Album/Song.mp3", MediaFileKind.Audio, track, isPrimary: false);
        MediaFile cover = seed.File("Album/cover.jpg", MediaFileKind.Image, isPrimary: false);
        await SaveAsync(seed);
        LibraryScanRepository repository = CreateRepository();

        IReadOnlyList<MediaFileSnapshot> ofSource = await repository.GetFileSnapshotsAsync(MediaSource.DefaultId, null);
        IReadOnlyList<MediaFileSnapshot> ofFolders =
            await repository.GetFileSnapshotsAsync(MediaSource.DefaultId, ["Album"]);
        IReadOnlyList<MediaFileSnapshot> ofFolder =
            await repository.GetFolderSnapshotsAsync(MediaSource.DefaultId, "Album");

        (Guid, Guid?, bool)[] expected =
        [
            (primary.Id, track.Id, true),
            (duplicate.Id, track.Id, false),
            (cover.Id, null, false),
        ];
        foreach (IReadOnlyList<MediaFileSnapshot> snapshots in new[] { ofSource, ofFolders, ofFolder })
        {
            snapshots.Select(snapshot => (snapshot.Id, snapshot.TrackId, snapshot.IsPrimary))
                .Should().BeEquivalentTo(expected);
        }
    }

    [Test]
    public async Task LoadTracksAsync_Ids_LoadsTheTracksTrackedWithTheirFilesCoversAndSelections()
    {
        LibrarySeed seed = new();
        Album album = seed.Album("Album");
        Track track = seed.Track("Song", album: album);
        MediaFile audio = seed.File("Album/Song.mp3", MediaFileKind.Audio, track);
        MediaFile picture = seed.File("Album/Song.jpg", MediaFileKind.Image, track, isPrimary: false);
        seed.TrackCover(track, picture);
        Track other = seed.Track("Other", album: album);
        seed.File("Album/Other.mp3", MediaFileKind.Audio, other);
        await SaveAsync(seed);
        WaveMpDbContext setup = CreateDbContext();
        await setup.MediaFiles
            .Where(file => file.Id == audio.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.Tags.Title, "Tagged"));
        setup.TrackFileSelections.Add(new TrackFileSelection
        {
            TrackId = track.Id,
            Category = FileSelectionCategory.Audio,
            MediaFileId = audio.Id,
            RelativePath = audio.RelativePath,
            IsMissing = false,
            SelectedAt = _day,
        });
        await setup.SaveChangesAsync();
        WaveMpDbContext db = CreateDbContext();
        LibraryScanRepository repository = new(db);

        IReadOnlyList<Track> loaded = await repository.LoadTracksAsync([track.Id, Guid.NewGuid(), track.Id]);

        Track result = loaded.Should().ContainSingle().Subject;
        result.Id.Should().Be(track.Id);
        result.Files.Select(file => file.Id).Should().BeEquivalentTo([audio.Id, picture.Id]);
        result.Files.Single(file => file.Id == audio.Id).Tags.Title.Should().Be("Tagged");
        result.Covers.Should().ContainSingle().Which.MediaFileId.Should().Be(picture.Id);
        result.FileSelections.Should().ContainSingle().Which.MediaFileId.Should().Be(audio.Id);
        db.Entry(result).State.Should().Be(EntityState.Unchanged);

        result.DirectoryPath = "Moved";
        await repository.SaveChangesAsync();
        (await CreateDbContext().Tracks.SingleAsync(item => item.Id == track.Id)).DirectoryPath.Should().Be("Moved");
        (await repository.LoadTracksAsync([])).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteVanishedTracksAsync_TracksStillInTheirPlace_AreDeletedWithTheirFilesAndData()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        Track gone = SongIn(seed, seed.Album("Old"), "Old/Song.mp3", 1000);
        MediaFile picture = seed.File("Old/Song.jpg", MediaFileKind.Image, gone, isPrimary: false);
        seed.Favorite(user, gone);
        Track stays = SongIn(seed, seed.Album("Stays"), "Stays/Song.mp3", 1000);
        await SaveAsync(seed);
        WaveMpDbContext db = CreateDbContext();
        Guid audioId = await db.MediaFiles
            .Where(file => file.RelativePath == "Old/Song.mp3")
            .Select(file => file.Id)
            .SingleAsync();

        VanishedTrack vanished = new(
            gone.Id,
            MediaSource.DefaultId,
            "Old",
            [(picture.Id, "Old/Song.jpg"), (audioId, "Old/Song.mp3")]);

        int deleted = await CreateRepository().DeleteVanishedTracksAsync([vanished]);

        deleted.Should().Be(1);
        WaveMpDbContext after = CreateDbContext();
        (await after.Tracks.Select(track => track.Id).ToListAsync()).Should().Equal(stays.Id);
        (await after.MediaFiles.Select(file => file.RelativePath).ToListAsync()).Should().Equal("Stays/Song.mp3");
        (await after.Favorites.CountAsync(favorite => favorite.UserId == user.Id)).Should().Be(0);
        (await CreateRepository().DeleteVanishedTracksAsync([])).Should().Be(0);
    }

    [Test]
    public async Task DeleteVanishedTracksAsync_TracksKeptAtANewPlace_StayWithTheirMovedFilesAndData()
    {
        // Seeded as a scan leaves them: the moved file kept its id at its new path, the moved track its id in its new
        // folder; the picture that was not moved along is still stored at its old path.
        LibrarySeed seed = new();
        User user = seed.User();
        MediaSource other = seed.Source();
        Track moved = SongIn(seed, seed.Album("New"), "New/Song.mp3", 1000);
        MediaFile stale = seed.File("Old/Song.jpg", MediaFileKind.Image, moved, isPrimary: false);
        seed.Favorite(user, moved);
        Track renamed = SongIn(seed, seed.Album("Album"), "Album/Track.mp3", 2000);
        Track toOtherSource = SongIn(seed, seed.Album("Inbox"), "Inbox/Clip.mp3", 3000, source: other);
        await SaveAsync(seed);
        WaveMpDbContext db = CreateDbContext();
        Dictionary<string, Guid> ids =
            await db.MediaFiles.ToDictionaryAsync(file => file.RelativePath, file => file.Id);
        Guid source = MediaSource.DefaultId;

        int deleted = await CreateRepository().DeleteVanishedTracksAsync(
        [
            new VanishedTrack(
                moved.Id,
                source,
                "Old",
                [(stale.Id, "Old/Song.jpg"), (ids["New/Song.mp3"], "Old/Song.mp3")]),
            new VanishedTrack(renamed.Id, source, "Album", [(ids["Album/Track.mp3"], "Album/Song.mp3")]),
            new VanishedTrack(toOtherSource.Id, source, "Inbox", [(ids["Inbox/Clip.mp3"], "Inbox/Clip.mp3")]),
        ]);

        deleted.Should().Be(0);
        WaveMpDbContext after = CreateDbContext();
        (await after.Tracks.Select(track => track.Id).ToListAsync())
            .Should().BeEquivalentTo([moved.Id, renamed.Id, toOtherSource.Id]);
        (await after.MediaFiles.Select(file => file.RelativePath).ToListAsync())
            .Should().BeEquivalentTo("New/Song.mp3", "Album/Track.mp3", "Inbox/Clip.mp3");
        (await after.Favorites.CountAsync(favorite => favorite.UserId == user.Id && favorite.TrackId == moved.Id))
            .Should().Be(1);
    }

    [Test]
    public async Task RecordAlbumMovesAsync_NewAndStoredPairs_AreAddedAndCountedUpWithTheNextSave()
    {
        LibrarySeed seed = new();
        Album from = AlbumWithTrack(seed, "From");
        Album stored = AlbumWithTrack(seed, "Stored");
        Album added = AlbumWithTrack(seed, "Added");
        await SaveAsync(seed);
        await AddMovesAsync((from, stored, 2));
        DateTimeOffset now = _day.AddDays(30);
        LibraryScanRepository repository = CreateRepository();

        await repository.RecordAlbumMovesAsync(
            new Dictionary<(Guid FromAlbumId, Guid ToAlbumId), int>
            {
                [(from.Id, stored.Id)] = 1,
                [(from.Id, added.Id)] = 2,
                [(from.Id, from.Id)] = 3,
                [(stored.Id, added.Id)] = 0,
            },
            now);
        await repository.RecordAlbumMovesAsync(
            new Dictionary<(Guid FromAlbumId, Guid ToAlbumId), int> { [(from.Id, added.Id)] = 1 },
            now);

        (await MovesAsync()).Should().BeEquivalentTo([(from.Id, stored.Id, 2, _day)]);
        await repository.SaveChangesAsync();
        (await MovesAsync()).Should().BeEquivalentTo([(from.Id, stored.Id, 3, now), (from.Id, added.Id, 3, now)]);
    }

    [Test]
    public async Task RecordAlbumMovesAsync_ToAnAlbumAddedInTheSameSave_IsStoredWithIt()
    {
        LibrarySeed seed = new();
        Album from = AlbumWithTrack(seed, "From");
        await SaveAsync(seed);
        LibraryScanRepository repository = CreateRepository();
        Album created = new LibrarySeed().Album("Created");

        repository.Add(created);
        await repository.RecordAlbumMovesAsync(
            new Dictionary<(Guid FromAlbumId, Guid ToAlbumId), int> { [(from.Id, created.Id)] = 1 },
            _day);
        await repository.SaveChangesAsync();

        (await MovesAsync()).Should().BeEquivalentTo([(from.Id, created.Id, 1, _day)]);
    }

    [Test]
    public async Task DeleteOrphansAsync_Merge_CarriesTheDataOfAllOldAlbumsToTheNewOne()
    {
        LibrarySeed seed = new();
        User first = seed.User();
        User second = seed.User();
        Album larger = seed.Album("Hits", seed.Artist("A"), firstSeenAt: _day.AddDays(2));
        Album smaller = seed.Album("Hits", seed.Artist("B"), firstSeenAt: _day.AddDays(1));
        Album merged = AlbumWithTrack(seed, "Hits", _day.AddDays(5));
        seed.Favorite(first, larger, _day.AddDays(3));
        seed.Favorite(first, smaller, _day.AddDays(4));
        seed.Favorite(second, smaller, _day.AddDays(2));
        await SaveAsync(seed);
        await SetCoverPriorityAsync(smaller, CoverPriority.TrackFirst);
        Guid largerCover = await AddOnlineCoverAsync(larger);
        await AddOnlineCoverAsync(smaller);
        await AddPlaybackStateAsync(first, smaller);
        await AddMovesAsync((larger, merged, 2), (smaller, merged, 1));
        DateTimeOffset now = _day.AddDays(30);

        (int Albums, int Artists) deleted = await CreateRepository().DeleteOrphansAsync(now);

        deleted.Should().Be((2, 2));
        WaveMpDbContext db = CreateDbContext();
        Album album = await db.Albums.SingleAsync();
        (album.Id, album.CoverPriority, album.FirstSeenAt, album.UpdatedAt)
            .Should().Be((merged.Id, (CoverPriority?)CoverPriority.TrackFirst, _day.AddDays(1), now));
        (await FavoritesAsync(first, second)).Should().BeEquivalentTo([
            (first.Id, merged.Id, _day.AddDays(3)),
            (second.Id, merged.Id, _day.AddDays(2)),
        ]);
        (await OnlineCoversAsync()).Should().Equal((largerCover, merged.Id));
        (await SourceOfPlaybackAsync(first)).Should().Be(merged.Id);
        (await MovesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteOrphansAsync_Split_GivesFavoritesRuleAndDateToEveryPartAndTheOnlineCoverToTheLargest()
    {
        LibrarySeed seed = new();
        User first = seed.User();
        User second = seed.User();
        Album split = seed.Album("Hits", firstSeenAt: _day.AddDays(1));
        Album large = AlbumWithTrack(seed, "Hits", _day.AddDays(3));
        Album small = AlbumWithTrack(seed, "Hits", _day.AddDays(2));
        seed.Favorite(first, split, _day.AddDays(4));
        seed.Favorite(first, small, _day.AddDays(6));
        seed.Favorite(second, split, _day.AddDays(5));
        seed.Favorite(second, large, _day.AddDays(2));
        await SaveAsync(seed);
        await SetCoverPriorityAsync(split, CoverPriority.TrackFirst);
        await SetCoverPriorityAsync(small, CoverPriority.AlbumFirst);
        Guid onlineCover = await AddOnlineCoverAsync(split);
        await AddPlaybackStateAsync(second, split);
        await AddMovesAsync((split, large, 3), (split, small, 1));

        await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        WaveMpDbContext db = CreateDbContext();
        (await db.Albums.Select(album => new { album.Id, album.CoverPriority, album.FirstSeenAt }).ToListAsync())
            .Should().BeEquivalentTo([
                new
                {
                    large.Id,
                    CoverPriority = (CoverPriority?)CoverPriority.TrackFirst,
                    FirstSeenAt = _day.AddDays(1),
                },
                new
                {
                    small.Id,
                    CoverPriority = (CoverPriority?)CoverPriority.AlbumFirst,
                    FirstSeenAt = _day.AddDays(1),
                },
            ]);

        // An existing favorite keeps the earlier of both times.
        (await FavoritesAsync(first, second)).Should().BeEquivalentTo([
            (first.Id, large.Id, _day.AddDays(4)),
            (first.Id, small.Id, _day.AddDays(4)),
            (second.Id, large.Id, _day.AddDays(2)),
            (second.Id, small.Id, _day.AddDays(5)),
        ]);
        (await OnlineCoversAsync()).Should().Equal((onlineCover, large.Id));
        (await SourceOfPlaybackAsync(second)).Should().Be(large.Id);
    }

    [Test]
    public async Task DeleteOrphansAsync_SuccessorWithItsOwnValues_KeepsThemAndOnlyGetsTheFavorites()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        Album old = seed.Album("Old", firstSeenAt: _day.AddDays(2));
        Album existing = AlbumWithTrack(seed, "Existing", _day.AddDays(1));
        seed.Favorite(user, old, _day.AddDays(3));
        await SaveAsync(seed);
        await SetCoverPriorityAsync(old, CoverPriority.TrackFirst);
        await SetCoverPriorityAsync(existing, CoverPriority.AlbumFirst);
        await AddOnlineCoverAsync(old);
        Guid ownCover = await AddOnlineCoverAsync(existing);
        await AddMovesAsync((old, existing, 1));

        await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        WaveMpDbContext db = CreateDbContext();
        Album album = await db.Albums.SingleAsync();
        (album.CoverPriority, album.FirstSeenAt, album.UpdatedAt)
            .Should().Be(((CoverPriority?)CoverPriority.AlbumFirst, _day.AddDays(1), _day.AddDays(1)));
        (await FavoritesAsync(user)).Should().Equal((user.Id, existing.Id, _day.AddDays(3)));
        (await OnlineCoversAsync()).Should().Equal((ownCover, existing.Id));
    }

    [Test]
    public async Task DeleteOrphansAsync_PartialMove_CarriesNothingWhileTheOldAlbumHasSongs()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        Album old = AlbumWithTrack(seed, "Old", _day);
        Album renamed = AlbumWithTrack(seed, "Renamed", _day.AddDays(1));
        seed.Favorite(user, old, _day);
        await SaveAsync(seed);
        await AddMovesAsync((old, renamed, 1));

        (int Albums, int Artists) deleted = await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        deleted.Should().Be((0, 0));
        WaveMpDbContext db = CreateDbContext();
        (await db.Albums.SingleAsync(album => album.Id == renamed.Id)).FirstSeenAt.Should().Be(_day.AddDays(1));
        (await FavoritesAsync(user)).Should().Equal((user.Id, old.Id, _day));
        (await MovesAsync()).Should().Equal((old.Id, renamed.Id, 1, _day));
    }

    [Test]
    public async Task DeleteOrphansAsync_OldAlbumLeftEmptyLater_CarriesItsDataToEveryAlbumThatGotItsSongs()
    {
        // Song by song: one song of "Old" went to "Middle" (which then lost it to "Last"), another one to "Other";
        // "Old" keeps a song until the end.
        LibrarySeed seed = new();
        User user = seed.User();
        Album old = seed.Album("Old", firstSeenAt: _day);
        Track lastSong = seed.Track("Last song", album: old);
        Album middle = seed.Album("Middle", firstSeenAt: _day.AddDays(1));
        Album last = AlbumWithTrack(seed, "Last", _day.AddDays(2));
        Album other = AlbumWithTrack(seed, "Other", _day.AddDays(3));
        seed.Favorite(user, old, _day.AddDays(4));
        await SaveAsync(seed);
        await SetCoverPriorityAsync(old, CoverPriority.TrackFirst);
        await AddMovesAsync((old, middle, 1), (middle, last, 1), (old, other, 1));

        await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        (await FavoritesAsync(user)).Should().Equal((user.Id, old.Id, _day.AddDays(4)));
        (await MovesAsync()).Should().BeEquivalentTo([
            (old.Id, last.Id, 1, _day),
            (old.Id, other.Id, 1, _day),
        ]);

        await CreateDbContext().Tracks.Where(track => track.Id == lastSong.Id).ExecuteDeleteAsync();

        await CreateRepository().DeleteOrphansAsync(_day.AddDays(31));

        WaveMpDbContext after = CreateDbContext();
        (await after.Albums.Select(album => new { album.Id, album.CoverPriority, album.FirstSeenAt }).ToListAsync())
            .Should().BeEquivalentTo([
                new { last.Id, CoverPriority = (CoverPriority?)CoverPriority.TrackFirst, FirstSeenAt = _day },
                new { other.Id, CoverPriority = (CoverPriority?)CoverPriority.TrackFirst, FirstSeenAt = _day },
            ]);
        (await FavoritesAsync(user)).Should().BeEquivalentTo([
            (user.Id, last.Id, _day.AddDays(4)),
            (user.Id, other.Id, _day.AddDays(4)),
        ]);
        (await MovesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteOrphansAsync_SongMovedBack_DropsTheMoveToTheAlbumItself()
    {
        LibrarySeed seed = new();
        Album album = AlbumWithTrack(seed, "Album");
        Album renamed = seed.Album("Renamed");
        await SaveAsync(seed);
        await AddMovesAsync((album, renamed, 1), (renamed, album, 1));

        await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        WaveMpDbContext db = CreateDbContext();
        (await db.Albums.Select(item => item.Id).ToListAsync()).Should().Equal(album.Id);
        (await MovesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteOrphansAsync_AlbumLeftEmptyWithoutMoves_IsDeletedWithItsData()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        Album gone = seed.Album("Gone", seed.Artist("Gone"));
        AlbumWithTrack(seed, "Stays");
        seed.Favorite(user, gone, _day);
        await SaveAsync(seed);
        await AddOnlineCoverAsync(gone);
        await AddPlaybackStateAsync(user, gone);

        (int Albums, int Artists) deleted = await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        deleted.Should().Be((1, 1));
        (await FavoritesAsync(user)).Should().BeEmpty();
        (await OnlineCoversAsync()).Should().BeEmpty();
        (await SourceOfPlaybackAsync(user)).Should().Be(gone.Id);
    }

    [Test]
    public async Task DeleteOrphansAsync_WithoutAlbumsLeftEmpty_DeletesOnlyArtistsWithoutTracks()
    {
        LibrarySeed seed = new();
        seed.Artist("Without tracks");
        Album album = AlbumWithTrack(seed, "Album");
        await SaveAsync(seed);

        (int Albums, int Artists) deleted = await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        deleted.Should().Be((0, 1));
        WaveMpDbContext db = CreateDbContext();
        (await db.Albums.Select(item => item.Id).ToListAsync()).Should().Equal(album.Id);
    }

    [Test]
    public async Task GetAlbumArtistIdsAsync_Albums_ReturnsTheirAlbumArtistsAndLeavesUnknownIdsOut()
    {
        LibrarySeed seed = new();
        Artist band = seed.Artist("Band");
        Album withArtist = seed.Album("With", band);
        Album without = seed.Album("Without");
        await SaveAsync(seed);

        IReadOnlyDictionary<Guid, Guid?> artists = await CreateRepository()
            .GetAlbumArtistIdsAsync([withArtist.Id, without.Id, Guid.NewGuid()]);

        artists.Should().BeEquivalentTo(new Dictionary<Guid, Guid?>
        {
            [withArtist.Id] = band.Id,
            [without.Id] = null,
        });
    }

    [Test]
    public async Task RecordArtistMovesAsync_NewAndStoredPairs_AreAddedAndCountedUpWithTheNextSave()
    {
        LibrarySeed seed = new();
        Artist from = ArtistWithTrack(seed, "From");
        Artist stored = ArtistWithTrack(seed, "Stored");
        Artist added = ArtistWithTrack(seed, "Added");
        await SaveAsync(seed);
        await AddArtistMovesAsync((from, stored, 2));
        DateTimeOffset now = _day.AddDays(30);
        LibraryScanRepository repository = CreateRepository();

        await repository.RecordArtistMovesAsync(
            new Dictionary<(Guid FromArtistId, Guid ToArtistId), int>
            {
                [(from.Id, stored.Id)] = 1,
                [(from.Id, added.Id)] = 2,
                [(from.Id, from.Id)] = 3,
                [(stored.Id, added.Id)] = 0,
            },
            now);
        await repository.RecordArtistMovesAsync(
            new Dictionary<(Guid FromArtistId, Guid ToArtistId), int> { [(from.Id, added.Id)] = 1 },
            now);

        (await ArtistMovesAsync()).Should().BeEquivalentTo([(from.Id, stored.Id, 2, _day)]);
        await repository.SaveChangesAsync();
        (await ArtistMovesAsync()).Should().BeEquivalentTo([
            (from.Id, stored.Id, 3, now),
            (from.Id, added.Id, 3, now),
        ]);
    }

    [Test]
    public async Task RecordArtistMovesAsync_ToAnArtistAddedInTheSameSave_IsStoredWithIt()
    {
        LibrarySeed seed = new();
        Artist from = ArtistWithTrack(seed, "From");
        await SaveAsync(seed);
        LibraryScanRepository repository = CreateRepository();
        Artist created = new LibrarySeed().Artist("Created");

        repository.Add(created);
        await repository.RecordArtistMovesAsync(
            new Dictionary<(Guid FromArtistId, Guid ToArtistId), int> { [(from.Id, created.Id)] = 1 },
            _day);
        await repository.SaveChangesAsync();

        (await ArtistMovesAsync()).Should().BeEquivalentTo([(from.Id, created.Id, 1, _day)]);
    }

    [Test]
    public async Task DeleteOrphansAsync_ArtistsLeftEmpty_CarryTheFavoritesOfAllProfilesTheDateAndThePlayerOver()
    {
        // Two spellings of a band merge into one: "The Band" had most of the songs.
        LibrarySeed seed = new();
        User first = seed.User();
        User second = seed.User();
        Artist larger = seed.Artist("The Band", _day.AddDays(2));
        Artist smaller = seed.Artist("Band", _day.AddDays(1));
        Artist merged = ArtistWithTrack(seed, "Band, The", _day.AddDays(5));
        seed.Favorite(first, larger, _day.AddDays(3));
        seed.Favorite(first, smaller, _day.AddDays(4));
        seed.Favorite(second, smaller, _day.AddDays(6));
        seed.Favorite(second, merged, _day.AddDays(7));
        await SaveAsync(seed);
        await AddPlaybackStateAsync(first, PlaybackSourceKind.Artist, smaller.Id);
        await AddArtistMovesAsync((larger, merged, 2), (smaller, merged, 1));

        (int Albums, int Artists) deleted = await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        deleted.Should().Be((0, 2));
        WaveMpDbContext db = CreateDbContext();
        Artist artist = await db.Artists.SingleAsync();
        (artist.Id, artist.FirstSeenAt).Should().Be((merged.Id, _day.AddDays(1)));

        // The union of the favorites; a profile that marked the new artist already keeps the earlier time.
        (await ArtistFavoritesAsync(first, second)).Should().BeEquivalentTo([
            (first.Id, merged.Id, _day.AddDays(3)),
            (second.Id, merged.Id, _day.AddDays(6)),
        ]);
        (await SourceOfPlaybackAsync(first)).Should().Be(merged.Id);
        (await ArtistMovesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteOrphansAsync_ArtistSplit_GivesFavoritesAndDateToEveryArtistAndThePlayerToTheLargest()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        Artist split = seed.Artist("Duo", _day);
        Artist small = ArtistWithTrack(seed, "Singer", _day.AddDays(2));
        Artist large = ArtistWithTrack(seed, "Pianist", _day.AddDays(3));
        Artist unrelated = ArtistWithTrack(seed, "Other", _day.AddDays(4));
        seed.Favorite(user, split, _day.AddDays(5));
        await SaveAsync(seed);
        await AddPlaybackStateAsync(user, PlaybackSourceKind.Artist, split.Id);
        await AddArtistMovesAsync((split, small, 1), (split, large, 3));

        await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        WaveMpDbContext db = CreateDbContext();
        (await db.Artists.Select(artist => new { artist.Id, artist.FirstSeenAt }).ToListAsync())
            .Should().BeEquivalentTo([
                new { small.Id, FirstSeenAt = _day },
                new { large.Id, FirstSeenAt = _day },
                new { unrelated.Id, FirstSeenAt = _day.AddDays(4) },
            ]);
        (await ArtistFavoritesAsync(user)).Should().BeEquivalentTo([
            (user.Id, small.Id, _day.AddDays(5)),
            (user.Id, large.Id, _day.AddDays(5)),
        ]);
        (await SourceOfPlaybackAsync(user)).Should().Be(large.Id);
    }

    [Test]
    public async Task DeleteOrphansAsync_OldArtistWithASongOrAnAlbumLeft_CarriesNothingAndTheMoveWaits()
    {
        // "Band" still has a song of its own, "Various" is still the album artist of an album with songs.
        LibrarySeed seed = new();
        User user = seed.User();
        Artist band = ArtistWithTrack(seed, "Band", _day);
        Artist various = seed.Artist("Various", _day);
        seed.Track("Hit", album: seed.Album("Sampler", various));
        Artist renamed = ArtistWithTrack(seed, "The Band", _day.AddDays(1));
        seed.Favorite(user, band, _day);
        seed.Favorite(user, various, _day.AddDays(1));
        await SaveAsync(seed);
        await AddPlaybackStateAsync(user, PlaybackSourceKind.Artist, various.Id);
        await AddArtistMovesAsync((band, renamed, 1), (various, renamed, 1));

        (int Albums, int Artists) deleted = await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        deleted.Should().Be((0, 0));
        WaveMpDbContext db = CreateDbContext();
        (await db.Artists.SingleAsync(artist => artist.Id == renamed.Id)).FirstSeenAt.Should().Be(_day.AddDays(1));
        (await ArtistFavoritesAsync(user)).Should().BeEquivalentTo([
            (user.Id, band.Id, _day),
            (user.Id, various.Id, _day.AddDays(1)),
        ]);
        (await SourceOfPlaybackAsync(user)).Should().Be(various.Id);
        (await ArtistMovesAsync()).Should().BeEquivalentTo([
            (band.Id, renamed.Id, 1, _day),
            (various.Id, renamed.Id, 1, _day),
        ]);
    }

    [Test]
    public async Task DeleteOrphansAsync_ArtistWhoseLastAlbumIsDeletedInTheSameCall_CarriesItsDataOver()
    {
        // The songs of the album of "Various" moved to an album of "Various Artists"; the old album is left empty.
        LibrarySeed seed = new();
        User user = seed.User();
        Artist various = seed.Artist("Various", _day);
        Album old = seed.Album("Sampler", various);
        Artist renamed = seed.Artist("Various Artists", _day.AddDays(1));
        Album album = seed.Album("Sampler", renamed);
        seed.Track("Hit", album: album);
        seed.Favorite(user, various, _day.AddDays(2));
        await SaveAsync(seed);
        await AddMovesAsync((old, album, 1));
        await AddArtistMovesAsync((various, renamed, 1));

        (int Albums, int Artists) deleted = await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        deleted.Should().Be((1, 1));
        WaveMpDbContext db = CreateDbContext();
        Artist artist = await db.Artists.SingleAsync();
        (artist.Id, artist.FirstSeenAt).Should().Be((renamed.Id, _day));
        (await ArtistFavoritesAsync(user)).Should().Equal((user.Id, renamed.Id, _day.AddDays(2)));
        (await ArtistMovesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteOrphansAsync_ArtistLeftEmptyLater_PassesTheMovesIntoItOnAndCarriesItsDataOver()
    {
        // "B" got a song of "A" and gave it on to "C"; "A" keeps a song until the end.
        LibrarySeed seed = new();
        User user = seed.User();
        Artist first = seed.Artist("A", _day);
        Track lastSong = seed.Track("Last song", first);
        Artist middle = seed.Artist("B", _day.AddDays(1));
        Artist last = ArtistWithTrack(seed, "C", _day.AddDays(2));
        seed.Favorite(user, first, _day.AddDays(4));
        await SaveAsync(seed);
        await AddArtistMovesAsync((first, middle, 1), (middle, last, 1));

        await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        (await ArtistFavoritesAsync(user)).Should().Equal((user.Id, first.Id, _day.AddDays(4)));
        (await ArtistMovesAsync()).Should().Equal((first.Id, last.Id, 1, _day));

        await CreateDbContext().Tracks.Where(track => track.Id == lastSong.Id).ExecuteDeleteAsync();

        await CreateRepository().DeleteOrphansAsync(_day.AddDays(31));

        WaveMpDbContext after = CreateDbContext();
        (await after.Artists.Select(artist => new { artist.Id, artist.FirstSeenAt }).ToListAsync())
            .Should().BeEquivalentTo([new { last.Id, FirstSeenAt = _day }]);
        (await ArtistFavoritesAsync(user)).Should().Equal((user.Id, last.Id, _day.AddDays(4)));
        (await ArtistMovesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteOrphansAsync_ArtistLeftEmptyWithoutMoves_IsDeletedWithItsData()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        Artist gone = seed.Artist("Gone");
        ArtistWithTrack(seed, "Stays");
        seed.Favorite(user, gone, _day);
        await SaveAsync(seed);
        await AddPlaybackStateAsync(user, PlaybackSourceKind.Artist, gone.Id);

        (int Albums, int Artists) deleted = await CreateRepository().DeleteOrphansAsync(_day.AddDays(30));

        deleted.Should().Be((0, 1));
        (await ArtistFavoritesAsync(user)).Should().BeEmpty();
        (await SourceOfPlaybackAsync(user)).Should().Be(gone.Id);
    }

    /// <summary>
    /// An artist with one track, so it is not left empty.
    /// </summary>
    private static Artist ArtistWithTrack(LibrarySeed seed, string name, DateTimeOffset? firstSeenAt = null)
    {
        Artist artist = seed.Artist(name, firstSeenAt);
        seed.Track(name + " song", artist);
        return artist;
    }

    /// <summary>
    /// An album with one track, so it is not left empty.
    /// </summary>
    private static Album AlbumWithTrack(LibrarySeed seed, string title, DateTimeOffset? firstSeenAt = null)
    {
        Album album = seed.Album(title, firstSeenAt: firstSeenAt);
        seed.Track(title + " song", album: album);
        return album;
    }

    /// <summary>
    /// A song of the album in the folder of <paramref name="relativePath"/> with that file as its primary file.
    /// </summary>
    private static Track SongIn(
        LibrarySeed seed,
        Album album,
        string relativePath,
        long size,
        MediaFileKind kind = MediaFileKind.Audio,
        MediaSource? source = null)
    {
        Track track = seed.Track(relativePath, album: album, source: source);
        track.DirectoryPath = relativePath[..relativePath.LastIndexOf('/')];
        seed.File(relativePath, kind, track, size: size);
        return track;
    }

    private async Task AddMovesAsync(params (Album From, Album To, int TrackCount)[] moves)
    {
        WaveMpDbContext db = CreateDbContext();
        db.AlbumMoves.AddRange(moves.Select(move => new AlbumMove
        {
            FromAlbumId = move.From.Id,
            ToAlbumId = move.To.Id,
            TrackCount = move.TrackCount,
            MovedAt = _day,
        }));
        await db.SaveChangesAsync();
    }

    private async Task SetCoverPriorityAsync(Album album, CoverPriority coverPriority)
    {
        WaveMpDbContext db = CreateDbContext();
        await db.Albums
            .Where(item => item.Id == album.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CoverPriority, coverPriority));
    }

    private async Task<Guid> AddOnlineCoverAsync(Album album)
    {
        var id = Guid.CreateVersion7();
        WaveMpDbContext db = CreateDbContext();
        db.OnlineCovers.Add(new OnlineCover
        {
            Id = id,
            AlbumId = album.Id,
            FileName = id + ".jpg",
            MimeType = "image/jpeg",
            Size = 1000,
            ReleaseId = Guid.NewGuid(),
            CreatedAt = _day,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task AddArtistMovesAsync(params (Artist From, Artist To, int TrackCount)[] moves)
    {
        WaveMpDbContext db = CreateDbContext();
        db.ArtistMoves.AddRange(moves.Select(move => new ArtistMove
        {
            FromArtistId = move.From.Id,
            ToArtistId = move.To.Id,
            TrackCount = move.TrackCount,
            MovedAt = _day,
        }));
        await db.SaveChangesAsync();
    }

    private Task AddPlaybackStateAsync(User user, Album album)
    {
        return AddPlaybackStateAsync(user, PlaybackSourceKind.Album, album.Id);
    }

    private async Task AddPlaybackStateAsync(User user, PlaybackSourceKind sourceKind, Guid sourceId)
    {
        WaveMpDbContext db = CreateDbContext();
        db.PlaybackStates.Add(new PlaybackState
        {
            UserId = user.Id,
            TrackIds = [Guid.NewGuid()],
            CurrentIndex = 0,
            PositionMilliseconds = 0,
            Shuffle = false,
            Repeat = RepeatMode.Off,
            SourceKind = sourceKind,
            SourceId = sourceId,
            DeviceId = "device",
            DeviceName = "Device",
            ChangedAt = _day,
        });
        await db.SaveChangesAsync();
    }

    private async Task<List<(Guid FromAlbumId, Guid ToAlbumId, int TrackCount, DateTimeOffset MovedAt)>> MovesAsync()
    {
        WaveMpDbContext db = CreateDbContext();
        return [.. (await db.AlbumMoves.ToListAsync())
            .Select(move => (move.FromAlbumId, move.ToAlbumId, move.TrackCount, move.MovedAt))];
    }

    private async Task<List<(Guid UserId, Guid AlbumId, DateTimeOffset CreatedAt)>> FavoritesAsync(
        params User[] users)
    {
        Guid[] ids = [.. users.Select(user => user.Id)];
        WaveMpDbContext db = CreateDbContext();
        return [.. (await db.Favorites.Where(favorite => ids.Contains(favorite.UserId)).ToListAsync())
            .Select(favorite => (favorite.UserId, favorite.AlbumId!.Value, favorite.CreatedAt))];
    }

    private async Task<List<(Guid FromArtistId, Guid ToArtistId, int TrackCount, DateTimeOffset MovedAt)>>
        ArtistMovesAsync()
    {
        WaveMpDbContext db = CreateDbContext();
        return [.. (await db.ArtistMoves.ToListAsync())
            .Select(move => (move.FromArtistId, move.ToArtistId, move.TrackCount, move.MovedAt))];
    }

    private async Task<List<(Guid UserId, Guid ArtistId, DateTimeOffset CreatedAt)>> ArtistFavoritesAsync(
        params User[] users)
    {
        Guid[] ids = [.. users.Select(user => user.Id)];
        WaveMpDbContext db = CreateDbContext();
        return [.. (await db.Favorites
                .Where(favorite => ids.Contains(favorite.UserId) && favorite.ArtistId != null)
                .ToListAsync())
            .Select(favorite => (favorite.UserId, favorite.ArtistId!.Value, favorite.CreatedAt))];
    }

    private async Task<List<(Guid Id, Guid AlbumId)>> OnlineCoversAsync()
    {
        WaveMpDbContext db = CreateDbContext();
        return [.. (await db.OnlineCovers.ToListAsync()).Select(cover => (cover.Id, cover.AlbumId!.Value))];
    }

    private async Task<Guid?> SourceOfPlaybackAsync(User user)
    {
        WaveMpDbContext db = CreateDbContext();
        return await db.PlaybackStates
            .Where(state => state.UserId == user.Id)
            .Select(state => state.SourceId)
            .SingleAsync();
    }

    private async Task SeedFilesAsync()
    {
        LibrarySeed seed = new();
        seed.File("Loose.mp3", MediaFileKind.Audio);
        seed.File("Band/Album/01 - One.mp3", MediaFileKind.Audio);
        seed.File("Band/Album/cover.jpg", MediaFileKind.Image, isPrimary: false);
        seed.File("Band/Album/CD2/01 - Two.mp3", MediaFileKind.Audio);
        seed.File("Band/Album Two/01 - Three.mp3", MediaFileKind.Audio);
        await SaveAsync(seed);
    }

    private LibraryScanRepository CreateRepository()
    {
        return new LibraryScanRepository(CreateDbContext());
    }
}
