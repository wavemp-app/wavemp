using AwesomeAssertions;
using WaveMP.Core.Metadata.Resolutions;
using WaveMP.Core.Scanning.Processors;
using WaveMP.Core.Scanning.Upgrades;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Tests.Upgrades;

[TestFixture]
public class NameKeyUpgradePlannerTests
{
    private static readonly DateTimeOffset _start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public void Plan_ArtistsWithEqualNormalizedNames_JoinsThemIntoTheOneSeenFirst()
    {
        NameKeyUpgradeArtist kFlay = Artist("K.Flay", days: 2);
        NameKeyUpgradeArtist kFlaySpaced = Artist("K Flay", days: 1);
        NameKeyUpgradeArtist kda = Artist("K/DA", days: 3);
        NameKeyUpgradeArtist acdc = Artist("AC/DC", days: 4);
        NameKeyUpgradeArtist acdcDashed = Artist("AC-DC", days: 4);

        NameKeyUpgradePlan plan = NameKeyUpgradePlanner.Plan(
            Input(artists: [kFlay, kFlaySpaced, kda, acdc, acdcDashed]));

        NameKeyUpgradeArtist acdcSurvivor = acdc.Id.CompareTo(acdcDashed.Id) < 0 ? acdc : acdcDashed;
        NameKeyUpgradeArtist acdcJoined = acdcSurvivor == acdc ? acdcDashed : acdc;
        plan.ArtistKeys.Should().BeEquivalentTo(new Dictionary<Guid, string>
        {
            [kFlaySpaced.Id] = "kflay",
            [kda.Id] = "kda",
            [acdcSurvivor.Id] = "acdc",
        });
        plan.ArtistMerges.Should().BeEquivalentTo(new Dictionary<Guid, Guid>
        {
            [kFlay.Id] = kFlaySpaced.Id,
            [acdcJoined.Id] = acdcSurvivor.Id,
        });
    }

    [Test]
    public void Plan_ArtistsInOtherScriptsAndWithoutLetters_StayApart()
    {
        NameKeyUpgradeArtist japanese = Artist("坂本龍一");
        NameKeyUpgradeArtist symbols = Artist("!!!");
        NameKeyUpgradeArtist question = Artist("?");

        NameKeyUpgradePlan plan = NameKeyUpgradePlanner.Plan(Input(artists: [japanese, symbols, question]));

        plan.ArtistMerges.Should().BeEmpty();
        plan.ArtistKeys.Values.Should().BeEquivalentTo("坂本龍一", "!!!", "?");
    }

    [Test]
    public void Plan_AlbumsOfJoinedArtistsWithEqualTitles_JoinsThem()
    {
        NameKeyUpgradeArtist dotted = Artist("K.Flay", days: 1);
        NameKeyUpgradeArtist spaced = Artist("K Flay", days: 2);
        NameKeyUpgradeAlbum first = Album("Solutions", dotted, days: 1);
        NameKeyUpgradeAlbum second = Album("SOLUTIONS!", spaced, days: 2);
        NameKeyUpgradeAlbum other = Album("Every Where Is Some Where", spaced, days: 3);

        NameKeyUpgradePlan plan = NameKeyUpgradePlanner.Plan(
            Input(artists: [dotted, spaced], albums: [first, second, other]));

        plan.AlbumMerges.Should().BeEquivalentTo(new Dictionary<Guid, Guid> { [second.Id] = first.Id });
        plan.AlbumKeys[first.Id].Should().Be((AlbumKeys.Create("Solutions", "kflay"), "solutions"));
        plan.AlbumKeys[other.Id].NormalizedTitle.Should().Be("everywhereissomewhere");
        plan.AlbumKeys.Should().NotContainKey(second.Id);
    }

    [Test]
    public void Plan_AlbumsWithoutArtist_AreKeptApartByTheirFolders()
    {
        Guid source = MediaSource.DefaultId;
        NameKeyUpgradeAlbum here = Album("Mix", null, source: source, directory: "Mixes/One");
        NameKeyUpgradeAlbum hereAgain = Album("MIX", null, days: 1, source: source, directory: "Mixes/One");
        NameKeyUpgradeAlbum there = Album("Mix", null, source: source, directory: "Mixes/Two");
        NameKeyUpgradeAlbum empty = Album("Mix", null);

        NameKeyUpgradePlan plan = NameKeyUpgradePlanner.Plan(Input(albums: [here, hereAgain, there, empty]));

        plan.AlbumMerges.Should().BeEquivalentTo(new Dictionary<Guid, Guid> { [hereAgain.Id] = here.Id });
        plan.AlbumKeys[here.Id].AlbumKey.Should().Be(
            AlbumKeys.Create("Mix", null, FolderProcessor.AlbumFolderOf(source, "Mixes/One")));
        plan.AlbumKeys.Should().ContainKey(there.Id).And.NotContainKey(empty.Id);
    }

