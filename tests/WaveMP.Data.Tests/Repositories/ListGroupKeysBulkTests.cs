using AwesomeAssertions;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

/// <summary>
/// The keys of the groups of long lists: a library of 5000 tracks on 800 albums of 160 artists, so the groups of a
/// list can be formed and jumped to without loading the list.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ListGroupKeysBulkTests : LibraryDatabaseTests
{
    private const int AlbumCount = 800;
    private const int TracksOnAlbums = 4800;
    private const int LooseTracks = 200;
    private const int ArtistCount = 160;

    private static readonly string[] _names =
    [
        "Alpha", "beta", "\u00c9cho", "\u00d6lund", "2 Fast", "(Paren)", "Zeta", "\u03a9mega", "\u00dfand", "The",
        "\u00e5ngstr\u00f6m", "Mono", "_Under", "Kilo", "\u6771\u4eac", "x-ray",
    ];

    private static readonly string?[] _genres = ["Rock", "rock", "Pop", "Jazz", "\u00c9lectro", "80s", null];

    protected override string DatabaseName => "wavemp_tests_library_group_keys";

    private static IEnumerable<TestCaseData> TrackQueries()
    {
        yield return Track(TrackSortField.AlbumArtist, SortDirection.Asc);
        yield return Track(TrackSortField.Album, SortDirection.Desc);
        yield return Track(TrackSortField.Title, SortDirection.Asc);
        yield return Track(TrackSortField.Genre, SortDirection.Asc);
        yield return Track(TrackSortField.Year, SortDirection.Desc);
        yield return Track(TrackSortField.FirstSeenAt, SortDirection.Desc);
    }

    [TestCaseSource(nameof(TrackQueries))]
    public async Task ListGroupKeysAsync_LongTrackList_FormsGroupsThatCoverTheListAndStartAtTheirPosition(
        TrackSortField sort,
        SortDirection direction)
    {
        await SeedBulkLibraryAsync();
        TrackRepository repository = new(CreateDbContext());
        TrackListQuery query = new(0, TracksOnAlbums + LooseTracks, sort, direction);

        IReadOnlyList<GroupKeyRow> keys = await repository.ListGroupKeysAsync(query);
        Page<TrackRow> list = await repository.ListAsync(query);

        keys.Should().HaveCount(list.Total).And.HaveCount(TracksOnAlbums + LooseTracks);
        keys.Select(key => (key.AlbumId, key.ArtistId, key.Year)).Should().Equal(
            list.Items.Select(track => (track.AlbumId, track.ArtistId, track.Year)));

        List<(int Position, int Count)> runs = Runs(keys, TrackKey);
        runs.Count.Should().BeGreaterThan(AlbumCount / 2);
        ShouldCoverTheList(runs, keys.Count);
        foreach ((int position, _) in Samples(runs))
        {
            // The list at the offset of the position starts with the first track of the group.
            Page<TrackRow> page = await repository.ListAsync(query with { Offset = position, Limit = 1 });
            TrackRow first = page.Items.Single();
            (first.AlbumId, first.AlbumId is null ? first.ArtistId : null).Should().Be(TrackKey(keys[position]));
        }
    }

    [TestCase(AlbumSortField.Artist, SortDirection.Asc)]
    [TestCase(AlbumSortField.Title, SortDirection.Asc)]
    [TestCase(AlbumSortField.Title, SortDirection.Desc)]
    [TestCase(AlbumSortField.Year, SortDirection.Desc)]
    public async Task ListGroupKeysAsync_LongAlbumList_FormsGroupsThatCoverTheListAndStartAtTheirPosition(
        AlbumSortField sort,
        SortDirection direction)
    {
        await SeedBulkLibraryAsync();
        AlbumRepository repository = new(CreateDbContext());
        AlbumListQuery query = new(0, AlbumCount, sort, direction);

        IReadOnlyList<GroupKeyRow> keys = await repository.ListGroupKeysAsync(query);
        Page<AlbumRow> list = await repository.ListAsync(query);

        keys.Should().HaveCount(list.Total).And.HaveCount(AlbumCount);
        keys.Select(key => key.AlbumId).Should().Equal(list.Items.Select(album => (Guid?)album.Id));

        Func<GroupKeyRow, object?> keyOf = sort switch
        {
            AlbumSortField.Title => key => key.Initial?.ToUpperInvariant(),
            AlbumSortField.Year => key => key.Year,
            _ => key => key.ArtistId,
        };
        List<(int Position, int Count)> runs = Runs(keys, keyOf);
        runs.Count.Should().BeGreaterThan(10);
        ShouldCoverTheList(runs, keys.Count);
        foreach ((int position, _) in Samples(runs))
        {
            Page<AlbumRow> page = await repository.ListAsync(query with { Offset = position, Limit = 1 });
            page.Items.Single().Id.Should().Be(keys[position].AlbumId!.Value);
        }
    }

    [TestCase(SortDirection.Asc)]
    [TestCase(SortDirection.Desc)]
    public async Task ListGroupKeysAsync_LongArtistList_FormsLetterGroupsThatCoverTheList(SortDirection direction)
    {
        await SeedBulkLibraryAsync();
        ArtistRepository repository = new(CreateDbContext());
        ArtistListQuery query = new(0, ArtistCount, ArtistSortField.Name, direction);

        IReadOnlyList<GroupKeyRow> keys = await repository.ListGroupKeysAsync(query);
        Page<ArtistRow> list = await repository.ListAsync(query);

        keys.Select(key => key.ArtistId).Should().Equal(list.Items.Select(artist => (Guid?)artist.Id));
        keys.Select(key => key.Initial).Should().Equal(list.Items.Select(artist => artist.Name[..1]));
        ShouldCoverTheList(Runs(keys, key => key.Initial?.ToUpperInvariant()), ArtistCount);
    }

    private static TestCaseData Track(TrackSortField sort, SortDirection direction)
    {
        return new TestCaseData(sort, direction).SetArgDisplayNames(sort.ToString(), direction.ToString());
    }

    private static (Guid? AlbumId, Guid? ArtistId) TrackKey(GroupKeyRow key)
    {
        return (key.AlbumId, key.AlbumId is null ? key.ArtistId : null);
    }

    /// <summary>
    /// The runs of equal keys, the way the groups are formed.
    /// </summary>
    private static List<(int Position, int Count)> Runs<TKey>(
        IReadOnlyList<GroupKeyRow> keys,
        Func<GroupKeyRow, TKey> keyOf)
    {
        List<(int Position, int Count)> runs = [];
        for (int index = 0; index < keys.Count; index++)
        {
            if (index > 0 && EqualityComparer<TKey>.Default.Equals(keyOf(keys[index - 1]), keyOf(keys[index])))
            {
                runs[^1] = (runs[^1].Position, runs[^1].Count + 1);
            }
            else
            {
                runs.Add((index, 1));
            }
        }

        return runs;
    }

    private static void ShouldCoverTheList(List<(int Position, int Count)> runs, int total)
    {
        runs.Sum(run => run.Count).Should().Be(total);
        runs[0].Position.Should().Be(0);
        runs.Zip(runs.Skip(1)).Should().OnlyContain(pair =>
            pair.First.Position + pair.First.Count == pair.Second.Position);
    }

    /// <summary>
    /// The first, some in between and the last group.
    /// </summary>
    private static IEnumerable<(int Position, int Count)> Samples(List<(int Position, int Count)> runs)
    {
        return runs.Where((_, index) => index % (runs.Count / 5) == 0 || index == runs.Count - 1);
    }

    /// <summary>
    /// Artists with names of all kinds of initials, albums with and without album artist and year, tracks on the
    /// albums in an order of their own and tracks without album, with and without artist.
    /// </summary>
    private async Task SeedBulkLibraryAsync()
    {
        Random random = new(45);
        LibrarySeed seed = new();
        DateTimeOffset start = LibrarySeed.DefaultTime;

        Artist[] artists = [.. Enumerable.Range(0, ArtistCount).Select(index => seed.Artist(
            $"{_names[index % _names.Length]} {index}",
            start.AddMinutes(random.Next(10_000))))];
        Album[] albums = [.. Enumerable.Range(0, AlbumCount).Select(index =>
        {
            Artist? artist = index % 20 == 0 ? null : artists[random.Next(ArtistCount)];
            return seed.Album(
                $"{_names[random.Next(_names.Length)]} Record {index}",
                artist,
                isCompilation: artist is null,
                year: index % 15 == 0 ? null : 1960 + random.Next(60),
                firstSeenAt: start.AddMinutes(random.Next(10_000)));
        })];

        for (int index = 0; index < TracksOnAlbums; index++)
        {
            Album album = albums[random.Next(AlbumCount)];
            Artist artist = album.AlbumArtistId is null || random.Next(5) == 0
                ? artists[random.Next(ArtistCount)]
                : artists.First(candidate => candidate.Id == album.AlbumArtistId);
            seed.Track(
                $"{_names[random.Next(_names.Length)]} Song {index}",
                artist,
                album,
                number: random.Next(1, 15),
                disc: random.Next(3) == 0 ? 2 : 1,
                year: album.Year,
                genre: _genres[random.Next(_genres.Length)],
                duration: TimeSpan.FromSeconds(random.Next(60, 600)),
                firstSeenAt: album.FirstSeenAt.AddSeconds(random.Next(3)));
        }

        for (int index = 0; index < LooseTracks; index++)
        {
            seed.Track(
                $"{_names[random.Next(_names.Length)]} Single {index}",
                index % 10 == 0 ? null : artists[random.Next(ArtistCount)],
                genre: _genres[random.Next(_genres.Length)],
                firstSeenAt: start.AddMinutes(random.Next(10_000)));
        }

        await SaveAsync(seed);
    }
}
