using AwesomeAssertions;
using WaveMP.Core.Metadata.Artists;
using WaveMP.Core.Metadata.Resolutions;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Tests.Resolutions;

[TestFixture]
public class AlbumResolverTests
{
    [Test]
    public void Resolve_SameArtistAndAlbumInOtherSpelling_IsOneAlbum()
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve(
        [
            Track("Band", album: "Album", year: 2001),
            Track(" band ", album: "ALBUM "),
        ]);

        result.Should().AllSatisfy(album => album.Should().Be(new AlbumAssignment(
            "Album",
            [new ArtistName("Band")],
            2001,
            AlbumKeys.Create("Album", "Band"))));
    }

    [Test]
    public void Resolve_SeveralArtistsWithoutAlbumArtist_AreSeparateAlbums()
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve(
        [
            Track("A", album: "Hits"),
            Track("B", album: "Hits"),
        ]);

        result[0]!.AlbumArtist.Should().Be("A");
        result[1]!.AlbumArtist.Should().Be("B");
        result[0]!.Key.Should().NotBe(result[1]!.Key);
    }

    [Test]
    public void Resolve_AlbumArtistTag_KeepsACompilationTogether()
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve(
        [
            Track("A", "Various Artists", "Hits"),
            Track("B feat. C", "Various Artists", "Hits"),
        ]);

        result.Should().AllSatisfy(album => album.Should().Be(new AlbumAssignment(
            "Hits",
            [new ArtistName("Various Artists")],
            null,
            AlbumKeys.Create("Hits", "Various Artists"))));
    }

    [Test]
    public void Resolve_AlbumArtistAndArtist_AreTheSameAlbumWhenTheyNameTheSameArtist()
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve(
        [
            Track("K.Flay", album: "2 Weak"),
            Track("Guest", "k.flay", "2 weak"),
            Track("Other", album: "2 Weak"),
        ]);

        result[1]!.Key.Should().Be(result[0]!.Key);
        result[2]!.Key.Should().NotBe(result[0]!.Key);
    }

    [Test]
    public void Resolve_TracksWithoutArtist_AreKeptApartPerFolder()
    {
        IReadOnlyList<AlbumAssignment?> first = AlbumResolver.Resolve([Track(null, album: "Hits")], "a/One");
        IReadOnlyList<AlbumAssignment?> second = AlbumResolver.Resolve([Track(null, album: "Hits")], "a/Two");
        IReadOnlyList<AlbumAssignment?> again = AlbumResolver.Resolve([Track(null, album: "HITS")], "a/One");

        first[0].Should().Be(new AlbumAssignment("Hits", [], null, AlbumKeys.Create("Hits", null, "a/One")));
        second[0]!.Key.Should().NotBe(first[0]!.Key);
        again[0]!.Key.Should().Be(first[0]!.Key);
    }

    [Test]
    public void Resolve_TracksWithAnArtist_IgnoreTheFolder()
    {
        IReadOnlyList<AlbumAssignment?> first = AlbumResolver.Resolve([Track("Band", album: "Album")], "a/One");
        IReadOnlyList<AlbumAssignment?> second = AlbumResolver.Resolve([Track("Band", album: "Album")], "b/Two");

        second[0]!.Key.Should().Be(first[0]!.Key);
    }

    [Test]
    public void Resolve_CompilationFlagWithoutAlbumArtist_IsOneAlbumOfTheFolder()
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve(
        [
            Track("A", album: "Hits", isCompilation: true),
            Track("B", album: " hits ", isCompilation: true),
            Track(null, album: "Hits", isCompilation: true),
            Track(null, album: "Hits"),
        ],
        "a/Hits");

        result.Should().AllSatisfy(album => album.Should().Be(new AlbumAssignment(
            "Hits",
            [],
            null,
            AlbumKeys.Create("Hits", null, "a/Hits"))));
    }

    [Test]
    public void Resolve_CompilationFlagInAnotherFolderOrWithAnotherTitle_IsAnotherAlbum()
    {
        IReadOnlyList<AlbumAssignment?> first = AlbumResolver.Resolve(
        [
            Track("A", album: "Hits", isCompilation: true),
            Track("A", album: "More Hits", isCompilation: true),
        ],
        "a/One");
        IReadOnlyList<AlbumAssignment?> second = AlbumResolver.Resolve(
            [Track("A", album: "Hits", isCompilation: true)],
            "a/Two");

        first[1]!.Key.Should().NotBe(first[0]!.Key);
        second[0]!.Key.Should().NotBe(first[0]!.Key);
    }

    [Test]
    public void Resolve_CompilationFlagWithAlbumArtist_IgnoresTheFolder()
    {
        ResolvedTrackMetadata[] tracks = [Track("A", "DJ", "Mix", isCompilation: true)];

        IReadOnlyList<AlbumAssignment?> first = AlbumResolver.Resolve(tracks, "a/One");
        IReadOnlyList<AlbumAssignment?> second = AlbumResolver.Resolve(tracks, "b/Two");

        first[0].Should().Be(new AlbumAssignment("Mix", [new ArtistName("DJ")], null, AlbumKeys.Create("Mix", "DJ")));
        second[0].Should().Be(first[0]);
    }

    [Test]
    public void Resolve_TracksWithoutFlagNextToFlaggedOnes_KeepTheKeyOfTheirArtist()
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve(
        [
            Track("A", album: "Hits", isCompilation: true),
            Track("A", album: "Hits"),
            Track("B", album: "Hits"),
            Track("C", "Various Artists", "Hits"),
        ],
        "a/Hits");

        result.Select(album => album!.Key).Should().Equal(
            AlbumKeys.Create("Hits", null, "a/Hits"),
            AlbumKeys.Create("Hits", "A"),
            AlbumKeys.Create("Hits", "B"),
            AlbumKeys.Create("Hits", "Various Artists"));
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Resolve_CompilationFlagWithBlankAlbumArtist_CountsAsWithoutAlbumArtist(string albumArtist)
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve(
            [Track("A", albumArtist, "Hits", isCompilation: true)],
            "a/Hits");

        result[0].Should().Be(new AlbumAssignment("Hits", [], null, AlbumKeys.Create("Hits", null, "a/Hits")));
    }

    [Test]
    public void Resolve_CompilationFlagWithoutAlbum_GetsNoAlbum()
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve([Track("A", isCompilation: true)], "a/Hits");

        result.Should().AllSatisfy(album => album.Should().BeNull());
    }

    [Test]
    public void Resolve_TrackWithoutAlbum_GetsNoAlbum()
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve([Track("A"), Track("A", album: "  ")]);

        result.Should().AllSatisfy(album => album.Should().BeNull());
    }

    [Test]
    public void Resolve_TwoAlbumsInOneFolder_ResolvesThemSeparately()
    {
        IReadOnlyList<AlbumAssignment?> result = AlbumResolver.Resolve(
        [
            Track("A", album: "First"),
            Track("A", album: "Second"),
        ]);

        (result[0]!.Title, result[1]!.Title).Should().Be(("First", "Second"));
        result[0]!.Key.Should().NotBe(result[1]!.Key);
    }

    [Test]
    public void AlbumKeys_SameNamesInOtherSpelling_HaveTheSameKey()
    {
        string key = AlbumKeys.Create(" Abbey  Road", "The Beatles");

        key.Should().Be(AlbumKeys.Create("ABBEY ROAD", "the beatles "));
        key.Should().HaveLength(64);
        key.Should().Be(AlbumKeys.Create("Abbey Road", "The Beatles", "any/folder"));
        key.Should().NotBe(AlbumKeys.Create("Abbey Road", null));
        key.Should().NotBe(AlbumKeys.Create("Abbey Road", "The Beatle"));
    }

    private static ResolvedTrackMetadata Track(
        string? artist,
        string? albumArtist = null,
        string? album = null,
        int? year = null,
        bool isCompilation = false)
    {
        return new ResolvedTrackMetadata(
            "Title",
            TrackMetadataSource.Tags,
            artist,
            albumArtist,
            album,
            null,
            null,
            year,
            null,
            isCompilation);
    }
}
