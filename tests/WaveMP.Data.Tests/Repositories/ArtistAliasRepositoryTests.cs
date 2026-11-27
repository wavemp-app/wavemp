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
public class ArtistAliasRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    protected override string DatabaseName => "wavemp_tests_data_library_artist_aliases";

    [Test]
    public async Task ListAsync_Aliases_AreSortedByTheirNormalizedNamesWithoutRemovedOnes()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("P!nk");
        seed.Alias(artist, "Pink", ArtistAliasSource.MusicBrainz);
        seed.Alias(artist, "Alecia Moore");
        seed.Alias(artist, "Hidden", ArtistAliasSource.MusicBrainz, isRemoved: true);
        Artist other = seed.Artist("Other");
        seed.Alias(other, "Another");
        await SaveAsync(seed);

        Page<ArtistAliasRow> page = await Repository().ListAsync(artist.Id, 0, 10);

        page.Items.Select(alias => (alias.Name, alias.Source)).Should().Equal(
            ("Alecia Moore", ArtistAliasSource.Admin),
            ("Pink", ArtistAliasSource.MusicBrainz));
        page.Total.Should().Be(2);
    }

    [Test]
    public async Task AddAsync_NewName_AddsAnAliasOfTheAdmin()
    {
        Artist artist = await SeedArtistAsync("P!nk");

        ArtistAliasAddResult result = await Repository().AddAsync(artist.Id, "Alecia Moore", _now);

        result.Status.Should().Be(ArtistAliasAddStatus.Added);
        result.Alias!.Name.Should().Be("Alecia Moore");
        (await Repository().FindAsync(artist.Id, result.Alias.Id)).Should().Be(result.Alias);
    }

    [TestCase("p!nk")]
    [TestCase("PINK")]
    public async Task AddAsync_NameOfTheArtistOrAnExistingAlias_IsADuplicate(string name)
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("P!nk");
        seed.Alias(artist, "Pink", ArtistAliasSource.MusicBrainz);
        await SaveAsync(seed);

        ArtistAliasAddResult result = await Repository().AddAsync(artist.Id, name, _now);

        result.Status.Should().Be(ArtistAliasAddStatus.Duplicate);
    }

    [Test]
    public async Task AddAsync_RemovedAlias_ComesBackAsAliasOfTheAdmin()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("P!nk");
        ArtistAlias removed = seed.Alias(artist, "Pink", ArtistAliasSource.MusicBrainz, isRemoved: true);
        await SaveAsync(seed);

        ArtistAliasAddResult result = await Repository().AddAsync(artist.Id, "PINK", _now);

        result.Should().Be(new ArtistAliasAddResult(
            ArtistAliasAddStatus.Added,
            new ArtistAliasRow(removed.Id, artist.Id, "PINK", ArtistAliasSource.Admin)));
    }

    [Test]
    public async Task AddAsync_UnknownArtistOrEmptyName_AddsNothing()
    {
        Artist artist = await SeedArtistAsync("Band");

        (await Repository().AddAsync(Guid.NewGuid(), "Name", _now)).Status
            .Should().Be(ArtistAliasAddStatus.ArtistNotFound);
        (await Repository().AddAsync(artist.Id, "  ", _now)).Status.Should().Be(ArtistAliasAddStatus.Empty);
    }

    [Test]
    public async Task RemoveAsync_AliasOfTheAdmin_IsDeletedAndOneOfMusicBrainzIsRemembered()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("P!nk");
        ArtistAlias own = seed.Alias(artist, "Alecia Moore");
        ArtistAlias online = seed.Alias(artist, "Pink", ArtistAliasSource.MusicBrainz);
        await SaveAsync(seed);

        (await Repository().RemoveAsync(artist.Id, own.Id)).Should().BeTrue();
        (await Repository().RemoveAsync(artist.Id, online.Id)).Should().BeTrue();
        (await Repository().RemoveAsync(artist.Id, online.Id)).Should().BeFalse();
        (await Repository().RemoveAsync(Guid.NewGuid(), own.Id)).Should().BeFalse();

        WaveMpDbContext db = CreateDbContext();
        (await db.ArtistAliases.SingleAsync()).Should().BeEquivalentTo(new { online.Id, IsRemoved = true });
        (await Repository().ListAsync(artist.Id, 0, 10)).Items.Should().BeEmpty();
    }

    [Test]
    public async Task ReplaceMusicBrainzAliasesAsync_NewNames_ReplaceTheOldOnesAndKeepTheAdminsChoices()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("P!nk");
        seed.Alias(artist, "Old Name", ArtistAliasSource.MusicBrainz);
        seed.Alias(artist, "Kept", ArtistAliasSource.MusicBrainz);
        seed.Alias(artist, "Alecia Moore");
        seed.Alias(artist, "Unwanted", ArtistAliasSource.MusicBrainz, isRemoved: true);
        await SaveAsync(seed);

        IReadOnlyList<ArtistAliasRow>? aliases = await Repository().ReplaceMusicBrainzAliasesAsync(
            artist.Id,
            ["P!nk", "Pink", "pink", "Kept", "alecia moore", "Unwanted", "  "],
            _now);

        aliases!.Select(alias => (alias.Name, alias.Source)).Should().Equal(
            ("Alecia Moore", ArtistAliasSource.Admin),
            ("Kept", ArtistAliasSource.MusicBrainz),
            ("Pink", ArtistAliasSource.MusicBrainz));
        WaveMpDbContext db = CreateDbContext();
        (await db.ArtistAliases.CountAsync(alias => alias.IsRemoved)).Should().Be(1);
    }

    [Test]
    public async Task ReplaceMusicBrainzAliasesAsync_UnknownArtist_IsNull()
    {
        (await Repository().ReplaceMusicBrainzAliasesAsync(Guid.NewGuid(), ["Name"], _now)).Should().BeNull();
    }

    [Test]
    public async Task DeleteArtist_WithAliases_RemovesThem()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        seed.Alias(artist, "Group");
        await SaveAsync(seed);

        WaveMpDbContext db = CreateDbContext();
        await db.Artists.Where(row => row.Id == artist.Id).ExecuteDeleteAsync();

        (await db.ArtistAliases.AnyAsync()).Should().BeFalse();
    }

    private ArtistAliasRepository Repository()
    {
        return new ArtistAliasRepository(CreateDbContext());
    }

    private async Task<Artist> SeedArtistAsync(string name)
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist(name);
        await SaveAsync(seed);
        return artist;
    }
}
