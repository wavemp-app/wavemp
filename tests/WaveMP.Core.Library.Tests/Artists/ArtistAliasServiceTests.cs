using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Library.Artists;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Tests.Artists;

[TestFixture]
public class ArtistAliasServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _artistId = Guid.NewGuid();
    private IArtistRepository _artists = null!;
    private IArtistAliasRepository _aliases = null!;
    private ArtistAliasService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _artists = Substitute.For<IArtistRepository>();
        _artists.ExistsAsync(_artistId, Arg.Any<CancellationToken>()).Returns(true);
        _aliases = Substitute.For<IArtistAliasRepository>();
        _aliases.AddAsync(default, default!, default, default)
            .ReturnsForAnyArgs(call => new ArtistAliasAddResult(
                ArtistAliasAddStatus.Added,
                new ArtistAliasRow(Guid.NewGuid(), _artistId, call.ArgAt<string>(1), ArtistAliasSource.Admin)));
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_now);
        _service = new ArtistAliasService(_artists, _aliases, clock);
    }

    [Test]
    public async Task AddAsync_Name_IsCleanedLikeNamesFromTags()
    {
        ArtistAliasAddResult result = await _service.AddAsync(_artistId, "  Alecia \t Moore ");

        result.Alias!.Name.Should().Be("Alecia Moore");
        await _aliases.Received(1).AddAsync(_artistId, "Alecia Moore", _now, Arg.Any<CancellationToken>());
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t ")]
    public async Task AddAsync_NoContent_IsEmptyWithoutAskingTheRepository(string? name)
    {
        ArtistAliasAddResult result = await _service.AddAsync(_artistId, name);

        result.Status.Should().Be(ArtistAliasAddStatus.Empty);
        await _aliases.DidNotReceiveWithAnyArgs().AddAsync(default, default!, default, default);
    }

    [Test]
    public void Clean_LongName_IsCutToTheLongestName()
    {
        string name = new('a', IArtistAliasService.MaxNameLength + 20);

        ArtistAliasService.Clean(name)!.Length.Should().Be(IArtistAliasService.MaxNameLength);
    }

    [Test]
    public async Task ListAsync_UnknownArtist_IsNull()
    {
        (await _service.ListAsync(Guid.NewGuid(), 0, 10)).Should().BeNull();
        await _aliases.DidNotReceiveWithAnyArgs().ListAsync(default, default, default, default);
    }

    [Test]
    public async Task ListAsync_KnownArtist_ReturnsThePageOfTheRepository()
    {
        Page<ArtistAliasRow> page = new([], 0, 10, 0);
        _aliases.ListAsync(_artistId, 0, 10, Arg.Any<CancellationToken>()).Returns(page);

        (await _service.ListAsync(_artistId, 0, 10)).Should().BeSameAs(page);
    }
}
