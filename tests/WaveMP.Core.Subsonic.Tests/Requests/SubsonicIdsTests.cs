using AwesomeAssertions;
using WaveMP.Core.Subsonic.Requests;

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
}
