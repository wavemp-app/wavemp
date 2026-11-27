using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Autoplay.Selections;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Autoplay.Tests.Selections;

[TestFixture]
public class ArtistAutoplaySelectorTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _alpha = Guid.NewGuid();
    private IAutoplayRepository _repository = null!;
    private ArtistAutoplaySelector _selector = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IAutoplayRepository>();
        _repository.ListRecentPlaysAsync(default, default, default)
            .ReturnsForAnyArgs(new Dictionary<Guid, DateTimeOffset>());
        _repository.ListCandidatesAsync(default!, default).ReturnsForAnyArgs([]);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_now);
        _selector = new ArtistAutoplaySelector(_repository, clock, new Random(1));
    }

    [Test]
    public async Task SelectAsync_EmptyList_ChoosesNothing()
    {
        IReadOnlyList<Guid> picked = await _selector.SelectAsync(Request([]));

        picked.Should().BeEmpty();
        await _repository.DidNotReceiveWithAnyArgs().ListSeedsAsync(default!, default);
    }

    [Test]
    public async Task SelectAsync_UnknownTracks_ChoosesNothing()
    {
        _repository.ListSeedsAsync(default!, default).ReturnsForAnyArgs([]);

        IReadOnlyList<Guid> picked = await _selector.SelectAsync(Request([Guid.NewGuid()]));

        picked.Should().BeEmpty();
        await _repository.DidNotReceiveWithAnyArgs().ListCandidatesAsync(default!, default);
    }

    [Test]
    public async Task SelectAsync_List_AsksForTheSongsOfItsArtistsWithoutTheListAndTheExcludedSongs()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var waiting = Guid.NewGuid();
        GivenSeeds(new AutoplaySeedRow(first, [_alpha], null, null), new AutoplaySeedRow(second, [_alpha], null, null));
        AutoplayCandidateRow song = new(Guid.NewGuid(), [_alpha], null, null, true, false, 0);
        _repository.ListCandidatesAsync(Arg.Is<AutoplayCandidateQuery>(query => query.ArtistIds != null), default)
            .ReturnsForAnyArgs([song]);

        IReadOnlyList<Guid> picked = await _selector.SelectAsync(
            Request([first, second], excluded: [waiting], includeVideoOnly: false));

        picked.Should().Equal(song.TrackId);
        await _repository.Received(1).ListCandidatesAsync(
            Arg.Is<AutoplayCandidateQuery>(query =>
                query.UserId == _userId
                && !query.IncludeVideoOnly
                && query.ArtistIds!.SequenceEqual(new[] { _alpha })
                && query.ExcludedTrackIds.Count == 3
                && query.ExcludedTrackIds.Contains(first)
                && query.ExcludedTrackIds.Contains(second)
                && query.ExcludedTrackIds.Contains(waiting)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SelectAsync_Always_ReadsThePlaysOfTheLastDay()
    {
        GivenSeeds(new AutoplaySeedRow(Guid.NewGuid(), [_alpha], null, null));

        await _selector.SelectAsync(Request([Guid.NewGuid()]));

        await _repository.Received(1).ListRecentPlaysAsync(_userId, _now.AddHours(-24), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SelectAsync_TooFewSongsOfTheArtists_AsksForSongsOfTheGenresAndSimilarDecades()
    {
        GivenSeeds(
            new AutoplaySeedRow(Guid.NewGuid(), [_alpha], "Rock", 1997),
            new AutoplaySeedRow(Guid.NewGuid(), [_alpha], "rock ", 2001),
            new AutoplaySeedRow(Guid.NewGuid(), [_alpha], "Pop", null));
        AutoplayCandidateRow fallback = new(Guid.NewGuid(), [Guid.NewGuid()], "Pop", 2005, true, false, 0);
        _repository.ListCandidatesAsync(Arg.Is<AutoplayCandidateQuery>(query => query.Genres != null), default)
            .ReturnsForAnyArgs([fallback]);

        IReadOnlyList<Guid> picked = await _selector.SelectAsync(Request([Guid.NewGuid()]));

        picked.Should().Equal(fallback.TrackId);
        await _repository.Received(1).ListCandidatesAsync(
            Arg.Is<AutoplayCandidateQuery>(query =>
                query.ArtistIds == null
                && query.Genres!.Count == 2
                && query.Decades!.Order().SequenceEqual(new[] { 1980, 1990, 2000, 2010 })),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SelectAsync_EnoughSongsOfTheArtists_AsksForNoOtherSongs()
    {
        GivenSeeds(new AutoplaySeedRow(Guid.NewGuid(), [_alpha], "Rock", 2001));
        _repository.ListCandidatesAsync(Arg.Is<AutoplayCandidateQuery>(query => query.ArtistIds != null), default)
            .ReturnsForAnyArgs([
                new AutoplayCandidateRow(Guid.NewGuid(), [_alpha], "Rock", 2001, true, false, 0),
                new AutoplayCandidateRow(Guid.NewGuid(), [_alpha], "Rock", 2001, true, false, 0),
            ]);

        IReadOnlyList<Guid> picked = await _selector.SelectAsync(Request([Guid.NewGuid()], count: 2));

        picked.Should().HaveCount(2);
        await _repository.Received(1).ListCandidatesAsync(
            Arg.Any<AutoplayCandidateQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SelectAsync_ListWithoutGenres_AsksForNoOtherSongs()
    {
        GivenSeeds(new AutoplaySeedRow(Guid.NewGuid(), [_alpha], null, 2001));

        IReadOnlyList<Guid> picked = await _selector.SelectAsync(Request([Guid.NewGuid()]));

        picked.Should().BeEmpty();
        await _repository.DidNotReceive().ListCandidatesAsync(
            Arg.Is<AutoplayCandidateQuery>(query => query.ArtistIds == null),
            Arg.Any<CancellationToken>());
    }

    private void GivenSeeds(params AutoplaySeedRow[] seeds)
    {
        _repository.ListSeedsAsync(default!, default).ReturnsForAnyArgs(seeds);
    }

    private AutoplayRequest Request(
        IReadOnlyList<Guid> list,
        IReadOnlyCollection<Guid>? excluded = null,
        int count = 10,
        bool includeVideoOnly = true)
    {
        return new AutoplayRequest(_userId, list, excluded ?? [], count, includeVideoOnly);
    }
}
