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
    private static readonly string[] _names = ["cover", "folder"];

    [Test]
    public void ForAlbum_DiscFolders_IsTheFolderAbove()
    {
        var target = CoverTarget.ForAlbum(
            _albumId,
            [Track("Band/Album/CD1"), Track("Band/Album/CD2"), Track("Band/Album/CD2")],
            _names);

        target.Should().Be(new CoverTarget(_albumId, null, _sourceId, "Band/Album", "cover"));
    }

    [Test]
    public void ForAlbum_SongsInSeveralFolders_IsTheFolderWithMostSongs()
    {
        var target = CoverTarget.ForAlbum(
            _albumId,
            [Track("Mix"), Track("Band/Album"), Track("Band/Album")],
            _names);

        target.DirectoryPath.Should().Be("Band/Album");
    }

    [Test]
    public void ForAlbum_NoCoverNames_UsesCover()
    {
        CoverTarget.ForAlbum(_albumId, [Track("")], []).BaseName.Should().Be(CoverTarget.DefaultAlbumCoverName);
    }

    [TestCase(CoverPriority.AlbumFirst, null, null, true)]
    [TestCase(CoverPriority.TrackFirst, null, null, false)]
    [TestCase(CoverPriority.TrackFirst, CoverPriority.AlbumFirst, null, true)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.AlbumFirst, CoverPriority.TrackFirst, false)]
    public void ForTrack_FollowsTheEffectiveCoverRule(
        CoverPriority global,
        CoverPriority? album,
        CoverPriority? track,
        bool albumCover)
    {
        var trackId = Guid.NewGuid();
        OnlineTrackRow row = new(trackId, _sourceId, "Band/Album", "01 Song", _albumId, null, track, album);

        var target = CoverTarget.ForTrack(row, global, [Track("Band/Album")], _names);

        target.Should().Be(albumCover
            ? new CoverTarget(_albumId, null, _sourceId, "Band/Album", "cover")
            : new CoverTarget(null, trackId, _sourceId, "Band/Album", "01 Song"));
    }

    [Test]
    public void ForTrack_WithoutAlbum_IsTheCoverOfTheSong()
    {
        var trackId = Guid.NewGuid();
        OnlineTrackRow row = new(trackId, _sourceId, "", "Song", null, null, null, null);

        var target = CoverTarget.ForTrack(row, CoverPriority.AlbumFirst, [], _names);

        target.Should().Be(new CoverTarget(null, trackId, _sourceId, "", "Song"));
    }

    private static OnlineAlbumTrackRow Track(string directory)
    {
        return new OnlineAlbumTrackRow(Guid.NewGuid(), _sourceId, directory, null);
    }
}
