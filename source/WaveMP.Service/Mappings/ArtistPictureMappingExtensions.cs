using WaveMP.Core.ArtistPictures.Pictures;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class ArtistPictureMappingExtensions
{
    /// <summary>
    /// The picture of an artist; pictures that were not read (<c>null</c>) show nothing.
    /// </summary>
    public static ArtistPictureDto ToPictureDto(this ArtistPictureRow? picture)
    {
        var view = ArtistPictureView.Of(picture);
        return new ArtistPictureDto
        {
            Kind = view.Kind.ToDto(),
            Version = view.Version,
            AlbumIds = view.AlbumIds,
            HasLocalPicture = picture?.LocalFileId is not null,
            HasUploadedPicture = picture?.UploadedPictureId is not null,
            HasOnlinePicture = picture?.OnlinePicture is not null,
            OnlineCredit = picture?.OnlinePicture is { } online
                ? new ArtistPictureCreditDto
                {
                    Provider = online.Provider.ToDto(),
                    Author = online.Author,
                    License = online.License,
                    LicenseUrl = online.LicenseUrl,
                    SourceUrl = online.SourceUrl,
                }
                : null,
            MusicBrainzArtistId = picture?.MusicBrainzArtistId,
        };
    }

    public static ArtistPictureKindDto ToDto(this ArtistPictureShownKind kind)
    {
        return kind switch
        {
            ArtistPictureShownKind.Local => ArtistPictureKindDto.Local,
            ArtistPictureShownKind.Uploaded => ArtistPictureKindDto.Uploaded,
            ArtistPictureShownKind.Online => ArtistPictureKindDto.Online,
            ArtistPictureShownKind.Albums => ArtistPictureKindDto.Albums,
            ArtistPictureShownKind.None => ArtistPictureKindDto.None,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind."),
        };
    }

    public static ArtistPictureProviderDto ToDto(this ArtistPictureProvider provider)
    {
        return provider switch
        {
            ArtistPictureProvider.Wikimedia => ArtistPictureProviderDto.Wikimedia,
            ArtistPictureProvider.FanartTv => ArtistPictureProviderDto.FanartTv,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown provider."),
        };
    }
}
