using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Library.Albums;
using WaveMP.Core.Library.Artists;
using WaveMP.Core.Library.Groups;
using WaveMP.Core.Library.Tracks;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Tests.Groups;

[TestFixture]
public class ListGroupServiceTests
{
    private static readonly Guid _firstAlbum = Guid.NewGuid();
    private static readonly Guid _secondAlbum = Guid.NewGuid();
    private static readonly Guid _alpha = Guid.NewGuid();
    private static readonly Guid _beta = Guid.NewGuid();

    private ITrackRepository _tracks = null!;
    private IAlbumRepository _albums = null!;
    private IArtistRepository _artists = null!;
    private ListGroupService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _tracks = Substitute.For<ITrackRepository>();
        _albums = Substitute.For<IAlbumRepository>();
        _artists = Substitute.For<IArtistRepository>();
        _albums.ListGroupAlbumsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<GroupAlbumRow>)[.. call.Arg<IReadOnlyCollection<Guid>>().Select(Album)]);
        _artists.ListGroupArtistsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<GroupArtistRow>)[.. call.Arg<IReadOnlyCollection<Guid>>().Select(Artist)]);
        _service = new ListGroupService(_tracks, _albums, _artists);
    }

    [Test]
    public async Task ListTrackGroupsAsync_WithoutSort_ReadsTheKeysWithTheDefaultsOfTheList()
    {
        var userId = Guid.NewGuid();
        GivenTrackKeys();

        await _service.ListTrackGroupsAsync(
            new TrackListRequest(5, 10, Genre: "  Rock ", UserId: userId, MyMusic: true));

        await _tracks.Received(1).ListGroupKeysAsync(
            new TrackListQuery(
                5,
                10,
                TrackSortField.AlbumArtist,
                SortDirection.Asc,
                Genre: "Rock",
                UserId: userId,
                MyMusic: true),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListTrackGroupsAsync_RelevanceWithoutSearch_FallsBackLikeTheList()
    {
        GivenTrackKeys();

        await _service.ListTrackGroupsAsync(new TrackListRequest(0, 10, TrackSortField.Relevance, MyMusic: true));

        await _tracks.Received(1).ListGroupKeysAsync(
            new TrackListQuery(0, 10, TrackSortField.AlbumArtist, SortDirection.Asc),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListTrackGroupsAsync_Always_GroupsByAlbumThenByTheArtistOfTracksWithoutAlbum()
    {
        GivenTrackKeys(
            new GroupKeyRow(_firstAlbum, _alpha, 2001, "A"),
            new GroupKeyRow(_firstAlbum, _beta, 2001, "A"),
            new GroupKeyRow(_secondAlbum, _beta, 2010, "B"),
            new GroupKeyRow(null, _beta, null, "B"),
            new GroupKeyRow(null, _beta, null, "B"),
            new GroupKeyRow(null, _alpha, null, "a"),
            new GroupKeyRow(null, null, null, null),
            new GroupKeyRow(_firstAlbum, _alpha, 2001, "A"));

        Page<ListGroup> page = await _service.ListTrackGroupsAsync(new TrackListRequest(0, 100));

        page.Total.Should().Be(6);
        page.Items.Should().Equal(
            new ListGroup(ListGroupKind.Album, 0, 2, Album(_firstAlbum), Letter: "A"),
            new ListGroup(ListGroupKind.Album, 2, 1, Album(_secondAlbum), Letter: "B"),
            new ListGroup(ListGroupKind.Artist, 3, 2, Artist: Artist(_beta), Letter: "B"),
            new ListGroup(ListGroupKind.Artist, 5, 1, Artist: Artist(_alpha), Letter: "A"),
            new ListGroup(ListGroupKind.Unknown, 6, 1),
            new ListGroup(ListGroupKind.Album, 7, 1, Album(_firstAlbum), Letter: "A"));
    }

    [Test]
    public async Task ListTrackGroupsAsync_SortedByYear_CarriesTheYearOfTheFirstTrack()
    {
        GivenTrackKeys(
            new GroupKeyRow(_firstAlbum, _alpha, 2001, null),
            new GroupKeyRow(_firstAlbum, _alpha, 2002, null),
            new GroupKeyRow(null, _beta, null, null));

        Page<ListGroup> byYear = await _service.ListTrackGroupsAsync(
            new TrackListRequest(0, 100, TrackSortField.Year));
        Page<ListGroup> byTitle = await _service.ListTrackGroupsAsync(
            new TrackListRequest(0, 100, TrackSortField.Title));

        byYear.Items.Select(group => group.Year).Should().Equal(2001, null);
        byTitle.Items.Should().OnlyContain(group => group.Year == null);
    }

    [Test]
    public async Task ListTrackGroupsAsync_WithPage_ResolvesOnlyTheAlbumsAndArtistsOfThePage()
    {
        GivenTrackKeys(
            new GroupKeyRow(_firstAlbum, _alpha, null, null),
            new GroupKeyRow(_secondAlbum, _alpha, null, null),
            new GroupKeyRow(_secondAlbum, _beta, null, null),
            new GroupKeyRow(null, _alpha, null, null),
            new GroupKeyRow(null, _beta, null, null));

        Page<ListGroup> page = await _service.ListTrackGroupsAsync(new TrackListRequest(1, 2));

        page.Should().BeEquivalentTo(new { Offset = 1, Limit = 2, Total = 4 });
        page.Items.Select(group => (group.Position, group.Count)).Should().Equal((1, 2), (3, 1));
        await _albums.Received(1).ListGroupAlbumsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { _secondAlbum })),
            Arg.Any<CancellationToken>());
        await _artists.Received(1).ListGroupArtistsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { _alpha })),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListTrackGroupsAsync_AlbumDeletedMeanwhile_IsUnknown()
    {
        GivenTrackKeys(new GroupKeyRow(_firstAlbum, _alpha, null, null));
        _albums.ListGroupAlbumsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        Page<ListGroup> page = await _service.ListTrackGroupsAsync(new TrackListRequest(0, 10));

        page.Items.Should().Equal(new ListGroup(ListGroupKind.Unknown, 0, 1));
    }

    [Test]
    public async Task ListTrackGroupsAsync_OffsetBehindTheGroups_ReturnsAnEmptyPageWithTheTotal()
    {
        GivenTrackKeys(new GroupKeyRow(_firstAlbum, _alpha, null, null));

        Page<ListGroup> page = await _service.ListTrackGroupsAsync(new TrackListRequest(5, 10));

        page.Should().BeEquivalentTo(new { Items = Array.Empty<ListGroup>(), Offset = 5, Limit = 10, Total = 1 });
    }

    [Test]
    public async Task ListAlbumGroupsAsync_ByArtist_GroupsByAlbumArtistWithTheLetterOfItsName()
    {
        GivenAlbumKeys(
            new GroupKeyRow(_firstAlbum, _alpha, 2001, "A"),
            new GroupKeyRow(_secondAlbum, _alpha, 2010, "A"),
            new GroupKeyRow(Guid.NewGuid(), _beta, null, "B"),
            new GroupKeyRow(Guid.NewGuid(), null, null, null));

        Page<ListGroup> page = await _service.ListAlbumGroupsAsync(new AlbumListRequest(0, 100, Genre: " Pop "));

        page.Total.Should().Be(3);
        page.Items.Should().Equal(
            new ListGroup(ListGroupKind.Artist, 0, 2, Artist: Artist(_alpha), Letter: "A"),
            new ListGroup(ListGroupKind.Artist, 2, 1, Artist: Artist(_beta), Letter: "B"),
            new ListGroup(ListGroupKind.Unknown, 3, 1));
        await _albums.Received(1).ListGroupKeysAsync(
            new AlbumListQuery(0, 100, AlbumSortField.Artist, SortDirection.Asc, Genre: "Pop"),
            Arg.Any<CancellationToken>());
        await _albums.DidNotReceiveWithAnyArgs().ListGroupAlbumsAsync(default!, default);
    }

    [Test]
    public async Task ListAlbumGroupsAsync_ByTitle_GroupsByIndexLetter()
    {
        GivenAlbumKeys(
            new GroupKeyRow(Guid.NewGuid(), null, null, "1"),
            new GroupKeyRow(Guid.NewGuid(), null, null, "("),
            new GroupKeyRow(Guid.NewGuid(), null, null, "a"),
            new GroupKeyRow(Guid.NewGuid(), null, null, "\u00c4"),
            new GroupKeyRow(Guid.NewGuid(), null, null, "B"),
            new GroupKeyRow(Guid.NewGuid(), null, null, string.Empty));

        Page<ListGroup> page = await _service.ListAlbumGroupsAsync(
            new AlbumListRequest(0, 100, AlbumSortField.Title));

        page.Items.Should().Equal(
            new ListGroup(ListGroupKind.Letter, 0, 2, Letter: "#"),
            new ListGroup(ListGroupKind.Letter, 2, 2, Letter: "A"),
            new ListGroup(ListGroupKind.Letter, 4, 1, Letter: "B"),
            new ListGroup(ListGroupKind.Letter, 5, 1, Letter: "#"));
        await _albums.DidNotReceiveWithAnyArgs().ListGroupAlbumsAsync(default!, default);
        await _artists.DidNotReceiveWithAnyArgs().ListGroupArtistsAsync(default!, default);
    }

    [Test]
    public async Task ListAlbumGroupsAsync_ByYear_GroupsByYearAndAlbumsWithoutYearAsUnknown()
    {
        GivenAlbumKeys(
            new GroupKeyRow(Guid.NewGuid(), _alpha, 2010, null),
            new GroupKeyRow(Guid.NewGuid(), _beta, 2010, null),
            new GroupKeyRow(Guid.NewGuid(), _beta, 2001, null),
            new GroupKeyRow(Guid.NewGuid(), _beta, null, null));

        Page<ListGroup> page = await _service.ListAlbumGroupsAsync(
            new AlbumListRequest(0, 100, AlbumSortField.Year, SortDirection.Desc));

        page.Items.Should().Equal(
            new ListGroup(ListGroupKind.Year, 0, 2, Year: 2010),
            new ListGroup(ListGroupKind.Year, 2, 1, Year: 2001),
            new ListGroup(ListGroupKind.Unknown, 3, 1));
    }

    [TestCase(AlbumSortField.FirstSeenAt, null)]
    [TestCase(AlbumSortField.Relevance, "alpha")]
    [TestCase(null, "alpha")]
    public async Task ListAlbumGroupsAsync_SortWithoutGroups_ReturnsAnEmptyPageWithoutReading(
        AlbumSortField? sort,
        string? search)
    {
        Page<ListGroup> page = await _service.ListAlbumGroupsAsync(new AlbumListRequest(10, 20, sort, Search: search));

        page.Should().BeEquivalentTo(new { Items = Array.Empty<ListGroup>(), Offset = 10, Limit = 20, Total = 0 });
        await _albums.DidNotReceiveWithAnyArgs().ListGroupKeysAsync(default!, default);
    }

    [Test]
    public async Task ListArtistGroupsAsync_ByName_GroupsByIndexLetterWithTheFiltersOfTheList()
    {
        var userId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        _artists.ListGroupKeysAsync(Arg.Any<ArtistListQuery>(), Arg.Any<CancellationToken>()).Returns(
        [
            new GroupKeyRow(null, _alpha, null, "A"),
            new GroupKeyRow(null, Guid.NewGuid(), null, "\u00e9"),
            new GroupKeyRow(null, Guid.NewGuid(), null, "e"),
            new GroupKeyRow(null, _beta, null, "b"),
        ]);

        Page<ListGroup> page = await _service.ListArtistGroupsAsync(
            new ArtistListRequest(
                0,
                50,
                Direction: SortDirection.Desc,
                UserId: userId,
                MyMusic: true,
                SourceId: sourceId));

        page.Items.Should().Equal(
            new ListGroup(ListGroupKind.Letter, 0, 1, Letter: "A"),
            new ListGroup(ListGroupKind.Letter, 1, 2, Letter: "E"),
            new ListGroup(ListGroupKind.Letter, 3, 1, Letter: "B"));
        await _artists.Received(1).ListGroupKeysAsync(
            new ArtistListQuery(0, 50, ArtistSortField.Name, SortDirection.Desc, null, userId, true, sourceId),
            Arg.Any<CancellationToken>());
    }

    [TestCase(ArtistSortField.FirstSeenAt)]
    [TestCase(ArtistSortField.Relevance)]
    public async Task ListArtistGroupsAsync_SortWithoutGroups_ReturnsAnEmptyPage(ArtistSortField sort)
    {
        Page<ListGroup> page = await _service.ListArtistGroupsAsync(new ArtistListRequest(0, 20, sort, Search: "a"));

        page.Total.Should().Be(0);
        await _artists.DidNotReceiveWithAnyArgs().ListGroupKeysAsync(default!, default);
    }

    private static GroupAlbumRow Album(Guid id)
    {
        return new GroupAlbumRow(id, $"Album {id}", _alpha, "Alpha Band", false, id == _firstAlbum);
    }

    private static GroupArtistRow Artist(Guid id)
    {
        return new GroupArtistRow(id, id == _alpha ? "Alpha Band" : "Beta Singer");
    }

    private void GivenTrackKeys(params GroupKeyRow[] rows)
    {
        _tracks.ListGroupKeysAsync(Arg.Any<TrackListQuery>(), Arg.Any<CancellationToken>()).Returns(rows);
    }

    private void GivenAlbumKeys(params GroupKeyRow[] rows)
    {
        _albums.ListGroupKeysAsync(Arg.Any<AlbumListQuery>(), Arg.Any<CancellationToken>()).Returns(rows);
    }
}
