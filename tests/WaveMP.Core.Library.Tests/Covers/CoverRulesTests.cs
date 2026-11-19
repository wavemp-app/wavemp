using AwesomeAssertions;
using WaveMP.Core.Library.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Library.Tests.Covers;

[TestFixture]
public class CoverRulesTests
{
    private static readonly Guid _albumId = Guid.NewGuid();
    private static readonly Guid _trackId = Guid.NewGuid();
    private static readonly Guid _otherTrackId = Guid.NewGuid();

    [TestCase(CoverPriority.AlbumFirst, null, null, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst, null, null, CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.TrackFirst, null, CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.TrackFirst, CoverPriority.TrackFirst, CoverPriority.AlbumFirst, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst, null, CoverPriority.AlbumFirst, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.AlbumFirst, CoverPriority.TrackFirst, CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.TrackFirst, CoverPriority.AlbumFirst, null, CoverPriority.AlbumFirst)]
    public void Effective_Always_TakesTheMostSpecificRule(
        CoverPriority global,
        CoverPriority? album,
        CoverPriority? track,
        CoverPriority expected)
    {
        CoverRules.Effective(global, album, track).Should().Be(expected);
    }

    [TestCase(CoverPriority.AlbumFirst, null, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst, null, CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.TrackFirst, CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.TrackFirst, CoverPriority.AlbumFirst, CoverPriority.AlbumFirst)]
    public void ForAlbum_RuleOfTheAlbumOrNone_TakesItOtherwiseTheGlobalOne(
        CoverPriority global,
        CoverPriority? album,
        CoverPriority expected)
    {
        CoverCandidateRow[] candidates =
        [
            AlbumCover(album),
            TrackCover(_trackId, album, track: CoverPriority.AlbumFirst),
        ];

        CoverRules.ForAlbum(global, candidates).Should().Be(expected);
    }

    [Test]
    public void ForAlbum_RuleOnlyOnTheCoversOfItsTracks_StillFindsTheRuleOfTheAlbum()
    {
        // Albums without a picture in their folder carry their rule on the covers of their tracks.
        CoverRules.ForAlbum(CoverPriority.AlbumFirst, [TrackCover(_trackId, CoverPriority.TrackFirst, null)])
            .Should().Be(CoverPriority.TrackFirst);
    }

    [TestCase(CoverPriority.AlbumFirst, null, null, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.TrackFirst, null, CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.TrackFirst, CoverPriority.AlbumFirst, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst, null, CoverPriority.AlbumFirst, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.AlbumFirst, null, CoverPriority.TrackFirst, CoverPriority.TrackFirst)]
    public void ForTrack_AllLevels_TakesTheMostSpecificRule(
        CoverPriority global,
        CoverPriority? album,
        CoverPriority? track,
        CoverPriority expected)
    {
        CoverCandidateRow[] candidates =
        [
            AlbumCover(album),
            TrackCover(_trackId, album, track),
            TrackCover(_otherTrackId, album, track is null ? CoverPriority.TrackFirst : null),
        ];

        CoverRules.ForTrack(_trackId, global, candidates).Should().Be(expected);
    }

    [Test]
    public void ForTrack_RuleOfAnotherTrack_IsIgnored()
    {
        CoverCandidateRow[] candidates =
        [
            AlbumCover(null),
            TrackCover(_otherTrackId, null, CoverPriority.TrackFirst),
        ];

        CoverRules.ForTrack(_trackId, CoverPriority.AlbumFirst, candidates).Should().Be(CoverPriority.AlbumFirst);
    }

    [Test]
    public void AlbumRuleOfAndTrackRuleOf_WithoutCovers_AreNull()
    {
        CoverRules.AlbumRuleOf([]).Should().BeNull();
        CoverRules.TrackRuleOf(_trackId, []).Should().BeNull();
    }

    private static CoverCandidateRow AlbumCover(CoverPriority? album)
    {
        return new CoverCandidateRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _albumId,
            null,
            null,
            null,
            null,
            null,
            true,
            CoverSource.File,
            MediaSource.DefaultId,
            "Album/cover.jpg",
            "image/jpeg",
            100,
            DateTimeOffset.UnixEpoch,
            AlbumCoverPriority: album);
    }

    private static CoverCandidateRow TrackCover(Guid trackId, CoverPriority? album, CoverPriority? track)
    {
        return new CoverCandidateRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            trackId,
            _albumId,
            1,
            1,
            "Song",
            true,
            CoverSource.File,
            MediaSource.DefaultId,
            "Album/Song.jpg",
            "image/jpeg",
            100,
            DateTimeOffset.UnixEpoch,
            AlbumCoverPriority: album,
            TrackCoverPriority: track);
    }
}
