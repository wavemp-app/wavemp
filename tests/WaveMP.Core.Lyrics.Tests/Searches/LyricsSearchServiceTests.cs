using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Searches;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Tests.Searches;

/// <summary>
/// The search of admins with a simulated LRCLIB (substituted client).
/// </summary>
[TestFixture]
public class LyricsSearchServiceTests
{
    private static readonly Guid _trackId = Guid.NewGuid();

    private ILyricsRepository _repository = null!;
    private ILrclibClient _lrclib = null!;
    private ISettingsSnapshot<LibrarySettings> _settings = null!;
    private LyricsSearchService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ILyricsRepository>();
        _repository.FindTrackAsync(_trackId, Arg.Any<CancellationToken>()).Returns(new LyricsTrackRow(
            _trackId,
            MediaSource.DefaultId,
            "Kerli",
            "Diamond Hard",
            "Diamond Hard (Official Video)",
            "Kerli",
            null,
            TimeSpan.FromSeconds(343),
            null,
            "Kerli/Diamond Hard.mp3",
            null,
            null));
        _lrclib = Substitute.For<ILrclibClient>();
        _lrclib.SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        _lrclib.SearchTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        _settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        _settings.Current.Returns(new LibrarySettings { DownloadLyrics = true });
        _service = new LyricsSearchService(_repository, _lrclib, _settings);
    }

    [Test]
    public async Task SearchAsync_WithoutNames_SearchesTheCleanTitleAndTheMainArtistAndSortsTheMatches()
    {
        LrclibRecord otherSong = Record(1, "Other Song", 343, synced: true);
        LrclibRecord unsynced = Record(2, "Diamond Hard", 343, synced: false);
        LrclibRecord single = Record(3, "Diamond Hard", 242, synced: true);
        LrclibRecord fitting = Record(4, "Diamond Hard", 344, synced: true);
        LrclibRecord empty = new() { Id = 5, TrackName = "Diamond Hard", ArtistName = "Kerli" };
        _lrclib.SearchAsync(new LrclibQuery("Diamond Hard", "Kerli", null, null), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([otherSong, unsynced, single, fitting, empty]));

        LyricsSearchResult result = await _service.SearchAsync(_trackId, null, null);

        result.Status.Should().Be(LyricsSearchStatus.Searched);
        result.Title.Should().Be("Diamond Hard");
        result.Artist.Should().Be("Kerli");
        result.Matches!.Select(match => match.Id).Should().Equal(4, 2, 3, 1);
        result.Matches![0].Should().Be(new LyricsMatch(
            4,
            "Diamond Hard",
            "Kerli",
            "Album",
            TimeSpan.FromSeconds(344),
            Synchronized: true,
            FitsDuration: true));
        result.Matches![2].FitsDuration.Should().BeFalse();
        await _lrclib.DidNotReceiveWithAnyArgs().SearchTextAsync(default!, default);
    }

    [Test]
    public async Task SearchAsync_NothingFoundByFields_UsesTheFreeTextSearchWithTheGivenNames()
    {
        _lrclib.SearchTextAsync("Other Artist Other Title", Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.Found([Record(7, "Other Title", 343, synced: true)]));

        LyricsSearchResult result = await _service.SearchAsync(_trackId, " Other Title ", "Other Artist");

        await _lrclib.Received(1).SearchAsync(
            new LrclibQuery("Other Title", "Other Artist", null, null),
            Arg.Any<CancellationToken>());
        result.Matches!.Should().ContainSingle().Which.Id.Should().Be(7);
    }

    [Test]
    public async Task SearchAsync_EmptyArtist_SearchesByTitleOnly()
    {
        LyricsSearchResult result = await _service.SearchAsync(_trackId, "Diamond Hard", string.Empty);

        result.Artist.Should().BeNull();
        await _lrclib.Received(1).SearchAsync(
            new LrclibQuery("Diamond Hard", null, null, null),
            Arg.Any<CancellationToken>());
        await _lrclib.Received(1).SearchTextAsync("Diamond Hard", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SearchAsync_SwitchedOffUnknownTrackOrServiceBusy_SaysWhy()
    {
        (await _service.SearchAsync(Guid.NewGuid(), null, null)).Status.Should().Be(LyricsSearchStatus.TrackNotFound);

        _lrclib.SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>())
            .Returns(LrclibResponse.RateLimited(TimeSpan.FromSeconds(5)));
        LyricsSearchResult busy = await _service.SearchAsync(_trackId, null, null);
        busy.Status.Should().Be(LyricsSearchStatus.RateLimited);
        busy.RetryAfter.Should().Be(TimeSpan.FromSeconds(5));

        _settings.Current.Returns(new LibrarySettings());
        (await _service.SearchAsync(_trackId, null, null)).Status.Should().Be(LyricsSearchStatus.Disabled);
    }

    private static LrclibRecord Record(long id, string title, double duration, bool synced)
    {
        return new LrclibRecord
        {
            Id = id,
            TrackName = title,
            ArtistName = "Kerli",
            AlbumName = "Album",
            Duration = duration,
            PlainLyrics = "Line",
            SyncedLyrics = synced ? "[00:01.00]Line" : null,
        };
    }
}
