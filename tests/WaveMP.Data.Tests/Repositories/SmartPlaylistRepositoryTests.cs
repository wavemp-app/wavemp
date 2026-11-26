using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

/// <summary>
/// The translation of the rules of smart playlists into queries of the tracks, against the standard library (see
/// <see cref="LibraryDatabaseTests.SeedStandardLibraryAsync"/>), and the storage of smart playlists.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SmartPlaylistRepositoryTests : LibraryDatabaseTests
{
    /// <summary>
    /// Ten days after the tracks of the standard library were found for the first time.
    /// </summary>
    private static readonly DateTimeOffset _now = LibrarySeed.DefaultTime.AddDays(10);

    protected override string DatabaseName => "wavemp_tests_smart_playlists";

    [Test]
    public async Task ListTrackIdsAsync_WithoutRules_ReturnsEveryTrack()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> ids = await MatchAsync(user, SmartPlaylistMatch.All);

        ids.Should().BeEquivalentTo(AllTracks(library));
    }

    [Test]
    public async Task ListTrackIdsAsync_ArtistIs_MatchesTheArtistOrTheAlbumArtistWithoutRegardToCase()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> ids = await MatchAsync(
            user,
            Text(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "alpha BAND"));

        // Duet is by Guest Star on First Album, whose album artist is Alpha Band.
        ids.Should().BeEquivalentTo([library.Opening.Id, library.Closing.Id, library.Duet.Id, library.HitOne.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_ArtistIsNot_MatchesEveryOtherTrack()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> ids = await MatchAsync(
            user,
            Text(SmartPlaylistField.Artist, SmartPlaylistOperator.IsNot, "Alpha Band"));

        ids.Should().BeEquivalentTo([library.Ballad.Id, library.HitTwo.Id, library.Clip.Id, library.LooseSong.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_ArtistContains_MatchesPartOfTheName()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> ids = await MatchAsync(
            user,
            Text(SmartPlaylistField.Artist, SmartPlaylistOperator.Contains, "sing"));

        ids.Should().BeEquivalentTo([library.Ballad.Id, library.HitTwo.Id, library.Clip.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_AlbumIs_MatchesTheTitleOfTheAlbum()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> ids = await MatchAsync(
            user,
            Text(SmartPlaylistField.Album, SmartPlaylistOperator.Is, "first album"));

        ids.Should().BeEquivalentTo([library.Opening.Id, library.Closing.Id, library.Duet.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_AlbumDoesNotContain_AlsoMatchesTracksWithoutAlbum()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> ids = await MatchAsync(
            user,
            Text(SmartPlaylistField.Album, SmartPlaylistOperator.DoesNotContain, "album"));

        ids.Should().BeEquivalentTo([library.HitOne.Id, library.HitTwo.Id, library.Clip.Id, library.LooseSong.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_GenreIsAndIsNot_AreComplementsAlsoForTracksWithoutGenre()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> rock = await MatchAsync(
            user,
            Text(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "ROCK"));
        IReadOnlyList<Guid> other = await MatchAsync(
            user,
            Text(SmartPlaylistField.Genre, SmartPlaylistOperator.IsNot, "rock"));

        rock.Should().BeEquivalentTo([library.Opening.Id, library.Closing.Id, library.Duet.Id]);
        other.Should().BeEquivalentTo(
            [library.Ballad.Id, library.HitOne.Id, library.HitTwo.Id, library.Clip.Id, library.LooseSong.Id]);
    }

    [TestCase("%")]
    [TestCase("_op")]
    public async Task ListTrackIdsAsync_TextWithWildcards_MatchesOnlyTheTextItself(string text)
    {
        await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> ids = await MatchAsync(
            user,
            Text(SmartPlaylistField.Genre, SmartPlaylistOperator.Contains, text));

        ids.Should().BeEmpty();
    }

    [Test]
    public async Task ListTrackIdsAsync_YearRanges_MatchTheBoundsAndLeaveOutTracksWithoutYear()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> between = await MatchAsync(
            user,
            Number(SmartPlaylistField.Year, SmartPlaylistOperator.Between, 2001, 2005));
        IReadOnlyList<Guid> atLeast = await MatchAsync(
            user,
            Number(SmartPlaylistField.Year, SmartPlaylistOperator.AtLeast, 2005));
        IReadOnlyList<Guid> atMost = await MatchAsync(
            user,
            Number(SmartPlaylistField.Year, SmartPlaylistOperator.AtMost, 2001));
        IReadOnlyList<Guid> isNot = await MatchAsync(
            user,
            Number(SmartPlaylistField.Year, SmartPlaylistOperator.IsNot, 2001));

        between.Should().BeEquivalentTo(
            [library.Opening.Id, library.Closing.Id, library.Duet.Id, library.HitOne.Id, library.HitTwo.Id]);
        atLeast.Should().BeEquivalentTo([library.Ballad.Id, library.HitOne.Id, library.HitTwo.Id]);
        atMost.Should().BeEquivalentTo([library.Opening.Id, library.Closing.Id, library.Duet.Id]);
        isNot.Should().BeEquivalentTo(
            [library.Ballad.Id, library.HitOne.Id, library.HitTwo.Id, library.Clip.Id, library.LooseSong.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_Rating_UsesTheRatingsOfTheUserAndCountsUnratedTracksAsZero()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        User user = seed.User();
        User other = seed.User();
        seed.Rating(user, library.Opening, 5);
        seed.Rating(user, library.Ballad, 4);
        seed.Rating(user, library.HitOne, 2);
        seed.Rating(other, library.Closing, 5);
        await SaveAsync(seed);

        IReadOnlyList<Guid> good = await MatchAsync(
            user,
            Number(SmartPlaylistField.Rating, SmartPlaylistOperator.AtLeast, 4));
        IReadOnlyList<Guid> unrated = await MatchAsync(
            user,
            Number(SmartPlaylistField.Rating, SmartPlaylistOperator.Is, 0));

        good.Should().BeEquivalentTo([library.Opening.Id, library.Ballad.Id]);
        unrated.Should().BeEquivalentTo(
            [library.Closing.Id, library.Duet.Id, library.HitTwo.Id, library.Clip.Id, library.LooseSong.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_Favorite_UsesTheFavoritesOfTheUser()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        User user = seed.User();
        User other = seed.User();
        seed.Favorite(user, library.Closing);
        seed.Favorite(other, library.Duet);
        await SaveAsync(seed);

        IReadOnlyList<Guid> favorites = await MatchAsync(
            user,
            Flag(SmartPlaylistField.Favorite, SmartPlaylistOperator.IsTrue));
        IReadOnlyList<Guid> others = await MatchAsync(
            user,
            Flag(SmartPlaylistField.Favorite, SmartPlaylistOperator.IsFalse));

        favorites.Should().Equal(library.Closing.Id);
        others.Should().HaveCount(7).And.NotContain(library.Closing.Id);
    }

    [Test]
    public async Task ListTrackIdsAsync_PlayCountAndLastPlayed_UseThePlaysOfTheUser()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        User user = seed.User();
        User other = seed.User();
        seed.Play(user, library.Opening, _now.AddDays(-20));
        seed.Play(user, library.Opening, _now.AddDays(-10));
        seed.Play(user, library.Opening, _now.AddDays(-1));
        seed.Play(user, library.Ballad, _now.AddDays(-30));
        seed.Play(other, library.Duet, _now.AddDays(-1));
        await SaveAsync(seed);

        IReadOnlyList<Guid> never = await MatchAsync(
            user,
            Number(SmartPlaylistField.PlayCount, SmartPlaylistOperator.Is, 0));
        IReadOnlyList<Guid> often = await MatchAsync(
            user,
            Number(SmartPlaylistField.PlayCount, SmartPlaylistOperator.AtLeast, 2));
        IReadOnlyList<Guid> lately = await MatchAsync(
            user,
            Days(SmartPlaylistField.LastPlayed, SmartPlaylistOperator.InLast, 7));
        IReadOnlyList<Guid> notLately = await MatchAsync(
            user,
            Days(SmartPlaylistField.LastPlayed, SmartPlaylistOperator.NotInLast, 7));

        never.Should().HaveCount(6).And.NotContain([library.Opening.Id, library.Ballad.Id]);
        often.Should().Equal(library.Opening.Id);
        lately.Should().Equal(library.Opening.Id);
        // Never played tracks were not played within the last seven days either.
        notLately.Should().HaveCount(7).And.NotContain(library.Opening.Id);
    }

    [Test]
    public async Task ListTrackIdsAsync_DateAdded_MatchesTracksFoundWithinTheDays()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> recent = await MatchAsync(
            user,
            Days(SmartPlaylistField.DateAdded, SmartPlaylistOperator.InLast, 8));
        IReadOnlyList<Guid> older = await MatchAsync(
            user,
            Days(SmartPlaylistField.DateAdded, SmartPlaylistOperator.NotInLast, 8));

        // Found on the first day of the library (ten days ago), the second and the third day (eight days ago).
        recent.Should().BeEquivalentTo([library.HitOne.Id, library.HitTwo.Id, library.Clip.Id, library.LooseSong.Id]);
        older.Should().BeEquivalentTo([library.Opening.Id, library.Closing.Id, library.Duet.Id, library.Ballad.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_Duration_ComparesWholeSeconds()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> three = await MatchAsync(
            user,
            Number(SmartPlaylistField.Duration, SmartPlaylistOperator.Is, 180));
        IReadOnlyList<Guid> shorter = await MatchAsync(
            user,
            Number(SmartPlaylistField.Duration, SmartPlaylistOperator.AtMost, 120));
        IReadOnlyList<Guid> between = await MatchAsync(
            user,
            Number(SmartPlaylistField.Duration, SmartPlaylistOperator.Between, 181, 300));
        IReadOnlyList<Guid> notThree = await MatchAsync(
            user,
            Number(SmartPlaylistField.Duration, SmartPlaylistOperator.IsNot, 180));

        three.Should().BeEquivalentTo([library.Opening.Id, library.HitOne.Id, library.HitTwo.Id]);
        shorter.Should().BeEquivalentTo([library.Duet.Id, library.LooseSong.Id]);
        between.Should().BeEquivalentTo([library.Closing.Id, library.Ballad.Id]);
        // Clip has no duration.
        notThree.Should().BeEquivalentTo(
            [library.Closing.Id, library.Duet.Id, library.Ballad.Id, library.Clip.Id, library.LooseSong.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_AudioAndVideo_MatchTheFilesOfTheTrack()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> withoutAudio = await MatchAsync(
            user,
            Flag(SmartPlaylistField.HasAudio, SmartPlaylistOperator.IsFalse));
        IReadOnlyList<Guid> withVideo = await MatchAsync(
            user,
            Flag(SmartPlaylistField.HasVideo, SmartPlaylistOperator.IsTrue));

        withoutAudio.Should().Equal(library.Clip.Id);
        withVideo.Should().BeEquivalentTo([library.Closing.Id, library.Clip.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_MatchAll_NeedsEveryRule()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> ids = await MatchAsync(
            user,
            SmartPlaylistMatch.All,
            Text(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "pop"),
            Number(SmartPlaylistField.Year, SmartPlaylistOperator.Between, 2000, 2009));

        ids.Should().BeEquivalentTo([library.HitOne.Id, library.HitTwo.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_MatchAny_NeedsOneRule()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> ids = await MatchAsync(
            user,
            SmartPlaylistMatch.Any,
            Text(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "pop"),
            Flag(SmartPlaylistField.HasAudio, SmartPlaylistOperator.IsFalse));

        ids.Should().BeEquivalentTo([library.Ballad.Id, library.HitOne.Id, library.HitTwo.Id, library.Clip.Id]);
    }

    [Test]
    public async Task ListTrackIdsAsync_SortedByYearDescending_PutsTracksWithoutYearLast()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        Page<Guid> page = await CreateRepository().ListTrackIdsAsync(
            user.Id,
            new SmartPlaylistCriteria(SmartPlaylistMatch.All, [], SmartPlaylistSortField.Year, SortDirection.Desc),
            _now,
            0,
            100);

        page.Items.Should().Equal(
            library.Ballad.Id,
            library.HitOne.Id,
            library.HitTwo.Id,
            library.Opening.Id,
            library.Closing.Id,
            library.Duet.Id,
            library.Clip.Id,
            library.LooseSong.Id);
    }

    [Test]
    public async Task ListTrackIdsAsync_SortedByPlaysAndRating_UsesTheDataOfTheUser()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        User user = seed.User();
        seed.Play(user, library.Ballad, _now.AddDays(-3));
        seed.Play(user, library.Ballad, _now.AddDays(-2));
        seed.Play(user, library.Duet, _now.AddDays(-1));
        seed.Rating(user, library.HitTwo, 5);
        seed.Rating(user, library.Opening, 3);
        await SaveAsync(seed);
        ISmartPlaylistRepository repository = CreateRepository();
        SmartPlaylistRule played = Number(SmartPlaylistField.PlayCount, SmartPlaylistOperator.AtLeast, 1);
        SmartPlaylistRule rated = Number(SmartPlaylistField.Rating, SmartPlaylistOperator.AtLeast, 1);

        Page<Guid> byPlays = await repository.ListTrackIdsAsync(
            user.Id,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [played],
                SmartPlaylistSortField.PlayCount,
                SortDirection.Desc),
            _now,
            0,
            100);
        Page<Guid> byLastPlayed = await repository.ListTrackIdsAsync(
            user.Id,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [played],
                SmartPlaylistSortField.LastPlayed,
                SortDirection.Desc),
            _now,
            0,
            100);
        Page<Guid> byRating = await repository.ListTrackIdsAsync(
            user.Id,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [rated],
                SmartPlaylistSortField.Rating,
                SortDirection.Asc),
            _now,
            0,
            100);

        byPlays.Items.Should().Equal(library.Ballad.Id, library.Duet.Id);
        byLastPlayed.Items.Should().Equal(library.Duet.Id, library.Ballad.Id);
        byRating.Items.Should().Equal(library.Opening.Id, library.HitTwo.Id);
    }

    [Test]
    public async Task ListTrackIdsAsync_WithLimit_EndsTheListAndItsPagesAtTheLimit()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        SmartPlaylistCriteria criteria = new(
            SmartPlaylistMatch.All,
            [],
            SmartPlaylistSortField.Year,
            SortDirection.Asc,
            Limit: 5);
        ISmartPlaylistRepository repository = CreateRepository();

        Page<Guid> first = await repository.ListTrackIdsAsync(user.Id, criteria, _now, 0, 3);
        Page<Guid> second = await repository.ListTrackIdsAsync(user.Id, criteria, _now, 3, 3);
        Page<Guid> beyond = await repository.ListTrackIdsAsync(user.Id, criteria, _now, 6, 3);

        first.Total.Should().Be(5);
        first.Items.Should().Equal(library.Opening.Id, library.Closing.Id, library.Duet.Id);
        second.Items.Should().Equal(library.HitOne.Id, library.HitTwo.Id);
        beyond.Items.Should().BeEmpty();
        beyond.Total.Should().Be(5);
    }

    [Test]
    public async Task SummarizeAsync_WithAndWithoutLimit_CountsAndAddsUpTheTracks()
    {
        await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        ISmartPlaylistRepository repository = CreateRepository();
        SmartPlaylistRule rock = Text(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "rock");

        SmartPlaylistSummary all = await repository.SummarizeAsync(
            user.Id,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [rock],
                SmartPlaylistSortField.Duration,
                SortDirection.Desc),
            _now);
        SmartPlaylistSummary longest = await repository.SummarizeAsync(
            user.Id,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [rock],
                SmartPlaylistSortField.Duration,
                SortDirection.Desc,
                Limit: 1),
            _now);
        SmartPlaylistSummary none = await repository.SummarizeAsync(
            user.Id,
            new SmartPlaylistCriteria(
                SmartPlaylistMatch.All,
                [Text(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "jazz")],
                SmartPlaylistSortField.Title,
                SortDirection.Asc),
            _now);

        all.Should().Be(new SmartPlaylistSummary(3, TimeSpan.FromMinutes(9).TotalMilliseconds));
        longest.Should().Be(new SmartPlaylistSummary(1, TimeSpan.FromMinutes(4).TotalMilliseconds));
        none.Should().Be(new SmartPlaylistSummary(0, 0));
    }

    [Test]
    public async Task ListCoverCandidatesAsync_FirstTracks_ReturnsThemInTheirOrderWithTheirCovers()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        var playlistId = Guid.CreateVersion7();

        IReadOnlyList<PlaylistCoverCandidateRow> candidates = await CreateRepository().ListCoverCandidatesAsync(
            playlistId,
            user.Id,
            new SmartPlaylistCriteria(SmartPlaylistMatch.All, [], SmartPlaylistSortField.Year, SortDirection.Desc),
            _now,
            4);

        candidates.Should().Equal(
            new PlaylistCoverCandidateRow(playlistId, 0, library.Ballad.Id, library.SecondAlbum.Id, true),
            new PlaylistCoverCandidateRow(playlistId, 1, library.HitOne.Id, library.Hits.Id, false),
            new PlaylistCoverCandidateRow(playlistId, 2, library.HitTwo.Id, library.Hits.Id, false),
            new PlaylistCoverCandidateRow(playlistId, 3, library.Opening.Id, library.FirstAlbum.Id, true));
    }

    [Test]
    public async Task CreateAsync_SmartPlaylist_KeepsItsRulesForItsUserOnly()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        SmartPlaylist playlist = NewPlaylist(
            user,
            "Rock from the 2000s",
            Text(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "Rock"),
            Number(SmartPlaylistField.Year, SmartPlaylistOperator.Between, 2000, 2009),
            Flag(SmartPlaylistField.HasVideo, SmartPlaylistOperator.IsFalse));

        await CreateRepository().CreateAsync(playlist);
        SmartPlaylist? found = await CreateRepository().FindAsync(user.Id, playlist.Id);
        SmartPlaylist? foreign = await CreateRepository().FindAsync(other.Id, playlist.Id);

        found.Should().BeEquivalentTo(playlist, options => options.Excluding(item => item.User));
        found!.Rules.Should().Equal(playlist.Rules);
        foreign.Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_UnknownUser_ThrowsUserNotFound()
    {
        SmartPlaylist playlist = NewPlaylist(new LibrarySeed().User(), "Orphan");

        Func<Task> create = () => CreateRepository().CreateAsync(playlist);

        (await create.Should().ThrowAsync<UserNotFoundException>()).Which.UserId.Should().Be(playlist.UserId);
    }

    [Test]
    public async Task UpdateAsync_ExistingSmartPlaylist_ReplacesItsRulesButNotForOtherUsers()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        SmartPlaylist playlist = NewPlaylist(
            user,
            "Before",
            Flag(SmartPlaylistField.Favorite, SmartPlaylistOperator.IsTrue));
        await CreateRepository().CreateAsync(playlist);
        SmartPlaylist changed = NewPlaylist(
            user,
            "After",
            Days(SmartPlaylistField.LastPlayed, SmartPlaylistOperator.InLast, 7));
        changed.Id = playlist.Id;
        changed.Match = SmartPlaylistMatch.Any;
        changed.Sort = SmartPlaylistSortField.LastPlayed;
        changed.Direction = SortDirection.Desc;
        changed.Limit = 25;
        changed.UpdatedAt = playlist.UpdatedAt.AddHours(1);

        bool foreign = await CreateRepository().UpdateAsync(new SmartPlaylist
        {
            Id = playlist.Id,
            UserId = other.Id,
            Name = "Stolen",
            NormalizedName = "STOLEN",
            Match = SmartPlaylistMatch.All,
            Sort = SmartPlaylistSortField.Title,
            Direction = SortDirection.Asc,
            CreatedAt = playlist.CreatedAt,
            UpdatedAt = playlist.UpdatedAt,
        });
        bool updated = await CreateRepository().UpdateAsync(changed);
        SmartPlaylist? found = await CreateRepository().FindAsync(user.Id, playlist.Id);

        foreign.Should().BeFalse();
        updated.Should().BeTrue();
        found.Should().BeEquivalentTo(
            changed,
            options => options.Excluding(item => item.User).Excluding(item => item.CreatedAt));
        found!.CreatedAt.Should().Be(playlist.CreatedAt);
    }

    [Test]
    public async Task ListAsync_SmartPlaylistsOfTheUser_AreSortedAndSearchedByName()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        ISmartPlaylistRepository repository = CreateRepository();
        await repository.CreateAsync(NewPlaylist(user, "beta"));
        await repository.CreateAsync(NewPlaylist(user, "Alpha"));
        await repository.CreateAsync(NewPlaylist(other, "Alpha Other"));

        Page<SmartPlaylist> all = await repository.ListAsync(new PlaylistListQuery(user.Id, 0, 10));
        Page<SmartPlaylist> searched = await repository.ListAsync(new PlaylistListQuery(user.Id, 0, 10, Search: "alp"));

        all.Items.Select(item => item.Name).Should().Equal("Alpha", "beta");
        searched.Items.Select(item => item.Name).Should().Equal("Alpha");
    }

    [Test]
    public async Task DeleteAsync_SmartPlaylist_IsGoneForItsUserOnly()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        SmartPlaylist playlist = NewPlaylist(user, "Gone");
        await CreateRepository().CreateAsync(playlist);

        bool foreign = await CreateRepository().DeleteAsync(other.Id, playlist.Id);
        bool deleted = await CreateRepository().DeleteAsync(user.Id, playlist.Id);
        bool again = await CreateRepository().DeleteAsync(user.Id, playlist.Id);

        foreign.Should().BeFalse();
        deleted.Should().BeTrue();
        again.Should().BeFalse();
        (await CreateRepository().FindAsync(user.Id, playlist.Id)).Should().BeNull();
    }

    [Test]
    public async Task LinkAsync_RulesNamingEntries_GetTheIdsOfTheEntriesWithTheNormalizedNames()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        SmartPlaylistRule[] rules =
        [
            Text(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "alpha-band"),
            Text(SmartPlaylistField.Album, SmartPlaylistOperator.IsNot, "SECOND ALBUM!"),
            Text(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "rock"),
            Text(SmartPlaylistField.Artist, SmartPlaylistOperator.Contains, "Alpha"),
            Text(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "Nobody"),
            Linked(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "Beta Singer", Guid.NewGuid()),
            Linked(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "Old Name", library.Guest.Id),
            Number(SmartPlaylistField.Year, SmartPlaylistOperator.AtLeast, 2000),
        ];

        IReadOnlyList<SmartPlaylistRule> linked = await CreateRepository().LinkAsync(rules);

        linked.Select(rule => rule.ReferenceId).Should().Equal(
            library.Alpha.Id,
            library.SecondAlbum.Id,
            library.Opening.GenreId,
            null,
            null,
            library.Beta.Id,
            library.Guest.Id,
            null);
        linked.Select(rule => rule.Text).Should().Equal(rules.Select(rule => rule.Text));
    }

    [Test]
    public async Task LinkAsync_SeveralAlbumsWithTheTitle_LeavesTheRuleUnlinked()
    {
        await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        Artist other = seed.Artist("Other Band");
        seed.Track("Elsewhere", other, seed.Album("First-Album", other));
        await SaveAsync(seed);

        IReadOnlyList<SmartPlaylistRule> linked = await CreateRepository().LinkAsync(
            [Text(SmartPlaylistField.Album, SmartPlaylistOperator.Is, "First Album")]);

        linked.Single().ReferenceId.Should().BeNull();
    }

    [Test]
    public async Task NameReferencesAsync_LinkedRules_ShowTheCurrentNamesAndForgetRemovedEntries()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        SmartPlaylistRule[] rules =
        [
            Linked(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "alpha", library.Alpha.Id),
            Linked(SmartPlaylistField.Album, SmartPlaylistOperator.Is, "Gone", Guid.NewGuid()),
            Text(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "pop"),
        ];

        IReadOnlyList<SmartPlaylistRule> shown = await CreateRepository().NameReferencesAsync(rules);

        shown.Should().Equal(
            rules[0] with { Text = "Alpha Band" },
            rules[1] with { ReferenceId = null },
            rules[2]);
    }

    [Test]
    public async Task ListTrackIdsAsync_LinkedArtistAfterRenaming_StillMatchesItsSongs()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        SmartPlaylistRule linked = Linked(
            SmartPlaylistField.Artist,
            SmartPlaylistOperator.Is,
            "Alpha Band",
            library.Alpha.Id);
        SmartPlaylistRule unlinked = Text(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "Alpha Band");
        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            Artist artist = await dbContext.Artists.SingleAsync(item => item.Id == library.Alpha.Id);
            artist.Name = "The Alpha Collective";
            artist.NormalizedName = "thealphacollective";
            await dbContext.SaveChangesAsync();
        }

        IReadOnlyList<Guid> byId = await MatchAsync(user, linked);
        IReadOnlyList<Guid> byText = await MatchAsync(user, unlinked);
        IReadOnlyList<Guid> others = await MatchAsync(user, linked with { Operator = SmartPlaylistOperator.IsNot });

        byId.Should().BeEquivalentTo([library.Opening.Id, library.Closing.Id, library.Duet.Id, library.HitOne.Id]);
        byText.Should().BeEmpty();
        others.Should().BeEquivalentTo(AllTracks(library).Except(byId));
    }

    [Test]
    public async Task ListTrackIdsAsync_LinkedAlbumAndGenre_MatchTheirSongs()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<Guid> album = await MatchAsync(
            user,
            Linked(SmartPlaylistField.Album, SmartPlaylistOperator.Is, "x", library.SecondAlbum.Id));
        IReadOnlyList<Guid> genre = await MatchAsync(
            user,
            Linked(SmartPlaylistField.Genre, SmartPlaylistOperator.IsNot, "x", library.Ballad.GenreId));

        album.Should().Equal(library.Ballad.Id);
        genre.Should().BeEquivalentTo(
            [library.Opening.Id, library.Closing.Id, library.Duet.Id, library.Clip.Id, library.LooseSong.Id]);
    }

    [Test]
    public async Task DeleteOrphansAsync_ArtistJoinedIntoAnother_MovesTheRulesToTheArtistThatStays()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        SmartPlaylist playlist = NewPlaylist(
            user,
            "Guest",
            Linked(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "Guest Star", library.Guest.Id),
            Linked(SmartPlaylistField.Album, SmartPlaylistOperator.Is, "Hits", library.Hits.Id));
        await CreateRepository().CreateAsync(playlist);
        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            dbContext.ArtistMoves.Add(new ArtistMove
            {
                FromArtistId = library.Guest.Id,
                ToArtistId = library.Beta.Id,
                TrackCount = 2,
                MovedAt = _now,
            });
            await dbContext.SaveChangesAsync();
            await dbContext.Tracks.Where(track => track.ArtistId == library.Guest.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(track => track.ArtistId, library.Beta.Id));
            await dbContext.Tracks.Where(track => track.AlbumId == library.Hits.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(track => track.AlbumId, (Guid?)null));
        }

        await new LibraryScanRepository(CreateDbContext()).DeleteOrphansAsync(_now);

        SmartPlaylist stored = (await CreateRepository().FindAsync(user.Id, playlist.Id))!;
        stored.Rules.Should().Equal(
            Linked(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "Beta Singer", library.Beta.Id),
            Text(SmartPlaylistField.Album, SmartPlaylistOperator.Is, "Hits"));
        stored.UpdatedAt.Should().Be(playlist.UpdatedAt);
    }

    private static Guid[] AllTracks(StandardLibrary library)
    {
        return
        [
            library.Opening.Id,
            library.Closing.Id,
            library.Duet.Id,
            library.Ballad.Id,
            library.HitOne.Id,
            library.HitTwo.Id,
            library.Clip.Id,
            library.LooseSong.Id,
        ];
    }

    private static SmartPlaylistRule Text(SmartPlaylistField field, SmartPlaylistOperator op, string text)
    {
        return new SmartPlaylistRule(field, op, Text: text);
    }

    private static SmartPlaylistRule Linked(
        SmartPlaylistField field,
        SmartPlaylistOperator op,
        string text,
        Guid? referenceId)
    {
        return new SmartPlaylistRule(field, op, Text: text, ReferenceId: referenceId);
    }

    private static SmartPlaylistRule Number(
        SmartPlaylistField field,
        SmartPlaylistOperator op,
        int number,
        int? numberTo = null)
    {
        return new SmartPlaylistRule(field, op, Number: number, NumberTo: numberTo);
    }

    private static SmartPlaylistRule Days(SmartPlaylistField field, SmartPlaylistOperator op, int days)
    {
        return new SmartPlaylistRule(field, op, Number: days);
    }

    private static SmartPlaylistRule Flag(SmartPlaylistField field, SmartPlaylistOperator op)
    {
        return new SmartPlaylistRule(field, op);
    }

    private static SmartPlaylist NewPlaylist(User user, string name, params SmartPlaylistRule[] rules)
    {
        return new SmartPlaylist
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Match = SmartPlaylistMatch.All,
            Rules = [.. rules],
            Sort = SmartPlaylistSortField.Artist,
            Direction = SortDirection.Asc,
            CreatedAt = LibrarySeed.DefaultTime,
            UpdatedAt = LibrarySeed.DefaultTime,
        };
    }

    private Task<IReadOnlyList<Guid>> MatchAsync(User user, SmartPlaylistRule rule)
    {
        return MatchAsync(user, SmartPlaylistMatch.All, rule);
    }

    /// <summary>
    /// The ids of the tracks the rules pick for the user, sorted by title.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> MatchAsync(
        User user,
        SmartPlaylistMatch match,
        params SmartPlaylistRule[] rules)
    {
        Page<Guid> page = await CreateRepository().ListTrackIdsAsync(
            user.Id,
            new SmartPlaylistCriteria(match, rules, SmartPlaylistSortField.Title, SortDirection.Asc),
            _now,
            0,
            100);
        page.Total.Should().Be(page.Items.Count);
        return page.Items;
    }

    private ISmartPlaylistRepository CreateRepository()
    {
        return new SmartPlaylistRepository(CreateDbContext());
    }

    private async Task<User> AddUserAsync()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        await SaveAsync(seed);
        return user;
    }
}
