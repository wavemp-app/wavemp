using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Playlists.Covers;
using WaveMP.Core.Playlists.Options;
using WaveMP.Core.Playlists.Playlists;
using WaveMP.Core.Playlists.SmartPlaylists;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Playlists.Tests.SmartPlaylists;

[TestFixture]
public class SmartPlaylistServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.NewGuid();

    private ISmartPlaylistRepository _smartPlaylists = null!;
    private ITrackRepository _tracks = null!;
    private IPlaylistService _playlists = null!;
    private PlaylistOptions _options = null!;
    private SmartPlaylistService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _smartPlaylists = Substitute.For<ISmartPlaylistRepository>();
        _tracks = Substitute.For<ITrackRepository>();
        _playlists = Substitute.For<IPlaylistService>();
        _options = new PlaylistOptions();
        _smartPlaylists.SummarizeAsync(
                Arg.Any<Guid>(),
                Arg.Any<SmartPlaylistCriteria>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(new SmartPlaylistSummary(0, 0));
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_now);
        _service = new SmartPlaylistService(
            _smartPlaylists,
            _tracks,
            _playlists,
            timeProvider,
            Microsoft.Extensions.Options.Options.Create(_options));
    }

    [Test]
    public async Task CreateAsync_ValidDefinition_StoresTheNormalizedSmartPlaylistOfTheUser()
    {
        SmartPlaylist? created = null;
        await _smartPlaylists.CreateAsync(
            Arg.Do<SmartPlaylist>(playlist => created = playlist),
            Arg.Any<CancellationToken>());
        _smartPlaylists.FindAsync(_userId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => created);

        SmartPlaylistResult result = await _service.CreateAsync(_userId, new SmartPlaylistDefinition(
            "  Good   Rock ",
            SmartPlaylistMatch.Any,
            [new SmartPlaylistRule(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, " Rock ", Number: 3)],
            SmartPlaylistSortField.Year,
            SortDirection.Desc,
            25));

        result.IsOk.Should().BeTrue();
        created!.UserId.Should().Be(_userId);
        created.Name.Should().Be("Good Rock");
        created.NormalizedName.Should().Be("GOOD ROCK");
        created.Match.Should().Be(SmartPlaylistMatch.Any);
        created.Rules.Should().Equal(new SmartPlaylistRule(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "Rock"));
        created.Sort.Should().Be(SmartPlaylistSortField.Year);
        created.Direction.Should().Be(SortDirection.Desc);
        created.Limit.Should().Be(25);
        created.CreatedAt.Should().Be(_now);
        result.Item!.Playlist.Should().BeSameAs(created);
    }

    [Test]
    public async Task CreateAsync_InvalidDefinition_StoresNothingAndReturnsTheErrors()
    {
        SmartPlaylistResult result = await _service.CreateAsync(_userId, new SmartPlaylistDefinition(
            " ",
            SmartPlaylistMatch.All,
            [],
            SmartPlaylistSortField.Title,
            SortDirection.Asc));

        result.Status.Should().Be(SmartPlaylistStatus.Invalid);
        result.Errors.Should().Equal(new SmartPlaylistError("name", SmartPlaylistErrorCode.Required));
        await _smartPlaylists.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Test]
    public async Task UpdateAsync_UnknownSmartPlaylist_ReturnsNotFound()
    {
        _smartPlaylists.UpdateAsync(Arg.Any<SmartPlaylist>(), Arg.Any<CancellationToken>()).Returns(false);

        SmartPlaylistResult result = await _service.UpdateAsync(_userId, Guid.NewGuid(), new SmartPlaylistDefinition(
            "Name",
            SmartPlaylistMatch.All,
            [],
            SmartPlaylistSortField.Title,
            SortDirection.Asc));

        result.Status.Should().Be(SmartPlaylistStatus.NotFound);
    }

    [Test]
    public async Task GetAsync_SmartPlaylistWithTracks_HasTheCoversOfItsFirstTracks()
    {
        SmartPlaylist playlist = Playlist("Covers");
        _smartPlaylists.FindAsync(_userId, playlist.Id, Arg.Any<CancellationToken>()).Returns(playlist);
        _smartPlaylists.SummarizeAsync(_userId, Arg.Any<SmartPlaylistCriteria>(), _now, Arg.Any<CancellationToken>())
            .Returns(new SmartPlaylistSummary(3, 540_000));
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _smartPlaylists.ListCoverCandidatesAsync(
                playlist.Id,
                _userId,
                Arg.Any<SmartPlaylistCriteria>(),
                _now,
                _options.CoverScanEntries,
                Arg.Any<CancellationToken>())
            .Returns(
            [
                new PlaylistCoverCandidateRow(playlist.Id, 0, first, Guid.NewGuid(), true),
                new PlaylistCoverCandidateRow(playlist.Id, 1, Guid.NewGuid(), null, false),
                new PlaylistCoverCandidateRow(playlist.Id, 2, second, Guid.NewGuid(), true),
            ]);

        SmartPlaylistItem? item = await _service.GetAsync(_userId, playlist.Id);

        item!.Summary.Should().Be(new SmartPlaylistSummary(3, 540_000));
        item.Cover.Kind.Should().Be(PlaylistCoverKind.Tracks);
        item.Cover.TrackIds.Should().Equal(first, second);
    }

    [Test]
    public async Task GetAsync_EmptySmartPlaylist_HasNoCover()
    {
        SmartPlaylist playlist = Playlist("Empty");
        _smartPlaylists.FindAsync(_userId, playlist.Id, Arg.Any<CancellationToken>()).Returns(playlist);

        SmartPlaylistItem? item = await _service.GetAsync(_userId, playlist.Id);

        item!.Cover.Should().Be(PlaylistCover.None);
        await _smartPlaylists.DidNotReceiveWithAnyArgs()
            .ListCoverCandidatesAsync(default, default, default!, default, default, default);
    }

    [Test]
    public async Task ListTracksAsync_SmartPlaylist_ReturnsTheTracksInItsOrderWithoutVanishedTracks()
    {
        SmartPlaylist playlist = Playlist("Order");
        _smartPlaylists.FindAsync(_userId, playlist.Id, Arg.Any<CancellationToken>()).Returns(playlist);
        TrackRow first = Track("First");
        TrackRow second = Track("Second");
        var vanished = Guid.NewGuid();
        _smartPlaylists.ListTrackIdsAsync(
            _userId,
            Arg.Any<SmartPlaylistCriteria>(),
            _now,
            10,
            3,
            Arg.Any<CancellationToken>())
            .Returns(new Page<Guid>([second.Id, vanished, first.Id], 10, 3, 13));
        _tracks.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([first, second]);

        Page<TrackRow>? page = await _service.ListTracksAsync(_userId, playlist.Id, 10, 3);

        page!.Items.Should().Equal(second, first);
        page.Offset.Should().Be(10);
        page.Total.Should().Be(13);
    }

    [Test]
    public async Task ListTracksAsync_UnknownSmartPlaylist_ReturnsNull()
    {
        Page<TrackRow>? page = await _service.ListTracksAsync(_userId, Guid.NewGuid(), 0, 10);

        page.Should().BeNull();
    }

    [Test]
    public async Task ConvertAsync_SmartPlaylist_CreatesAPlaylistWithItsTracksAndDeletesIt()
    {
        SmartPlaylist playlist = Playlist("Keep");
        _smartPlaylists.FindAsync(_userId, playlist.Id, Arg.Any<CancellationToken>()).Returns(playlist);
        Guid[] trackIds = [Guid.NewGuid(), Guid.NewGuid()];
        _smartPlaylists.ListTrackIdsAsync(
                _userId,
                Arg.Any<SmartPlaylistCriteria>(),
                _now,
                0,
                _options.MaxEntries + 1,
                Arg.Any<CancellationToken>())
            .Returns(new Page<Guid>(trackIds, 0, _options.MaxEntries + 1, 2));
        PlaylistItem created = new(new PlaylistRow(Guid.NewGuid(), "Keep", 2, 0, null, _now, _now), PlaylistCover.None);
        _playlists.CreateAsync(_userId, "Keep", Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new PlaylistResult(PlaylistStatus.Ok, created));

        SmartPlaylistConversion conversion = await _service.ConvertAsync(_userId, playlist.Id);

        conversion.IsOk.Should().BeTrue();
        conversion.Playlist.Should().Be(created);
        await _playlists.Received(1).CreateAsync(
            _userId,
            "Keep",
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(trackIds)),
            Arg.Any<CancellationToken>());
        await _smartPlaylists.Received(1).DeleteAsync(_userId, playlist.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ConvertAsync_MoreTracksThanAPlaylistMayHave_ReturnsFullAndKeepsTheSmartPlaylist()
    {
        _options.MaxEntries = 2;
        SmartPlaylist playlist = Playlist("Huge");
        _smartPlaylists.FindAsync(_userId, playlist.Id, Arg.Any<CancellationToken>()).Returns(playlist);
        _smartPlaylists.ListTrackIdsAsync(
            _userId,
            Arg.Any<SmartPlaylistCriteria>(),
            _now,
            0,
            3,
            Arg.Any<CancellationToken>())
            .Returns(new Page<Guid>([Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], 0, 3, 7));

        SmartPlaylistConversion conversion = await _service.ConvertAsync(_userId, playlist.Id);

        conversion.Should().Be(SmartPlaylistConversion.Of(SmartPlaylistStatus.Full, 2));
        await _playlists.DidNotReceiveWithAnyArgs().CreateAsync(default, default, default!, default);
        await _smartPlaylists.DidNotReceiveWithAnyArgs().DeleteAsync(default, default, default);
    }

    [Test]
    public async Task ConvertAsync_TracksKeepVanishing_GivesUpAfterThreeAttemptsAndKeepsTheSmartPlaylist()
    {
        SmartPlaylist playlist = Playlist("Moving");
        _smartPlaylists.FindAsync(_userId, playlist.Id, Arg.Any<CancellationToken>()).Returns(playlist);
        _smartPlaylists.ListTrackIdsAsync(
                _userId,
                Arg.Any<SmartPlaylistCriteria>(),
                _now,
                Arg.Is(0),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new Page<Guid>([Guid.NewGuid()], 0, 1, 1));
        _playlists.CreateAsync(_userId, "Moving", Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(PlaylistResult.Of(PlaylistStatus.TrackNotFound));

        SmartPlaylistConversion conversion = await _service.ConvertAsync(_userId, playlist.Id);

        conversion.Status.Should().Be(SmartPlaylistStatus.LibraryChanged);
        await _playlists.ReceivedWithAnyArgs(3).CreateAsync(default, default, default!, default);
        await _smartPlaylists.DidNotReceiveWithAnyArgs().DeleteAsync(default, default, default);
    }

    [Test]
    public async Task ConvertAsync_UnknownSmartPlaylist_ReturnsNotFound()
    {
        SmartPlaylistConversion conversion = await _service.ConvertAsync(_userId, Guid.NewGuid());

        conversion.Status.Should().Be(SmartPlaylistStatus.NotFound);
    }

    private SmartPlaylist Playlist(string name)
    {
        return new SmartPlaylist
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Match = SmartPlaylistMatch.All,
            Rules = [new SmartPlaylistRule(SmartPlaylistField.Favorite, SmartPlaylistOperator.IsTrue)],
            Sort = SmartPlaylistSortField.Artist,
            Direction = SortDirection.Asc,
            CreatedAt = _now,
            UpdatedAt = _now,
        };
    }

    private static TrackRow Track(string title)
    {
        return new TrackRow(
            Guid.NewGuid(),
            title,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            TimeSpan.FromMinutes(3),
            true,
            false,
            false,
            _now);
    }
}
