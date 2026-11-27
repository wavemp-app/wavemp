using AwesomeAssertions;
using WaveMP.Core.Lyrics.Downloads;

namespace WaveMP.Core.Lyrics.Tests.Downloads;

[TestFixture]
public class LrclibMatcherTests
{
    private static readonly LrclibQuery _query = new("Song", "Band", "Album", TimeSpan.FromSeconds(200));

    [Test]
    public void Choose_SyncedAndPlainLyrics_PrefersTheSyncedOnes()
    {
        LrclibRecord plain = Record(1, plain: "Plain", album: "Album");
        LrclibRecord synced = Record(2, synced: "[00:01.00]Synced", album: "Other");

        LrclibMatcher.Choose([plain, synced], _query, out bool instrumental).Should().Be(synced);
        instrumental.Should().BeFalse();
    }

    [Test]
    public void Choose_EquallyGoodRecords_PrefersTheSameAlbumAndThenTheClosestDuration()
    {
        LrclibRecord otherAlbum = Record(1, synced: "A", album: "Live", duration: 200);
        LrclibRecord farther = Record(2, synced: "B", album: "album", duration: 202);
        LrclibRecord closer = Record(3, synced: "C", album: "ALBUM ", duration: 199.5);

        LrclibMatcher.Choose([otherAlbum, farther, closer], _query, out _).Should().Be(closer);
    }

    [Test]
    public void Choose_DurationOutsideTheTolerance_IsNotChosen()
    {
        LrclibMatcher.Choose([Record(1, synced: "A", duration: 210)], _query, out bool instrumental)
            .Should().BeNull();
        instrumental.Should().BeFalse();
    }

    [Test]
    public void Choose_OnlyInstrumentalRecords_ReportsInstrumental()
    {
        LrclibRecord record = Record(1) with { Instrumental = true };

        LrclibMatcher.Choose([record], _query, out bool instrumental).Should().BeNull();
        instrumental.Should().BeTrue();
    }

    [Test]
    public void Choose_QueryWithoutDuration_AcceptsAnyDuration()
    {
        LrclibQuery query = _query with { Duration = null };
        LrclibRecord record = Record(1, plain: "Plain", duration: 30);

        LrclibMatcher.Choose([record], query, out _).Should().Be(record);
    }

    [Test]
    public void Choose_RecordsWithoutLyrics_ChoosesNothing()
    {
        LrclibMatcher.Choose([Record(1, plain: "  ")], _query, out bool instrumental).Should().BeNull();
        instrumental.Should().BeFalse();
    }

    [Test]
    public void Choose_WithNames_PrefersTracksWithTheNamesOfTheTrack()
    {
        LrclibRecord other = Record(1, synced: "A") with { TrackName = "Other" };
        LrclibRecord named = Record(2, plain: "B");

        LrclibMatcher.Choose([other, named], _query, LyricsNames.Of(["Song"], ["Band"]), out _).Should().Be(named);
    }

    [Test]
    public void ChooseOtherVersion_RecordsOfAnotherDuration_TakesTheSameNamesAndTheClosestDuration()
    {
        LrclibRecord farther = Record(1, synced: "A", album: "Other", duration: 300);
        LrclibRecord closer = Record(2, synced: "B", album: "Other", duration: 250);
        LrclibRecord otherSong = Record(3, synced: "C", duration: 240) with { TrackName = "Other" };
        LrclibRecord instrumental = Record(4, duration: 240) with { Instrumental = true };

        LrclibMatcher.ChooseOtherVersion(
                [farther, closer, otherSong, instrumental],
                _query,
                LyricsNames.Of(["Song"], ["Band"]))
            .Should().Be(closer);
    }

    [Test]
    public void ChooseOtherVersion_NoTrackWithTheNames_IsNull()
    {
        LrclibMatcher.ChooseOtherVersion([Record(1, plain: "A", duration: 400)], _query, LyricsNames.Of(["X"], ["Y"]))
            .Should().BeNull();
    }

    [Test]
    public void LyricsOf_OtherVersion_KeepsOnlyThePlainLines()
    {
        LrclibRecord record = Record(1, synced: "[00:01.00]One\n[00:02.00]\n[00:03.00]Two", plain: "One\n\nTwo");

        LrclibMatcher.LyricsOf(record, otherVersion: false).Should().StartWith("[00:01.00]");
        LrclibMatcher.LyricsOf(record, otherVersion: true).Should().Be("One\n\nTwo");
        LrclibMatcher.LyricsOf(record with { PlainLyrics = null }, otherVersion: true).Should().Be("One\n\nTwo");
        LrclibMatcher.LyricsOf(Record(2), otherVersion: true).Should().BeNull();
    }

    [Test]
    public void FitsDuration_ThreeSecondsApartOrUnknown_Fits()
    {
        LrclibMatcher.FitsDuration(Record(1, duration: 203), TimeSpan.FromSeconds(200)).Should().BeTrue();
        LrclibMatcher.FitsDuration(Record(1, duration: 203.5), TimeSpan.FromSeconds(200)).Should().BeFalse();
        LrclibMatcher.FitsDuration(Record(1), null).Should().BeTrue();
    }

    private static LrclibRecord Record(
        long id,
        string? synced = null,
        string? plain = null,
        string? album = "Album",
        double duration = 200)
    {
        return new LrclibRecord
        {
            Id = id,
            TrackName = "Song",
            ArtistName = "Band",
            AlbumName = album,
            Duration = duration,
            SyncedLyrics = synced,
            PlainLyrics = plain,
        };
    }
}
