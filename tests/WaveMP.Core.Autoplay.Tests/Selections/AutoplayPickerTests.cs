using AwesomeAssertions;
using WaveMP.Core.Autoplay.Selections;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Autoplay.Tests.Selections;

[TestFixture]
public class AutoplayPickerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyDictionary<Guid, DateTimeOffset> _noPlays = new Dictionary<Guid, DateTimeOffset>();

    private readonly Guid _alpha = Guid.NewGuid();
    private readonly Guid _beta = Guid.NewGuid();
    private readonly Guid _gamma = Guid.NewGuid();

    [Test]
    public void Pick_AlbumOfOneArtist_ChoosesMoreSongsOfThatArtist()
    {
        AutoplaySeedRow[] seeds = Seeds(_alpha, _alpha, _alpha);
        AutoplayCandidateRow[] alphaSongs = Songs(_alpha, 5);
        AutoplayCandidateRow[] betaSongs = Songs(_beta, 5);

        IReadOnlyList<Guid> picked = AutoplayPicker.Pick(
            seeds,
            [.. alphaSongs, .. betaSongs],
            [],
            _noPlays,
            10,
            new Random(1));

        picked.Should().BeEquivalentTo(alphaSongs.Select(song => song.TrackId));
    }

    [Test]
    public void Pick_ListOfSeveralArtists_ChoosesSongsOfAllOfThem()
    {
        AutoplaySeedRow[] seeds = Seeds(_alpha, _beta, _gamma);
        AutoplayCandidateRow[] songs = [.. Songs(_alpha, 2), .. Songs(_beta, 2), .. Songs(_gamma, 2)];

        IReadOnlyList<Guid> picked = AutoplayPicker.Pick(seeds, songs, [], _noPlays, 6, new Random(2));

        picked.Should().BeEquivalentTo(songs.Select(song => song.TrackId));
        picked.Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void Pick_ArtistMoreOftenInTheList_IsChosenMoreOften()
    {
        AutoplaySeedRow[] seeds = Seeds(_alpha, _alpha, _alpha, _beta);
        AutoplayCandidateRow[] songs = [.. Songs(_alpha, 3), .. Songs(_beta, 3)];
        HashSet<Guid> alphaSongs = [.. songs.Where(song => song.ArtistId == _alpha).Select(song => song.TrackId)];
        Random random = new(3);
        const int Draws = 4000;

        int alpha = Enumerable.Range(0, Draws)
            .Count(_ => alphaSongs.Contains(AutoplayPicker.Pick(seeds, songs, [], _noPlays, 1, random)[0]));

        // Three of four entries of the list are by Alpha.
        ((double)alpha / Draws).Should().BeApproximately(0.75, 0.03);
    }

    [Test]
    public void Pick_FavoritesAndWellRatedSongs_AreChosenABitMoreOften()
    {
        AutoplaySeedRow[] seeds = Seeds(_alpha);
        AutoplayCandidateRow plain = Song(_alpha);
        AutoplayCandidateRow loved = Song(_alpha, isFavorite: true, rating: 5);
        Random random = new(4);
        const int Draws = 4000;

        int lovedCount = Enumerable.Range(0, Draws)
            .Count(_ => AutoplayPicker.Pick(seeds, [plain, loved], [], _noPlays, 1, random)[0] == loved.TrackId);

        // Weights 1 and 1.5 * 1.5 = 2.25.
        ((double)lovedCount / Draws).Should().BeApproximately(2.25 / 3.25, 0.03);
    }

    [Test]
    public void Pick_SongRatedWithOneStar_IsNeverChosen()
    {
        AutoplaySeedRow[] seeds = Seeds(_alpha);
        AutoplayCandidateRow disliked = Song(_alpha, rating: 1);
        AutoplayCandidateRow other = Song(_alpha, rating: 2);

        IReadOnlyList<Guid> picked = AutoplayPicker.Pick(seeds, [disliked, other], [], _noPlays, 5, new Random(5));

        picked.Should().Equal(other.TrackId);
    }

    [Test]
    public void Pick_ArtistSongsAreNotEnough_AddsSongsOfTheSameGenresAfterThem()
    {
        AutoplaySeedRow[] seeds = [Seed(_alpha, "Rock", 2001), Seed(_alpha, "Rock", 2003)];
        AutoplayCandidateRow[] artistSongs = Songs(_alpha, 2);
        AutoplayCandidateRow[] fallback = [Song(_beta, genre: "rock", year: 1998), Song(_gamma, genre: "Rock", year: 2012)];

        IReadOnlyList<Guid> picked = AutoplayPicker.Pick(seeds, artistSongs, fallback, _noPlays, 10, new Random(6));

        picked.Take(2).Should().BeEquivalentTo(artistSongs.Select(song => song.TrackId));
        picked.Skip(2).Should().BeEquivalentTo(fallback.Select(song => song.TrackId));
    }

    [Test]
    public void Pick_EnoughArtistSongs_AddsNoFallback()
    {
        AutoplaySeedRow[] seeds = [Seed(_alpha, "Rock", 2001)];
        AutoplayCandidateRow[] artistSongs = Songs(_alpha, 3);

        IReadOnlyList<Guid> picked = AutoplayPicker.Pick(
            seeds,
            artistSongs,
            [Song(_beta, genre: "Rock")],
            _noPlays,
            3,
            new Random(7));

        picked.Should().BeEquivalentTo(artistSongs.Select(song => song.TrackId));
    }

    [Test]
    public void Pick_FallbackOfAnotherGenre_IsNotChosen()
    {
        AutoplaySeedRow[] seeds = [Seed(_alpha, "Rock", 2001)];
        AutoplayCandidateRow jazz = Song(_beta, genre: "Jazz");
        AutoplayCandidateRow rock = Song(_beta, genre: "Rock");

        IReadOnlyList<Guid> picked = AutoplayPicker.Pick(seeds, [], [jazz, rock], _noPlays, 5, new Random(8));

        picked.Should().Equal(rock.TrackId);
    }

    [Test]
    public void Pick_GenreMoreOftenInTheList_IsChosenMoreOftenInTheFallback()
    {
        AutoplaySeedRow[] seeds = [Seed(_alpha, "Rock", null), Seed(_alpha, "Rock", null), Seed(_alpha, "Pop", null)];
        AutoplayCandidateRow rock = Song(_beta, genre: "Rock");
        AutoplayCandidateRow pop = Song(_beta, genre: "Pop");
        Random random = new(9);
        const int Draws = 3000;

        int rockCount = Enumerable.Range(0, Draws)
            .Count(_ => AutoplayPicker.Pick(seeds, [], [rock, pop], _noPlays, 1, random)[0] == rock.TrackId);

        ((double)rockCount / Draws).Should().BeApproximately(2.0 / 3, 0.03);
    }

    [Test]
    public void Pick_RecentlyPlayedSongs_AreLeftOutWhileOthersMatch()
    {
        AutoplaySeedRow[] seeds = Seeds(_alpha);
        AutoplayCandidateRow played = Song(_alpha);
        AutoplayCandidateRow fresh = Song(_alpha);
        Dictionary<Guid, DateTimeOffset> plays = new() { [played.TrackId] = _now.AddHours(-1) };

        IReadOnlyList<Guid> picked = AutoplayPicker.Pick(seeds, [played, fresh], [], plays, 5, new Random(10));

        picked.Should().Equal(fresh.TrackId);
    }

    [Test]
    public void Pick_OnlyRecentlyPlayedSongsLeft_ChoosesThoseLongestAgo()
    {
        AutoplaySeedRow[] seeds = Seeds(_alpha);
        AutoplayCandidateRow[] songs = Songs(_alpha, 6);
        var plays = songs
            .Select((song, index) => (song, index))
            .ToDictionary(pair => pair.song.TrackId, pair => _now.AddHours(-10 + pair.index));

        IReadOnlyList<Guid> picked = AutoplayPicker.Pick(seeds, songs, [], plays, 2, new Random(11));

        // Half of them come back: the three played longest ago.
        picked.Should().HaveCount(2);
        picked.Should().BeSubsetOf(songs.Take(3).Select(song => song.TrackId));
    }

    [Test]
    public void Pick_DifferentRandomness_ChoosesInRandomOrder()
    {
        AutoplaySeedRow[] seeds = Seeds(_alpha);
        AutoplayCandidateRow[] songs = Songs(_alpha, 8);

        IReadOnlyList<Guid>[] orders = [.. Enumerable.Range(0, 5)
            .Select(seed => AutoplayPicker.Pick(seeds, songs, [], _noPlays, 8, new Random(seed)))];

        orders.Should().AllSatisfy(order => order.Should().BeEquivalentTo(songs.Select(song => song.TrackId)));
        orders.Select(order => string.Join(',', order)).Distinct().Should().HaveCountGreaterThan(1);
    }

    [Test]
    public void Pick_ListWithoutArtists_ChoosesNoArtistSongs()
    {
        AutoplaySeedRow[] seeds = [new AutoplaySeedRow(Guid.NewGuid(), null, null, null)];

        IReadOnlyList<Guid> picked = AutoplayPicker.Pick(seeds, Songs(_alpha, 2), [], _noPlays, 5, new Random(12));

        picked.Should().BeEmpty();
    }

    private static AutoplaySeedRow[] Seeds(params Guid[] artists)
    {
        return [.. artists.Select(artist => Seed(artist, null, null))];
    }

    private static AutoplaySeedRow Seed(Guid artist, string? genre, int? year)
    {
        return new AutoplaySeedRow(Guid.NewGuid(), artist, genre, year);
    }

    private static AutoplayCandidateRow[] Songs(Guid artist, int count)
    {
        return [.. Enumerable.Range(0, count).Select(_ => Song(artist))];
    }

    private static AutoplayCandidateRow Song(
        Guid artist,
        string? genre = null,
        int? year = null,
        bool isFavorite = false,
        int rating = 0)
    {
        return new AutoplayCandidateRow(Guid.NewGuid(), artist, genre, year, true, isFavorite, rating);
    }
}
