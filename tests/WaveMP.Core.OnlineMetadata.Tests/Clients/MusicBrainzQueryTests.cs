using AwesomeAssertions;
using WaveMP.Core.OnlineMetadata.Clients;

namespace WaveMP.Core.OnlineMetadata.Tests.Clients;

[TestFixture]
public class MusicBrainzQueryTests
{
    [Test]
    public void Recording_AllValues_RequiresTitleAndArtistAndPrefersAlbumAndDuration()
    {
        string query = MusicBrainzQuery.Recording("Song", "Band", "Album", TimeSpan.FromSeconds(200));

        query.Should().Be(
            "recording:\"Song\" AND artist:\"Band\" "
                + "AND (release:\"Album\" OR dur:[195000 TO 205000] OR recording:\"Song\")");
    }

    [Test]
    public void Recording_TitleOnly_SearchesTheTitle()
    {
        MusicBrainzQuery.Recording("Song", null, " ", null).Should().Be("recording:\"Song\"");
    }

    [Test]
    public void Release_WithTrackCount_PrefersTheCount()
    {
        string query = MusicBrainzQuery.Release("Album", "Band", 12);

        query.Should().Be("release:\"Album\" AND artist:\"Band\" AND (tracks:12 OR release:\"Album\")");
    }

    [Test]
    public void Phrase_QuotesAndBackslashes_AreEscaped()
    {
        MusicBrainzQuery.Phrase(" Say \"Hi\" \\o/ ").Should().Be("\"Say \\\"Hi\\\" \\\\o/\"");
    }
}
