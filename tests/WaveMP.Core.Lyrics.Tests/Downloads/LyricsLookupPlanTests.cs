using AwesomeAssertions;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Lyrics.Tests.Downloads;

[TestFixture]
public class LyricsLookupPlanTests
{
    private static readonly TimeSpan _duration = TimeSpan.FromSeconds(200);

    [Test]
    public void Of_TrackWithAdditions_SearchesAlsoWithoutThemAndWithTheMainArtist()
    {
        var plan = LyricsLookupPlan.Of(Row("Song (Official Video)", "Band & Friend"));

        plan.Query.Should().Be(new LrclibQuery("Song (Official Video)", "Band & Friend", "Album", _duration));
        plan.SearchQueries.Should().Equal(
            new LrclibQuery("Song (Official Video)", "Band & Friend", "Album", _duration),
            new LrclibQuery("Song", "Band", "Album", _duration));
        plan.Text.Should().Be("Band Song");
    }

    [Test]
    public void Of_TrackWithoutAdditions_SearchesOnce()
    {
        LyricsLookupPlan.Of(Row("Song", "Band")).SearchQueries.Should().ContainSingle();
    }

    [Test]
    public void Of_TrackWithoutArtist_TakesArtistAndTitleFromTheFileName()
    {
        var plan = LyricsLookupPlan.Of(
            Row("Kerli - Diamond Hard", artist: null, baseName: "Kerli - Diamond Hard"));

        plan.Title.Should().Be("Diamond Hard");
        plan.Artist.Should().Be("Kerli");
        plan.Names.Matches(Record("Diamond Hard", "Kerli")).Should().BeTrue();
    }

    [Test]
    public void Of_TrackWithoutArtistButWithATitleTag_KeepsTheTitle()
    {
        var plan = LyricsLookupPlan.Of(Row("Tagged", artist: null, baseName: "Kerli - Diamond Hard"));

        plan.Title.Should().Be("Tagged");
        plan.Artist.Should().Be("Kerli");
    }

    [Test]
    public void Of_TrackWithoutArtistAndPlainFileName_SearchesByTitleOnly()
    {
        var plan = LyricsLookupPlan.Of(Row("Song", artist: null, baseName: "Song"));

        plan.Artist.Should().BeNull();
        plan.Text.Should().Be("Song");
        plan.Names.HasArtists.Should().BeFalse();
        plan.Names.Matches(Record("Song", "Anyone")).Should().BeTrue();
    }

    [Test]
    public void Names_TrackOfSeveralArtists_MatchRecordsOfOneOfThem()
    {
        LyricsTrackRow row = Row("Song", "Band") with
        {
            Artists = [new LyricsArtistRow("Band", ArtistRole.Main), new LyricsArtistRow("Guest", ArtistRole.Featured)],
        };
        LyricsNames names = LyricsLookupPlan.Of(row).Names;

        names.Matches(Record("Song", "Band")).Should().BeTrue();
        names.Matches(Record("SONG (Remastered)", "Guest, Other")).Should().BeTrue();
        names.Matches(Record("Song", "Band feat. Guest")).Should().BeTrue();
        names.Matches(Record("Song", "Someone Else")).Should().BeFalse();
        names.Matches(Record("Other Song", "Band")).Should().BeFalse();
        names.Matches(new LrclibRecord { Id = 1 }).Should().BeFalse();
    }

    private static LyricsTrackRow Row(string title, string? artist, string baseName = "File")
    {
        return new LyricsTrackRow(
            Guid.NewGuid(),
            MediaSource.DefaultId,
            "Folder",
            baseName,
            title,
            artist,
            "Album",
            _duration,
            null,
            $"Folder/{baseName}.mp3",
            null,
            null)
        {
            Artists = artist is null ? [] : [new LyricsArtistRow(artist, ArtistRole.Main)],
        };
    }

    private static LrclibRecord Record(string title, string artist)
    {
        return new LrclibRecord { Id = 1, TrackName = title, ArtistName = artist, PlainLyrics = "Line" };
    }
}
