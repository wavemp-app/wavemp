using AwesomeAssertions;
using WaveMP.Core.Scanning.Groupings;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Tests.Groupings;

[TestFixture]
public class ArtistPictureCandidatesTests
{
    private static readonly IReadOnlyList<string> _names = ["artist", "folder", "photo"];

    private static readonly Guid _artist = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid _other = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid _source = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid _secondSource = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    [Test]
    public void Select_PictureInTheFolderNamedAfterTheArtist_IsFoundAboveTheAlbumFolders()
    {
        ArtistPictureImage picture = Image("Weezer/artist.jpg");

        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, "Weezer", "Weezer/Blue Album"), Folder(_artist, "Weezer", "Weezer/Pinkerton/CD1")],
            [picture, Image("Weezer/Blue Album/folder.jpg"), Image("Weezer/Pinkerton/photo.png")],
            _names);

        result.Should().ContainKey(_artist).WhoseValue.Should().Equal(picture);
    }

    [Test]
    public void Select_FolderAndArtistOfTheSameName_UsesTheTopmostFolder()
    {
        // Weezer/Weezer/... : the outer folder is the folder of the artist, the inner one an album.
        ArtistPictureImage outer = Image("Music/Weezer/photo.jpg");
        ArtistPictureImage inner = Image("Music/Weezer/Weezer/artist.jpg");

        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, "Weezer", "Music/Weezer/Weezer")],
            [inner, outer],
            _names);

        result[_artist].Should().Equal(outer);
    }

    [TestCase("AC/DC", "AC_DC")]
    [TestCase("AC/DC", "ACDC")]
    [TestCase("ac/dc", "AC-DC")]
    [TestCase("Sigur Rós", "SIGUR RÓS")]
    [TestCase("Guns N' Roses", "Guns N Roses")]
    public void Select_FolderNameWithOtherSeparatorsOrCase_MatchesTheArtist(string artist, string folder)
    {
        ArtistPictureImage picture = Image($"{folder}/folder.jpg");

        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, artist, $"{folder}/Album")],
            [picture],
            _names);

        result[_artist].Should().Equal(picture);
    }

    [Test]
    public void Select_FoldersNotNamedAfterTheArtist_AreNeverUsed()
    {
        // A sampler in a folder of compilations, an album folder with a cover named folder.jpg, and a flat folder of
        // another name: none of them is the folder of the artist.
        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [
                Folder(_artist, "Various Artists", "Compilations/Hits 2020"),
                Folder(_other, "Solo", "Singles"),
            ],
            [
                Image("Compilations/folder.jpg"),
                Image("Compilations/Hits 2020/folder.jpg"),
                Image("Singles/artist.jpg"),
            ],
            _names);

        result.Should().BeEmpty();
    }

    [TestCase("!!!")]
    [TestCase("...")]
    [TestCase("")]
    public void Select_ArtistNameWithoutLettersOrDigits_MatchesNoFolder(string name)
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, name, "___/Album")],
            [Image("___/artist.jpg"), Image("___/Album/artist.jpg")],
            _names);

        result.Should().BeEmpty();
    }

    [Test]
    public void Select_FlatArtistFolder_TakesThePictureNextToTheSongs()
    {
        ArtistPictureImage picture = Image("Solo/folder.jpg");

        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, "Solo", "Solo")],
            [picture],
            _names);

        result[_artist].Should().Equal(picture);
    }

    [Test]
    public void Select_SongsInTheRootOfTheSource_HaveNoArtistFolder()
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, "Solo", string.Empty)],
            [Image("artist.jpg")],
            _names);

        result.Should().BeEmpty();
    }

    [Test]
    public void Select_SeveralPictures_OrdersThemByNameThenQualityThenSourceAndPath()
    {
        ArtistPictureImage photo = Image("Band/photo.jpg", width: 3000, height: 3000);
        ArtistPictureImage smallArtist = Image("Band/artist.png", width: 300, height: 300);
        ArtistPictureImage largeArtist = Image("Band/artist.jpg", width: 1000, height: 1000);
        ArtistPictureImage folder = Image("Band/folder.jpg", width: 1000, height: 1000);
        ArtistPictureImage sameInSecond = Image("Band/artist.webp", width: 1000, height: 1000, source: _secondSource);
        ArtistPictureImage sameByPath = Image("Band/artist.webp", width: 1000, height: 1000);

        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, "Band", "Band/Album"), Folder(_artist, "Band", "Band/Live", _secondSource)],
            [photo, smallArtist, folder, sameInSecond, sameByPath, largeArtist],
            _names);

        result[_artist].Should().Equal(largeArtist, sameByPath, sameInSecond, smallArtist, folder, photo);
    }

    [Test]
    public void Select_PictureOfTheArtistFolderInAnotherSource_IsNotUsed()
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, "Band", "Band/Album")],
            [Image("Band/artist.jpg", source: _secondSource)],
            _names);

        result.Should().BeEmpty();
    }

    [Test]
    public void Select_NamesOfTheSettings_DecideWhichPicturesCountAndTheirOrder()
    {
        ArtistPictureImage band = Image("Band/Band.jpg");
        ArtistPictureImage artist = Image("Band/artist.jpg");

        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, "Band", "Band/Album")],
            [artist, band, Image("Band/photo.jpg")],
            ["BAND", "artist"]);

        result[_artist].Should().Equal(band, artist);
    }

    [Test]
    public void Select_TrackAndAlbumArtistOfTheSameFolder_EachFindTheirOwnFolder()
    {
        // The album artist "Band" in Band/Album, the track artist "Guest" of a song in the same folder.
        ArtistPictureImage bandPicture = Image("Band/artist.jpg");

        IReadOnlyDictionary<Guid, IReadOnlyList<ArtistPictureImage>> result = ArtistPictureCandidates.Select(
            [Folder(_artist, "Band", "Band/Album"), Folder(_other, "Guest", "Band/Album")],
            [bandPicture],
            _names);

        result.Keys.Should().Equal(_artist);
        result[_artist].Should().Equal(bandPicture);
    }

    [TestCase("AC/DC", "ACDC")]
    [TestCase("  Sigur  Rós ", "SIGURRÓS")]
    [TestCase("!!!", "")]
    public void NameKey_Name_KeepsOnlyLettersAndDigitsInUpperCase(string name, string key)
    {
        ArtistPictureCandidates.NameKey(name).Should().Be(key);
    }

    private static ArtistFolder Folder(Guid artistId, string name, string directory, Guid? source = null)
    {
        return new ArtistFolder(artistId, name, source ?? _source, directory);
    }

    private static ArtistPictureImage Image(
        string relativePath,
        int width = 500,
        int height = 500,
        long size = 1000,
        Guid? source = null)
    {
        int slash = relativePath.LastIndexOf('/');
        string directory = slash < 0 ? string.Empty : relativePath[..slash];
        string fileName = relativePath[(slash + 1)..];
        string baseName = fileName[..fileName.LastIndexOf('.')];
        return new ArtistPictureImage(
            Guid.NewGuid(),
            source ?? _source,
            relativePath,
            directory,
            FileGrouper.NormalizeBaseName(baseName),
            size,
            width,
            height);
    }
}
