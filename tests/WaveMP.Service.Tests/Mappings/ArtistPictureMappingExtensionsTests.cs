using AwesomeAssertions;
using WaveMP.Core.ArtistPictures.Pictures;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class ArtistPictureMappingExtensionsTests
{
    private static readonly DateTimeOffset _time = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void ToPictureDto_UploadedAndHiddenOnlinePicture_ShowsTheUploadedOneWithTheOnlineCredit()
    {
        var uploadedId = Guid.NewGuid();
        var musicBrainzId = Guid.NewGuid();
        ArtistPictureRow row = new(
            null,
            null,
            uploadedId,
            new ArtistOnlinePictureRow(
                Guid.NewGuid(),
                ArtistPictureProvider.FanartTv,
                null,
                null,
                null,
                "https://fanart.tv/artist/" + musicBrainzId),
            musicBrainzId);

        ArtistPictureDto dto = row.ToPictureDto();

        dto.Should().BeEquivalentTo(new ArtistPictureDto
        {
            Kind = ArtistPictureKindDto.Uploaded,
            Version = $"u-{uploadedId:N}",
            AlbumIds = [],
            HasLocalPicture = false,
            HasUploadedPicture = true,
            HasOnlinePicture = true,
            OnlineCredit = new ArtistPictureCreditDto
            {
                Provider = ArtistPictureProviderDto.FanartTv,
                SourceUrl = "https://fanart.tv/artist/" + musicBrainzId,
            },
            MusicBrainzArtistId = musicBrainzId,
        });
    }

    [Test]
    public void ToPictureDto_Collage_NamesTheAlbums()
    {
        Guid[] albums = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        ArtistPictureRow row = new(null, null, null, null, null) { CollageAlbumIds = albums };

        ArtistPictureDto dto = row.ToPictureDto();

        dto.Kind.Should().Be(ArtistPictureKindDto.Albums);
        dto.Version.Should().BeNull();
        dto.AlbumIds.Should().Equal(albums);
        dto.OnlineCredit.Should().BeNull();
    }

    [Test]
    public void ToDto_ArtistWithoutReadPictures_HasNone()
    {
        ArtistRow artist = new(Guid.NewGuid(), "Alpha Band", 1, 2, _time);

        ArtistDto dto = artist.ToDto();

        dto.Picture.Should().BeEquivalentTo(new
        {
            Kind = ArtistPictureKindDto.None,
            AlbumIds = Array.Empty<Guid>(),
            HasLocalPicture = false,
            HasUploadedPicture = false,
            HasOnlinePicture = false,
        });
    }

    [Test]
    public void ToDto_AllKindsAndProviders_AreMapped()
    {
        Enum.GetValues<ArtistPictureShownKind>().Select(kind => kind.ToDto())
            .Should().OnlyHaveUniqueItems().And.HaveCount(Enum.GetValues<ArtistPictureKindDto>().Length);
        Enum.GetValues<ArtistPictureProvider>().Select(provider => provider.ToDto())
            .Should().OnlyHaveUniqueItems().And.HaveCount(Enum.GetValues<ArtistPictureProviderDto>().Length);
    }
}
