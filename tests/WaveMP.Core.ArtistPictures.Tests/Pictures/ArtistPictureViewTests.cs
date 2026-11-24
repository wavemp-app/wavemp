using AwesomeAssertions;
using WaveMP.Core.ArtistPictures.Pictures;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.ArtistPictures.Tests.Pictures;

[TestFixture]
public class ArtistPictureViewTests
{
    private static readonly Guid _fileId = Guid.Parse("0196a0a0-0000-7000-8000-000000000001");
    private static readonly Guid _uploadedId = Guid.Parse("0196a0a0-0000-7000-8000-000000000002");
    private static readonly Guid _onlineId = Guid.Parse("0196a0a0-0000-7000-8000-000000000003");
    private static readonly DateTimeOffset _modifiedAt = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly ArtistOnlinePictureRow _online =
        new(_onlineId, ArtistPictureProvider.Wikimedia, "Jane Doe", "CC BY-SA 4.0", null, null);

    [Test]
    public void Of_AllSources_ShowsThePictureInTheArtistFolder()
    {
        ArtistPictureRow row = new(_fileId, _modifiedAt, _uploadedId, _online, null)
        {
            CollageAlbumIds = [Guid.NewGuid()],
        };

        var view = ArtistPictureView.Of(row);

        view.Should().BeEquivalentTo(new ArtistPictureView(
            ArtistPictureShownKind.Local,
            $"l-{_fileId:N}-{_modifiedAt.UtcTicks:x}",
            []));
    }

    [Test]
    public void Of_UploadedAndOnline_ShowsTheUploadedPicture()
    {
        var view = ArtistPictureView.Of(new ArtistPictureRow(null, null, _uploadedId, _online, null));

        view.Kind.Should().Be(ArtistPictureShownKind.Uploaded);
        view.Version.Should().Be($"u-{_uploadedId:N}");
        view.AlbumIds.Should().BeEmpty();
    }

    [Test]
    public void Of_OnlyOnline_ShowsTheOnlinePicture()
    {
        var view = ArtistPictureView.Of(new ArtistPictureRow(null, null, null, _online, null));

        view.Kind.Should().Be(ArtistPictureShownKind.Online);
        view.Version.Should().Be($"o-{_onlineId:N}");
    }

    [Test]
    public void Of_NoPictureButAlbums_ShowsTheCollage()
    {
        Guid[] albums = [Guid.NewGuid(), Guid.NewGuid()];

        var view = ArtistPictureView.Of(new ArtistPictureRow(null, null, null, null, null) { CollageAlbumIds = albums });

        view.Should().BeEquivalentTo(new ArtistPictureView(ArtistPictureShownKind.Albums, null, albums));
    }

    [Test]
    public void Of_NothingOrNotRead_ShowsNone()
    {
        ArtistPictureView.Of(new ArtistPictureRow(null, null, null, null, Guid.NewGuid()))
            .Should().Be(ArtistPictureView.None);
        ArtistPictureView.Of(null).Should().Be(ArtistPictureView.None);
    }

    [Test]
    public void Versions_DifferentPictures_AreDifferent()
    {
        string[] versions =
        [
            ArtistPictureVersions.Local(_fileId, _modifiedAt),
            ArtistPictureVersions.Local(_fileId, _modifiedAt.AddSeconds(1)),
            ArtistPictureVersions.Uploaded(_uploadedId),
            ArtistPictureVersions.Online(_uploadedId),
        ];

        versions.Should().OnlyHaveUniqueItems();
        versions.Should().AllSatisfy(version => version.Should().MatchRegex("^[luo]-[0-9a-f]{32}(-[0-9a-f]+)?$"));
    }
}
