using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Matches;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.OnlineMetadata.Searches;
using WaveMP.Core.OnlineMetadata.Tests.Fakes;
using WaveMP.Core.Scanning.Edits;
using WaveMP.Core.Settings.Groups;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.OnlineMetadata.Tests.Searches;

/// <summary>
/// The search with simulated answers of MusicBrainz and the Cover Art Archive.
/// </summary>
[TestFixture]
public class OnlineMetadataSearchServiceTests
{
    private static readonly Guid _trackId = Guid.NewGuid();
    private static readonly Guid _albumId = Guid.NewGuid();

    private FakeSnapshot<OnlineMetadataSettings> _settings = null!;
    private ITrackMetadataEditor _editor = null!;
    private ITrackRepository _tracks = null!;
    private IAlbumRepository _albums = null!;
    private IOnlineMetadataRepository _repository = null!;
    private IMusicBrainzClient _musicBrainz = null!;
    private ICoverArtArchiveClient _coverArtArchive = null!;
    private OnlineMetadataSearchService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _settings = new FakeSnapshot<OnlineMetadataSettings>(new OnlineMetadataSettings { Enabled = true });
        _editor = Substitute.For<ITrackMetadataEditor>();
        _tracks = Substitute.For<ITrackRepository>();
        _albums = Substitute.For<IAlbumRepository>();
        _repository = Substitute.For<IOnlineMetadataRepository>();
        _musicBrainz = Substitute.For<IMusicBrainzClient>();
        _coverArtArchive = Substitute.For<ICoverArtArchiveClient>();
        _service = new OnlineMetadataSearchService(
            _settings,
            _editor,
            _tracks,
            _albums,
            _repository,
            _musicBrainz,
            _coverArtArchive,
            Microsoft.Extensions.Options.Options.Create(new OnlineMetadataOptions()));

