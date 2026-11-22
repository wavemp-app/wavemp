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
        await repository.DeleteDirectoriesAsync(other.Id, ["Band"]);

        before.Should().Be(2);
        (await CreateRepository().CountFilesAsync(other.Id)).Should().Be(0);
        (await CreateRepository().CountFilesAsync(MediaSource.DefaultId)).Should().Be(1);
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

    /// <summary>
    /// An album with one track, so it is not left empty.
    /// </summary>
    private static Album AlbumWithTrack(LibrarySeed seed, string title, DateTimeOffset? firstSeenAt = null)
    {
        Album album = seed.Album(title, firstSeenAt: firstSeenAt);
        seed.Track(title + " song", album: album);
        return album;
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

    private async Task AddPlaybackStateAsync(User user, Album album)
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
            SourceKind = PlaybackSourceKind.Album,
            SourceId = album.Id,
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
