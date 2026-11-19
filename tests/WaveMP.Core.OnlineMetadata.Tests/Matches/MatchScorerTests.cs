using AwesomeAssertions;
using WaveMP.Core.OnlineMetadata.Matches;

namespace WaveMP.Core.OnlineMetadata.Tests.Matches;

[TestFixture]
public class MatchScorerTests
{
    private static readonly TrackLookup _song = new("Song", "Band", "Album", TimeSpan.FromSeconds(200));

    [Test]
    public void ForTrack_SameValues_Is100()
    {
        int agreement = MatchScorer.ForTrack(_song, Values("Song", "Band", "Album"), TimeSpan.FromSeconds(201));

        agreement.Should().Be(100);
    }

    [Test]
    public void ForTrack_OtherAlbum_LosesTheWeightOfTheAlbum()
    {
        int agreement = MatchScorer.ForTrack(_song, Values("Song", "Band", "Zzzzz"), TimeSpan.FromSeconds(200));

        agreement.Should().Be(85);
    }

    [Test]
    public void ForTrack_UnknownRemoteDuration_LosesTheWeightOfTheDuration()
    {
        int agreement = MatchScorer.ForTrack(_song, Values("Song", "Band", "Album"), null);

        agreement.Should().Be(80);
    }

    [Test]
    public void ForTrack_UnknownLocalValues_CountOnlyTheTitle()
    {
        int agreement = MatchScorer.ForTrack(
            new TrackLookup("Song", null, null, null),
            Values("Song", "Other", "Other"),
            null);

        agreement.Should().Be(100);
    }

    [Test]
    public void ForTrack_BetterMatch_ScoresHigher()
    {
        int good = MatchScorer.ForTrack(_song, Values("Song (Remastered)", "Band", "Album"), TimeSpan.FromSeconds(203));
        int bad = MatchScorer.ForTrack(_song, Values("Another Song", "Other Band", "Live"), TimeSpan.FromSeconds(320));

        good.Should().BeGreaterThan(bad);
    }

    [Test]
    public void ForAlbum_SameValues_Is100()
    {
        int agreement = MatchScorer.ForAlbum(
            new AlbumLookup("Album", "Band", 12),
            new OnlineAlbumValues("Album", "Band", 1999, null),
            12);

        agreement.Should().Be(100);
    }

    [Test]
    public void ForAlbum_OtherTrackCount_ScoresLower()
    {
        int agreement = MatchScorer.ForAlbum(
            new AlbumLookup("Album", "Band", 12),
            new OnlineAlbumValues("Album", "Band", 1999, null),
            24);

        agreement.Should().Be(90);
    }

    [TestCase("Beyoncé & Jay-Z", "beyonce and jay z", 1.0)]
    [TestCase("  The   Song!! ", "the song", 1.0)]
    [TestCase("abcd", "abce", 0.75)]
    [TestCase("", "abc", 0.0)]
    [TestCase(null, null, 0.0)]
    public void TextSimilarity_IgnoresCaseAccentsAndPunctuation(string? left, string? right, double expected)
    {
        MatchScorer.TextSimilarity(left, right).Should().BeApproximately(expected, 0.0001);
    }

    [TestCase(0, 1.0)]
    [TestCase(2, 1.0)]
    [TestCase(8.5, 0.5)]
    [TestCase(15, 0.0)]
    [TestCase(60, 0.0)]
    public void DurationSimilarity_FallsLinearlyFromTwoToFifteenSeconds(double differenceSeconds, double expected)
    {
        double similarity = MatchScorer.DurationSimilarity(
            TimeSpan.FromSeconds(200),
            TimeSpan.FromSeconds(200 + differenceSeconds));

        similarity.Should().BeApproximately(expected, 0.0001);
    }

    private static OnlineTrackValues Values(string title, string artist, string album)
    {
        return new OnlineTrackValues(title, artist, album, artist, 1999, 1, 1, null);
    }
}
