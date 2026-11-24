using System.Globalization;
using Microsoft.Extensions.Logging;
using WaveMP.Core.ArtistPictures.Stores;
using WaveMP.Core.Caching.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.ArtistPictures.Pictures;

internal sealed class ArtistPictureService(
    IArtistPictureRepository pictures,
    IArtistPictureStore store,
    ICoverImageCache covers,
    TimeProvider timeProvider,
    ILogger<ArtistPictureService> logger) : IArtistPictureService
{
    internal const string ContentType = "image/webp";

    public async Task<CoverDeliveryResult> GetShownAsync(
        Guid artistId,
        int? size,
        CancellationToken cancellationToken = default)
    {
        ArtistPictureSources? sources = await pictures.FindSourcesAsync(artistId, cancellationToken);
        if (sources is null)
        {
            return CoverDeliveryResult.OwnerNotFound;
        }

        int requested = size ?? CoverSizes.Large;
        if (sources.LocalFile is { } local)
        {
            CoverDelivery? delivery = await covers.DeliverAsync([CandidateOf(local)], requested, cancellationToken);
            if (delivery is not null)
            {
                return CoverDeliveryResult.Found(delivery);
            }

            logger.LogDebug(
                "The picture in the folder of the artist {ArtistId} cannot be read; showing the next one",
                artistId);
        }

        foreach ((ArtistStoredPictureRow? stored, ArtistPictureKind kind) in
                 new[] { (sources.Uploaded, ArtistPictureKind.Uploaded), (sources.Online, ArtistPictureKind.Online) })
        {
            if (stored is not null && Open(artistId, stored, kind, requested) is { } delivery)
            {
                return CoverDeliveryResult.Found(delivery);
            }
        }

        return CoverDeliveryResult.NotFound;
    }

    public async Task<CoverDeliveryResult> GetStoredAsync(
        Guid artistId,
        ArtistPictureKind kind,
        int? size,
        CancellationToken cancellationToken = default)
    {
        ArtistPictureSources? sources = await pictures.FindSourcesAsync(artistId, cancellationToken);
        if (sources is null)
        {
            return CoverDeliveryResult.OwnerNotFound;
        }

        ArtistStoredPictureRow? stored = kind == ArtistPictureKind.Uploaded ? sources.Uploaded : sources.Online;
        return stored is not null && Open(artistId, stored, kind, size ?? CoverSizes.Large) is { } delivery
            ? CoverDeliveryResult.Found(delivery)
            : CoverDeliveryResult.NotFound;
    }

    public async Task<ArtistPictureUploadStatus> UploadAsync(
        Guid artistId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (await pictures.FindSourcesAsync(artistId, cancellationToken) is null)
        {
            return ArtistPictureUploadStatus.ArtistNotFound;
        }

        var pictureId = Guid.CreateVersion7();
        ArtistPictureSaveResult saved = await store.SaveAsync(pictureId, content, cancellationToken);
        if (saved.Status != ArtistPictureSaveStatus.Saved)
        {
            return saved.Status switch
            {
                ArtistPictureSaveStatus.UnsupportedFormat => ArtistPictureUploadStatus.UnsupportedFormat,
                ArtistPictureSaveStatus.Unreadable => ArtistPictureUploadStatus.Unreadable,
                _ => ArtistPictureUploadStatus.Unavailable,
            };
        }

        ArtistPicture picture = new()
        {
            Id = pictureId,
            ArtistId = artistId,
            Kind = ArtistPictureKind.Uploaded,
            CreatedAt = TruncateToMicroseconds(timeProvider.GetUtcNow()),
            Width = saved.Width,
            Height = saved.Height,
        };
        ArtistPictureReplacement replacement;
        try
        {
            replacement = await pictures.ReplaceAsync(picture, CancellationToken.None);
        }
        catch
        {
            store.Delete(pictureId);
            throw;
        }

        foreach (Guid replaced in replacement.ReplacedPictureIds)
        {
            store.Delete(replaced);
        }

        if (!replacement.Saved)
        {
            // The artist was removed during the upload.
            store.Delete(pictureId);
            return ArtistPictureUploadStatus.ArtistNotFound;
        }

        return ArtistPictureUploadStatus.Saved;
    }

    public async Task<bool> RemoveAsync(
        Guid artistId,
        ArtistPictureKind kind,
        CancellationToken cancellationToken = default)
    {
        ArtistPictureRemoval? removal = await pictures.RemoveAsync(artistId, kind, cancellationToken);
        if (removal is null)
        {
            return false;
        }

        if (removal.RemovedPictureId is { } removed)
        {
            store.Delete(removed);
        }

        return true;
    }

    public async Task RemoveOrphansAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> stored = store.ListPictureIds();
        if (stored.Count == 0)
        {
            return;
        }

        IReadOnlySet<Guid> existing = await pictures.ListExistingIdsAsync(stored, cancellationToken);
        foreach (Guid id in stored.Where(id => !existing.Contains(id)))
        {
            store.Delete(id);
        }
    }

    private CoverDelivery? Open(Guid artistId, ArtistStoredPictureRow stored, ArtistPictureKind kind, int size)
    {
        int snapped = CoverSizes.SnapStored(size);
        Stream? content = store.Open(stored.Id, snapped);
        if (content is null)
        {
            logger.LogWarning(
                "The {Kind} picture {PictureId} of the artist {ArtistId} is missing in its folder",
                kind,
                stored.Id,
                artistId);
            return null;
        }

        string version = kind == ArtistPictureKind.Uploaded
            ? ArtistPictureVersions.Uploaded(stored.Id)
            : ArtistPictureVersions.Online(stored.Id);
        return new CoverDelivery(
            content,
            ContentType,
            string.Create(CultureInfo.InvariantCulture, $"{version}-{snapped}"),
            stored.CreatedAt);
    }

    /// <summary>
    /// The picture file in the artist folder as a cover of the cache, so it is scaled and kept like the covers.
    /// </summary>
    private static CoverCandidateRow CandidateOf(ArtistLocalPictureRow local)
    {
        return new CoverCandidateRow(
            CoverId: local.MediaFileId,
            MediaFileId: local.MediaFileId,
            AlbumId: null,
            TrackId: null,
            TrackAlbumId: null,
            DiscNumber: null,
            TrackNumber: null,
            TrackTitle: null,
            IsPrimary: true,
            Source: CoverSource.File,
            SourceId: local.SourceId,
            RelativePath: local.RelativePath,
            MimeType: null,
            Size: local.Size,
            ModifiedAt: local.ModifiedAt);
    }

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        return new DateTimeOffset(value.UtcTicks - (value.UtcTicks % TimeSpan.TicksPerMicrosecond), TimeSpan.Zero);
    }
}
