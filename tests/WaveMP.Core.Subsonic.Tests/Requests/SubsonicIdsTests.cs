using AwesomeAssertions;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Subsonic.Tests.Requests;

[TestFixture]
public class SubsonicIdsTests
{
    private static readonly Guid _id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    [Test]
    public void FormatAndParse_RoundTrip()
    {
        SubsonicIds.Format(_id).Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");
        SubsonicIds.Parse(SubsonicIds.Format(_id)).Should().Be(_id);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("42")]
    [TestCase("al-0f8fad5b-d9cb-469f-a165-70867728950e")]
    public void Parse_NoId_ReturnsNull(string? value)
    {
        SubsonicIds.Parse(value).Should().BeNull();
    }

    [Test]
    public void ParseCover_KnowsTheOwnerByThePrefix()
    {
        SubsonicIds.ParseCover(SubsonicIds.AlbumCover(_id))
            .Should().Be(new SubsonicCoverId(SubsonicCoverOwner.Album, _id));
        SubsonicIds.ParseCover(SubsonicIds.TrackCover(_id))
            .Should().Be(new SubsonicCoverId(SubsonicCoverOwner.Track, _id));
        SubsonicIds.ParseCover(SubsonicIds.PlaylistCover(_id))
            .Should().Be(new SubsonicCoverId(SubsonicCoverOwner.Playlist, _id));
    }

    [Test]
    public void SmartPlaylist_CarriesThePrefixAndIsItsOwnCoverId()
    {
        string id = SubsonicIds.SmartPlaylist(_id);

        id.Should().Be("sp-0f8fad5b-d9cb-469f-a165-70867728950e");
        SubsonicIds.IsSmartPlaylist(id).Should().BeTrue();
        SubsonicIds.ParseSmartPlaylist(id).Should().Be(_id);
        SubsonicIds.ParseSmartPlaylist(" SP-0f8fad5b-d9cb-469f-a165-70867728950e ").Should().Be(_id);
        SubsonicIds.ParseCover(id).Should().Be(new SubsonicCoverId(SubsonicCoverOwner.SmartPlaylist, _id));
    }

    [Test]
    public void SmartPlaylist_IdsOfNormalPlaylistsAreNoSmartPlaylists()
    {
        // The prefix keeps the two kinds apart, so a plain id never reaches the smart playlists.
        SubsonicIds.IsSmartPlaylist(SubsonicIds.Format(_id)).Should().BeFalse();
        SubsonicIds.ParseSmartPlaylist(SubsonicIds.Format(_id)).Should().BeNull();
        SubsonicIds.Parse(SubsonicIds.SmartPlaylist(_id)).Should().BeNull();
    }

    [TestCase("sp-")]
    [TestCase("sp-nothing")]
    public void ParseSmartPlaylist_PrefixWithoutId_IsSmartButHasNoId(string value)
    {
        SubsonicIds.IsSmartPlaylist(value).Should().BeTrue();
        SubsonicIds.ParseSmartPlaylist(value).Should().BeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("pl-0f8fad5b-d9cb-469f-a165-70867728950e")]
    public void IsSmartPlaylist_OtherValues_IsFalse(string? value)
    {
        SubsonicIds.IsSmartPlaylist(value).Should().BeFalse();
    }

    [Test]
    public void ParseCover_PlainId_CanBeAnyOwner()
    {
        // Some apps ask for the cover with the id of the song or album.
        SubsonicIds.ParseCover(SubsonicIds.Format(_id)).Should().Be(new SubsonicCoverId(SubsonicCoverOwner.Any, _id));
    }

    [TestCase("xx-0f8fad5b-d9cb-469f-a165-70867728950e")]
    [TestCase("al-nothing")]
    [TestCase(null)]
    public void ParseCover_Invalid_ReturnsNull(string? value)
    {
        SubsonicIds.ParseCover(value).Should().BeNull();
    }

    [Test]
    public void MusicFolder_FirstSource_KeepsTheIdOfTheOneFolderOfEarlierVersions()
    {
        SubsonicIds.MusicFolder(MediaSource.DefaultId).Should().Be(SubsonicIds.DefaultMusicFolderId).And.Be(1);
    }

    [Test]
    public void MusicFolder_OtherSource_IsAFixedNumber()
    {
        // The id must stay the same across starts and versions, or apps lose the folder they remember.
        SubsonicIds.MusicFolder(_id).Should().Be(1303713389);
    }

    [Test]
    public void MusicFolder_OtherSources_GetDifferentPositiveIdsAboveTheFirstFolder()
    {
        Guid[] ids =
        [
            .. Enumerable.Range(2, 1000).Select(number => Guid.Parse($"019a0000-0000-7000-8000-{number:D12}")),
        ];

        int[] folders = [.. ids.Select(SubsonicIds.MusicFolder)];

        folders.Should().OnlyHaveUniqueItems().And.OnlyContain(folder => folder > SubsonicIds.DefaultMusicFolderId);
    }
}
