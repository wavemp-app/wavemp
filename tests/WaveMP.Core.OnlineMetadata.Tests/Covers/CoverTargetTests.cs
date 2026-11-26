using AwesomeAssertions;
using WaveMP.Core.OnlineMetadata.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.OnlineMetadata.Tests.Covers;

[TestFixture]
public class CoverTargetTests
{
    private static readonly Guid _sourceId = Guid.NewGuid();
    private static readonly Guid _albumId = Guid.NewGuid();

    [Test]
    public void ForAlbum_Always_IsTheAlbumWithoutAFolder()
    {
        var target = CoverTarget.ForAlbum(_albumId);

        // The cover of an album is kept by the id of the album, never as a picture in a folder it may share.
        target.Should().Be(new CoverTarget(_albumId, null, Guid.Empty, "", ""));
    }

    [TestCase(CoverPriority.AlbumFirst, null, null, true)]
    [TestCase(CoverPriority.TrackFirst, null, null, false)]
    [TestCase(CoverPriority.TrackOnly, null, null, false)]
    [TestCase(CoverPriority.TrackFirst, CoverPriority.AlbumFirst, null, true)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.AlbumFirst, CoverPriority.TrackFirst, false)]
    [TestCase(CoverPriority.AlbumFirst, null, CoverPriority.TrackOnly, false)]
    public void ForTrack_FollowsTheEffectiveCoverRule(
        CoverPriority global,
        CoverPriority? album,
        CoverPriority? track,
        bool albumCover)
    {
        var trackId = Guid.NewGuid();
        OnlineTrackRow row = new(trackId, _sourceId, "Band/Album", "01 Song", _albumId, null, track, album);

        var target = CoverTarget.ForTrack(row, global);

        target.Should().Be(albumCover
            ? CoverTarget.ForAlbum(_albumId)
            : new CoverTarget(null, trackId, _sourceId, "Band/Album", "01 Song"));
    }

    [Test]
    public void ForTrack_WithoutAlbum_IsTheCoverOfTheSong()
    {
        var trackId = Guid.NewGuid();
        OnlineTrackRow row = new(trackId, _sourceId, "", "Song", null, null, null, null);

        var target = CoverTarget.ForTrack(row, CoverPriority.AlbumFirst);

        target.Should().Be(new CoverTarget(null, trackId, _sourceId, "", "Song"));
    }
}
