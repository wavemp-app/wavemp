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