        _tracks.FindAsync(_trackId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Track(TimeSpan.FromSeconds(200)));
        _editor.GetAsync(_trackId, Arg.Any<CancellationToken>()).Returns(Metadata(_trackId, "Song"));
    }

    [Test]
    public async Task SearchTrackAsync_Disabled_AsksNoService()
    {
        _settings.Current = new OnlineMetadataSettings { Enabled = false };

        TrackSearchResult result = await _service.SearchTrackAsync(_trackId);

        result.Status.Should().Be(OnlineSearchStatus.Disabled);
        _musicBrainz.ReceivedCalls().Should().BeEmpty();
        _coverArtArchive.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task SearchTrackAsync_UnknownTrack_IsNotFound()
    {
        TrackSearchResult result = await _service.SearchTrackAsync(Guid.NewGuid());

        result.Status.Should().Be(OnlineSearchStatus.NotFound);
        _musicBrainz.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task SearchTrackAsync_ServiceUnavailable_IsUnavailable()
    {
        _musicBrainz.SearchRecordingsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<MusicBrainzRecording>>.Failed(OnlineStatus.Unavailable));

        TrackSearchResult result = await _service.SearchTrackAsync(_trackId);

        result.Status.Should().Be(OnlineSearchStatus.Unavailable);
    }

    [Test]
    public async Task SearchTrackAsync_Recordings_SearchesByTagsAndDurationAndRanksTheBestFirst()
    {
        _musicBrainz.SearchRecordingsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<MusicBrainzRecording>>.Found(
            [
                Recording("Other Song", "Other Band", 90, 320000),
                Recording("Song", "Band", 80, 201000),
            ]));

        TrackSearchResult result = await _service.SearchTrackAsync(_trackId);

        await _musicBrainz.Received(1).SearchRecordingsAsync(
            MusicBrainzQuery.Recording("Song", "Band", "Album", TimeSpan.FromSeconds(200)),
            Arg.Any<CancellationToken>());
        result.Status.Should().Be(OnlineSearchStatus.Searched);
        result.Current!.Title.Should().Be("Song");
        result.Matches.Select(match => match.Values.Title).Should().Equal("Song", "Other Song");
        result.Matches[0].Agreement.Should().Be(100);
        result.Matches.Should().BeInDescendingOrder(match => match.Agreement);
    }

    [Test]
    public void RankTracks_ManyReleases_KeepsTheLimits()
    {
        MusicBrainzRecording recording = new()
        {
            Id = Guid.NewGuid(),
            Title = "Song",
            Releases = [.. Enumerable.Range(0, 5).Select(index => new MusicBrainzRelease
            {
                Id = Guid.NewGuid(),
                Title = "Album",
                Date = $"200{index}",
            })],
        };

        IReadOnlyList<TrackMatch> matches = OnlineMetadataSearchService.RankTracks(
            new TrackLookup("Song", null, "Album", null),
            [recording, recording],
            new OnlineMetadataOptions { MaxReleasesPerRecording = 2, MaxMatches = 3 });

        matches.Should().HaveCount(3);
        matches.Select(match => match.Release!.Date).Should().Equal("2000", "2000", "2001");
    }

    [Test]
    public async Task SearchAlbumAsync_Releases_CountsEditableTracksAndRanks()
    {
        var editable = Guid.NewGuid();
        var readOnly = Guid.NewGuid();
        _albums.FindAsync(_albumId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(new AlbumRow(
            _albumId,
            "Album",
            null,
            "Band",
            false,
            1999,
            "Rock",
            2,
            400000,
            false,
            DateTimeOffset.UnixEpoch));
        _repository.FindAlbumAsync(_albumId, Arg.Any<CancellationToken>()).Returns(new OnlineAlbumRow(
            _albumId,
            [
                new OnlineAlbumTrackRow(editable, Guid.NewGuid(), "Band/Album", null),
                new OnlineAlbumTrackRow(readOnly, Guid.NewGuid(), "Band/Album", null),
            ]));
        _editor.GetAsync(editable, Arg.Any<CancellationToken>()).Returns(Metadata(editable, "A"));
        _editor.GetAsync(readOnly, Arg.Any<CancellationToken>())
            .Returns(Metadata(readOnly, "B") with { Status = TrackMetadataStatus.NotWritable });
        _musicBrainz.SearchReleasesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<MusicBrainzRelease>>.Found(
            [
                new MusicBrainzRelease { Id = Guid.NewGuid(), Title = "Album", Score = 70, TrackCount = 20 },
                new MusicBrainzRelease
                {
                    Id = Guid.NewGuid(),
                    Title = "Album",
                    Score = 60,
                    TrackCount = 2,
                    ArtistCredit = [new MusicBrainzArtistCredit { Name = "Band" }],
                },
            ]));

        AlbumSearchResult result = await _service.SearchAlbumAsync(_albumId);

        await _musicBrainz.Received(1).SearchReleasesAsync(
            MusicBrainzQuery.Release("Album", "Band", 2),
            Arg.Any<CancellationToken>());
        result.Current.Should().Be(new AlbumCurrent(new OnlineAlbumValues("Album", "Band", 1999, "Rock"), 2, 1));
        result.Matches.Select(match => match.Release.TrackCount).Should().Equal(2, 20);
    }

    [Test]
    public async Task SearchAlbumAsync_UnknownAlbum_IsNotFound()
    {
        AlbumSearchResult result = await _service.SearchAlbumAsync(Guid.NewGuid());

        result.Status.Should().Be(OnlineSearchStatus.NotFound);
        _musicBrainz.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task GetCoverAsync_Picture_IsFoundWithItsType()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        _coverArtArchive.GetFrontCoverAsync(Arg.Any<Guid>(), CoverArtSize.Small, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<byte[]>.Found(png));

        OnlineCoverPreview preview = await _service.GetCoverAsync(Guid.NewGuid(), CoverArtSize.Small);

        preview.Status.Should().Be(OnlineStatus.Found);
        preview.ContentType.Should().Be("image/png");
    }

    [Test]
    public async Task GetCoverAsync_NoPicture_IsUnavailable()
    {
        _coverArtArchive.GetFrontCoverAsync(Arg.Any<Guid>(), Arg.Any<CoverArtSize>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<byte[]>.Found("<html></html>"u8.ToArray()));

        OnlineCoverPreview preview = await _service.GetCoverAsync(Guid.NewGuid(), CoverArtSize.Small);

        preview.Status.Should().Be(OnlineStatus.Unavailable);
    }

    [Test]
    public async Task GetCoverAsync_Disabled_AsksNoService()
    {
        _settings.Current = new OnlineMetadataSettings { Enabled = false };

        OnlineCoverPreview preview = await _service.GetCoverAsync(Guid.NewGuid(), CoverArtSize.Small);

        preview.Status.Should().Be(OnlineStatus.Disabled);
        _coverArtArchive.ReceivedCalls().Should().BeEmpty();
    }

    private static TrackRow Track(TimeSpan duration)
    {
        return new TrackRow(
            _trackId,
            "Song",
            null,
            "Band",
            _albumId,
            "Album",
            null,
            "Band",
            false,
            1,
            1,
            1999,
            null,
            duration,
            true,
            false,
            false,
            DateTimeOffset.UnixEpoch);
    }

    private static TrackMetadata Metadata(Guid id, string title)
    {
        return new TrackMetadata(
            id,
            TrackMetadataStatus.Editable,
            "Band/Album/Song.mp3",
            title,
            "Band",
            "Album",
            "Band",
            null,
            null,
            null,
            null);
    }

    private static MusicBrainzRecording Recording(string title, string artist, int score, long length)
    {
        return new MusicBrainzRecording
        {
            Id = Guid.NewGuid(),
            Title = title,
            Score = score,
            Length = length,
            ArtistCredit = [new MusicBrainzArtistCredit { Name = artist }],
            Releases =
            [
                new MusicBrainzRelease
                {
                    Id = Guid.NewGuid(),
                    Title = "Album",
                    Media =
                    [
                        new MusicBrainzMedium
                        {
                            Position = 1,
                            Tracks = [new MusicBrainzTrack { Title = title, Number = "1" }],
                        },
                    ],
                },
            ],
        };
    }
}