    [Test]
    public void Plan_GenresWithEqualNormalizedNames_JoinsThemIntoTheOneWithTheMostTracks()
    {
        NameKeyUpgradeGenre hipHop = new(Guid.NewGuid(), "Hip-Hop", 3);
        NameKeyUpgradeGenre hiphop = new(Guid.NewGuid(), "hiphop", 7);
        NameKeyUpgradeGenre rock = new(Guid.NewGuid(), "Rock", 1);

        NameKeyUpgradePlan plan = NameKeyUpgradePlanner.Plan(Input(genres: [hipHop, hiphop, rock]));

        plan.GenreMerges.Should().BeEquivalentTo(new Dictionary<Guid, Guid> { [hipHop.Id] = hiphop.Id });
        plan.GenreKeys.Should().BeEquivalentTo(new Dictionary<Guid, string>
        {
            [hiphop.Id] = "hiphop",
            [rock.Id] = "rock",
        });
    }

    [Test]
    public void Plan_RulesNamingEntries_GetTheirIdsAndOthersKeepTheirText()
    {
        NameKeyUpgradeArtist artist = Artist("Beyoncé", days: 1);
        NameKeyUpgradeArtist joined = Artist("Beyonce", days: 2);
        NameKeyUpgradeAlbum album = Album("Lemonade", artist);
        NameKeyUpgradeAlbum greatestHits = Album("Greatest Hits", artist);
        NameKeyUpgradeAlbum otherHits = Album("Greatest Hits", joined);
        NameKeyUpgradeArtist other = Artist("Other");
        NameKeyUpgradeAlbum otherArtistHits = Album("Greatest Hits!", other);
        NameKeyUpgradeGenre genre = new(Guid.NewGuid(), "R&B", 4);
        SmartPlaylistRule[] rules =
        [
            Rule(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "beyonce"),
            Rule(SmartPlaylistField.Album, SmartPlaylistOperator.IsNot, "LEMONADE"),
            Rule(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "R and B"),
            Rule(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "r & b "),
            Rule(SmartPlaylistField.Album, SmartPlaylistOperator.Is, "Greatest Hits"),
            Rule(SmartPlaylistField.Artist, SmartPlaylistOperator.Contains, "Beyoncé"),
            Rule(SmartPlaylistField.Artist, SmartPlaylistOperator.Is, "Nobody"),
            new SmartPlaylistRule(SmartPlaylistField.Rating, SmartPlaylistOperator.AtLeast, Number: 4),
        ];
        NameKeyUpgradeSmartPlaylist playlist = new(Guid.NewGuid(), rules);
        NameKeyUpgradeSmartPlaylist unchanged = new(Guid.NewGuid(), [rules[6], rules[7]]);

        NameKeyUpgradePlan plan = NameKeyUpgradePlanner.Plan(Input(
            artists: [artist, joined, other],
            albums: [album, greatestHits, otherHits, otherArtistHits],
            genres: [genre],
            smartPlaylists: [playlist, unchanged]));

        plan.SmartPlaylistRules.Keys.Should().Equal(playlist.Id);
        plan.SmartPlaylistRules[playlist.Id].Select(rule => rule.ReferenceId).Should().Equal(
            artist.Id,
            album.Id,
            null,
            genre.Id,
            null,
            null,
            null,
            null);
        plan.SmartPlaylistRules[playlist.Id].Select(rule => rule.Text).Should().Equal(rules.Select(rule => rule.Text));
    }

    private static NameKeyUpgradeInput Input(
        IReadOnlyList<NameKeyUpgradeArtist>? artists = null,
        IReadOnlyList<NameKeyUpgradeAlbum>? albums = null,
        IReadOnlyList<NameKeyUpgradeGenre>? genres = null,
        IReadOnlyList<NameKeyUpgradeSmartPlaylist>? smartPlaylists = null)
    {
        return new NameKeyUpgradeInput(artists ?? [], albums ?? [], genres ?? [], smartPlaylists ?? []);
    }

    private static NameKeyUpgradeArtist Artist(string name, int days = 0)
    {
        return new NameKeyUpgradeArtist(Guid.NewGuid(), name, _start.AddDays(days));
    }

    private static NameKeyUpgradeAlbum Album(
        string title,
        NameKeyUpgradeArtist? artist,
        int days = 0,
        Guid? source = null,
        string? directory = null)
    {
        return new NameKeyUpgradeAlbum(
            Guid.NewGuid(),
            title,
            "old-" + Guid.NewGuid().ToString("N"),
            artist?.Id,
            _start.AddDays(days),
            source,
            directory);
    }

    private static SmartPlaylistRule Rule(SmartPlaylistField field, SmartPlaylistOperator @operator, string text)
    {
        return new SmartPlaylistRule(field, @operator, text);
    }
}
