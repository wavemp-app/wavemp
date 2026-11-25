using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class ArtistPictureRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _later = LibrarySeed.DefaultTime.AddDays(3);

    protected override string DatabaseName => "wavemp_tests_library_artist_pictures";

    [Test]
    public async Task FindSourcesAsync_AllSources_NamesTheFileAndBothStoredPictures()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        MediaFile local = seed.File("Band/folder.png", MediaFileKind.Image, isPrimary: false, size: 4321);
        artist.LocalPictureFileId = local.Id;
        ArtistPicture uploaded = seed.ArtistPicture(artist, ArtistPictureKind.Uploaded, _later);
        ArtistPicture online = seed.ArtistPicture(artist, ArtistPictureKind.Online);
        await SaveAsync(seed);

        ArtistPictureSources? sources = await Repository().FindSourcesAsync(artist.Id);

        sources.Should().BeEquivalentTo(new ArtistPictureSources(
            artist.Id,
            new ArtistLocalPictureRow(local.Id, local.SourceId, "Band/folder.png", 4321, local.ModifiedAt),
            new ArtistStoredPictureRow(uploaded.Id, _later),
            new ArtistStoredPictureRow(online.Id, LibrarySeed.DefaultTime)));
    }

    [Test]
    public async Task FindSourcesAsync_NoPictureOrNoArtist_ReturnsEmptySourcesOrNull()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        await SaveAsync(seed);

        (await Repository().FindSourcesAsync(artist.Id))
            .Should().Be(new ArtistPictureSources(artist.Id, null, null, null));
        (await Repository().FindSourcesAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Test]
    public async Task ReplaceAsync_EarlierPictureOfTheKind_ReplacesOnlyThatOne()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        ArtistPicture earlier = seed.ArtistPicture(artist, ArtistPictureKind.Uploaded);
        ArtistPicture online = seed.ArtistPicture(artist, ArtistPictureKind.Online);
        await SaveAsync(seed);
        ArtistPicture next = Uploaded(artist);

        ArtistPictureReplacement result = await Repository().ReplaceAsync(next);

        result.Should().BeEquivalentTo(new ArtistPictureReplacement(true, [earlier.Id]));
        (await StoredAsync()).Should().BeEquivalentTo([
            (next.Id, artist.Id, ArtistPictureKind.Uploaded),
            (online.Id, artist.Id, ArtistPictureKind.Online),
        ]);
    }

    [Test]
    public async Task ReplaceAsync_FirstPicture_StoresIt()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        await SaveAsync(seed);
        ArtistPicture picture = Uploaded(artist);

        ArtistPictureReplacement result = await Repository().ReplaceAsync(picture);

        result.Saved.Should().BeTrue();
        result.ReplacedPictureIds.Should().BeEmpty();
        (await StoredAsync()).Should().Equal((picture.Id, artist.Id, ArtistPictureKind.Uploaded));
    }

    [Test]
    public async Task ReplaceAsync_RemovedArtist_StoresNothing()
    {
        ArtistPicture picture = new()
        {
            Id = Guid.CreateVersion7(),
            ArtistId = Guid.NewGuid(),
            Kind = ArtistPictureKind.Uploaded,
            CreatedAt = _later,
            Width = 300,
            Height = 300,
        };

        ArtistPictureReplacement result = await Repository().ReplaceAsync(picture);

        result.Should().BeEquivalentTo(new ArtistPictureReplacement(false, []));
        (await StoredAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task ReplaceAsync_TwoAtTheSameTime_KeepsOnePictureOfTheKind()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        await SaveAsync(seed);
        ArtistPicture first = Uploaded(artist);
        ArtistPicture second = Uploaded(artist);

        ArtistPictureReplacement[] results = await Task.WhenAll(
            Repository().ReplaceAsync(first),
            Repository().ReplaceAsync(second));

        results.Should().AllSatisfy(result => result.Saved.Should().BeTrue());
        List<(Guid Id, Guid ArtistId, ArtistPictureKind Kind)> stored = await StoredAsync();
        stored.Should().ContainSingle();
        Guid loser = stored[0].Id == first.Id ? second.Id : first.Id;
        results.SelectMany(result => result.ReplacedPictureIds).Should().Contain(loser);
    }

    [Test]
    public async Task RemoveAsync_PictureOfTheKind_RemovesOnlyThatOne()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        ArtistPicture uploaded = seed.ArtistPicture(artist, ArtistPictureKind.Uploaded);
        ArtistPicture online = seed.ArtistPicture(artist, ArtistPictureKind.Online);
        await SaveAsync(seed);

        ArtistPictureRemoval? removal = await Repository().RemoveAsync(artist.Id, ArtistPictureKind.Online);

        removal.Should().Be(new ArtistPictureRemoval(online.Id));
        (await StoredAsync()).Should().Equal((uploaded.Id, artist.Id, ArtistPictureKind.Uploaded));
        (await Repository().RemoveAsync(artist.Id, ArtistPictureKind.Online)).Should().Be(
            new ArtistPictureRemoval(null));
        (await Repository().RemoveAsync(Guid.NewGuid(), ArtistPictureKind.Online)).Should().BeNull();
    }

    [Test]
    public async Task ListExistingIdsAsync_MixedIds_ReturnsTheStoredOnes()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        ArtistPicture uploaded = seed.ArtistPicture(artist, ArtistPictureKind.Uploaded);
        await SaveAsync(seed);

        IReadOnlySet<Guid> existing = await Repository().ListExistingIdsAsync([uploaded.Id, Guid.NewGuid()]);

        existing.Should().BeEquivalentTo([uploaded.Id]);
        (await Repository().ListExistingIdsAsync([])).Should().BeEmpty();
    }

    [Test]
    public async Task DeleteArtist_WithPictures_RemovesThemWithIt()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        seed.ArtistPicture(artist, ArtistPictureKind.Uploaded);
        await SaveAsync(seed);

        await CreateDbContext().Artists.Where(item => item.Id == artist.Id).ExecuteDeleteAsync();

        (await StoredAsync()).Should().BeEmpty();
    }

    private ArtistPictureRepository Repository()
    {
        return new ArtistPictureRepository(CreateDbContext());
    }

    private static ArtistPicture Uploaded(Artist artist)
    {
        return new ArtistPicture
        {
            Id = Guid.CreateVersion7(),
            ArtistId = artist.Id,
            Kind = ArtistPictureKind.Uploaded,
            CreatedAt = _later,
            Width = 300,
            Height = 300,
        };
    }

    private async Task<List<(Guid Id, Guid ArtistId, ArtistPictureKind Kind)>> StoredAsync()
    {
        return [.. (await CreateDbContext().ArtistPictures.AsNoTracking().ToListAsync())
            .Select(picture => (picture.Id, picture.ArtistId, picture.Kind))];
    }
}
