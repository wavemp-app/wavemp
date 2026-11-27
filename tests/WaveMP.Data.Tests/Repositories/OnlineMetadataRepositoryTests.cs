using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class OnlineMetadataRepositoryTests : LibraryDatabaseTests
{
    protected override string DatabaseName => "wavemp_tests_data_library_online_metadata";

    [Test]
    public async Task FindTrackAsync_TrackOfAnAlbum_ReturnsItsFolderAndCoverRules()
    {
        LibrarySeed seed = new();
        Album album = seed.Album("Album");
        album.CoverPriority = CoverPriority.TrackFirst;
        Track track = seed.Track("Song", album: album, duration: TimeSpan.FromSeconds(200));
        await SaveAsync(seed);
        OnlineMetadataRepository repository = new(CreateDbContext());

        OnlineTrackRow? row = await repository.FindTrackAsync(track.Id);

        row.Should().Be(new OnlineTrackRow(
            track.Id,
            MediaSource.DefaultId,
            "Album",
            track.BaseName,
            album.Id,
            TimeSpan.FromSeconds(200),
            null,
            CoverPriority.TrackFirst));
        (await repository.FindTrackAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Test]
    public async Task FindAlbumAsync_Album_ReturnsItsTracksInOrder()
    {
        LibrarySeed seed = new();
        Album album = seed.Album("Album");
        Track second = seed.Track("Second", album: album, number: 2, disc: 1);
        Track first = seed.Track("First", album: album, number: 1, disc: 1);
        await SaveAsync(seed);
        OnlineMetadataRepository repository = new(CreateDbContext());

        OnlineAlbumRow? row = await repository.FindAlbumAsync(album.Id);

        row!.Tracks.Select(track => track.TrackId).Should().Equal(first.Id, second.Id);
        (await repository.FindAlbumAsync(Guid.NewGuid())).Should().BeNull();
        (await repository.FindAlbumIdOfTrackAsync(first.Id)).Should().Be(album.Id);
    }

    [Test]
    public async Task SetOnlineCoverAsync_EarlierCover_ReplacesItAndNamesItsFile()
    {
        LibrarySeed seed = new();
        Album album = seed.Album("Album");
        await SaveAsync(seed);
        OnlineMetadataRepository repository = new(CreateDbContext());

        (bool Saved, string? ReplacedFileName) first = await repository.SetOnlineCoverAsync(Cover(album.Id, null));
        (bool Saved, string? ReplacedFileName) second = await repository.SetOnlineCoverAsync(Cover(album.Id, null));

        first.Should().Be((true, (string?)null));
        second.Saved.Should().BeTrue();
        second.ReplacedFileName.Should().NotBeNull();
        IReadOnlySet<string> names = await repository.ListOnlineCoverFileNamesAsync();
        names.Should().ContainSingle().Which.Should().NotBe(second.ReplacedFileName);
    }

    [Test]
    public async Task SetOnlineCoverAsync_UnknownOwner_SavesNothing()
    {
        OnlineMetadataRepository repository = new(CreateDbContext());

        (bool saved, _) = await repository.SetOnlineCoverAsync(Cover(null, Guid.NewGuid()));

        saved.Should().BeFalse();
        (await repository.ListOnlineCoverFileNamesAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task RemoveOnlineCoverAsync_CoverOfATrack_RemovesItAndNamesItsFile()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Song");
        await SaveAsync(seed);
        OnlineMetadataRepository repository = new(CreateDbContext());
        OnlineCover cover = Cover(null, track.Id);
        await repository.SetOnlineCoverAsync(cover);

        string? removed = await repository.RemoveOnlineCoverAsync(null, track.Id);

        removed.Should().Be(cover.FileName);
        (await repository.RemoveOnlineCoverAsync(null, track.Id)).Should().BeNull();
    }

    [Test]
    public async Task OnlineCoverOfAnAlbum_IsACandidateAndGivesTheAlbumAndItsTracksACover()
    {
        LibrarySeed seed = new();
        Album album = seed.Album("Album");
        Track track = seed.Track("Song", album: album);
        await SaveAsync(seed);
        OnlineCover cover = Cover(album.Id, null);
        await new OnlineMetadataRepository(CreateDbContext()).SetOnlineCoverAsync(cover);
        CoverRepository covers = new(CreateDbContext());

        IReadOnlyList<CoverCandidateRow> candidates = (await covers.GetTrackCandidatesAsync(track.Id))!;

        CoverCandidateRow candidate = candidates.Should().ContainSingle().Subject;
        candidate.Should().BeEquivalentTo(new
        {
            CoverId = cover.Id,
            AlbumId = (Guid?)album.Id,
            Source = CoverSource.Online,
            RelativePath = cover.FileName,
            MimeType = "image/jpeg",
            Size = 1234L,
        });
        (await covers.ListAlbumIdsWithCoversAsync()).Should().Equal(album.Id);
        (await new AlbumRepository(CreateDbContext()).FindAsync(album.Id))!.HasCover.Should().BeTrue();
        (await new TrackRepository(CreateDbContext()).FindAsync(track.Id))!.HasCover.Should().BeTrue();
    }

    [Test]
    public async Task OnlineCover_OwnerRemoved_IsRemovedWithIt()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Song");
        await SaveAsync(seed);
        await new OnlineMetadataRepository(CreateDbContext()).SetOnlineCoverAsync(Cover(null, track.Id));

        await using (WaveMpDbContext db = CreateDbContext())
        {
            await db.Tracks.Where(row => row.Id == track.Id).ExecuteDeleteAsync();
        }

        (await new OnlineMetadataRepository(CreateDbContext()).ListOnlineCoverFileNamesAsync()).Should().BeEmpty();
    }

    private static OnlineCover Cover(Guid? albumId, Guid? trackId)
    {
        var id = Guid.CreateVersion7();
        return new OnlineCover
        {
            Id = id,
            AlbumId = albumId,
            TrackId = trackId,
            FileName = $"{id:N}.jpg",
            MimeType = "image/jpeg",
            Size = 1234,
            ReleaseId = Guid.NewGuid(),
            CreatedAt = LibrarySeed.DefaultTime,
        };
    }
}
