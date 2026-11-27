using AwesomeAssertions;
using WaveMP.Core.Lyrics.Downloads;

namespace WaveMP.Core.Lyrics.Tests.Downloads;

[TestFixture]
public class LyricsTitlesTests
{
    [TestCase("Diamond Hard", "Diamond Hard")]
    [TestCase("Song (Remastered 2011)", "Song")]
    [TestCase("Song (2011 Remaster)", "Song")]
    [TestCase("Song (Live)", "Song")]
    [TestCase("Song (Live at Wembley)", "Song")]
    [TestCase("Song - Radio Edit", "Song")]
    [TestCase("Song - Remastered 2009", "Song")]
    [TestCase("Song (Official Video)", "Song")]
    [TestCase("Song (Official Music Video)", "Song")]
    [TestCase("Song [HD]", "Song")]
    [TestCase("Song [Explicit] (Lyric Video)", "Song")]
    [TestCase("Song feat. Other", "Song")]
    [TestCase("Song (feat. Other) (Radio Edit)", "Song")]
    [TestCase("Song (ft. Other & Third)", "Song")]
    [TestCase("Song featuring Other [Official Video]", "Song")]
    [TestCase("Song (Acoustic Remix)", "Song (Acoustic Remix)")]
    [TestCase("Hold On - Part 2", "Hold On - Part 2")]
    [TestCase("Video Killed the Radio Star", "Video Killed the Radio Star")]
    [TestCase("[Official Video]", "[Official Video]")]
    public void Clean_Title_RemovesTheAdditionsOfTheRecording(string title, string expected)
    {
        LyricsTitles.Clean(title).Should().Be(expected);
    }

    [TestCase("Kerli", "Kerli")]
    [TestCase("Bob Sinclar feat. K.Flay", "Bob Sinclar")]
    [TestCase("Simon & Garfunkel", "Simon")]
    [TestCase("A, B", "A")]
    [TestCase("AC/DC", "AC")]
    public void MainArtist_Name_IsTheFirstMainArtist(string artist, string expected)
    {
        LyricsTitles.MainArtist(artist).Should().Be(expected);
    }

    [Test]
    public void ArtistParts_NameOfSeveralArtists_NamesTheWholeTextAndEveryArtist()
    {
        LyricsTitles.ArtistParts("A & B feat. C").Should().Contain(["A & B feat. C", "A & B", "A", "B", "C"]);
        LyricsTitles.ArtistParts(null).Should().BeEmpty();
    }

    [TestCase("Kerli - Diamond Hard", "Kerli", "Diamond Hard")]
    [TestCase("01 - Kerli - Diamond Hard", "Kerli", "Diamond Hard")]
    [TestCase("Kerli - Diamond Hard - Live", "Kerli", "Diamond Hard - Live")]
    public void FromFileName_ArtistAndTitle_ReadsBoth(string baseName, string artist, string title)
    {
        LyricsTitles.FromFileName(baseName).Should().Be((artist, title));
    }

    [TestCase("Diamond Hard")]
    [TestCase("01 - Diamond Hard")]
    [TestCase("Kerli-Diamond Hard")]
    public void FromFileName_NoArtist_IsNull(string baseName)
    {
        LyricsTitles.FromFileName(baseName).Should().BeNull();
    }
}
