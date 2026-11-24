using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using WaveMP.Core.ArtistPictures.Stores;
using WaveMP.Core.Caching.Images;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.ArtistPictures.Lookups;

internal sealed class ArtistPictureLookupService(
    IArtistPictureRepository pictures,
    IArtistPictureStore store,
    ArtistLookupCache cache,
    IMusicBrainzArtistClient musicBrainz,
    IWikidataClient wikidata,
    IWikimediaCommonsClient commons,
    IFanartTvClient fanartTv,
    ICoverImageEncoder encoder,
    ISettingsSnapshot<ArtistPictureSettings> settings,
    ISettingsSnapshot<OnlineMetadataSettings> onlineSettings,
    TimeProvider timeProvider,
    ILogger<ArtistPictureLookupService> logger) : IArtistPictureLookupService
{
    /// <summary>
    /// The sizes of the previews; a request is rounded up to the next one.
    /// </summary>
    internal static readonly int[] PreviewSizes = [300, 500];

    private static readonly string[] _pictureFormats = [ImageFormats.Jpeg, ImageFormats.Png, ImageFormats.WebP];

    public async Task<ArtistMatchesResult> FindMatchesAsync(
        Guid artistId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return ArtistMatchesResult.Failed(ArtistLookupStatus.Disabled);
        }

        ArtistLookupRow? artist = await pictures.FindLookupAsync(artistId, cancellationToken);
        if (artist is null)
        {
            return ArtistMatchesResult.Failed(ArtistLookupStatus.ArtistNotFound);
        }

        int pageOffset = Math.Max(0, offset);
        int pageLimit = Math.Clamp(limit, 1, IArtistPictureLookupService.MaxMatchLimit);
        string name = "musicbrainz/search-" + ArtistLookupCache.Hash(
            onlineSettings.Current.MusicBrainzUrl,
            artist.Name.Trim().ToUpperInvariant(),
            pageOffset.ToString(CultureInfo.InvariantCulture),
            pageLimit.ToString(CultureInfo.InvariantCulture)) + ".json";
        MusicBrainzArtistPage? page = await cache.ReadAsync<MusicBrainzArtistPage>(name, cancellationToken);
        if (page is null)
        {
            OnlineResponse<MusicBrainzArtistPage> response =
                await musicBrainz.SearchAsync(artist.Name, pageOffset, pageLimit, cancellationToken);
            if (!response.IsFound)
            {
                return ArtistMatchesResult.Failed(StatusOf(response.Status));
            }

            page = response.Value;
            await cache.WriteAsync(name, page, cancellationToken);
        }

        return new ArtistMatchesResult(ArtistLookupStatus.Found, page.Artists, page.Total, artist.MusicBrainzArtistId);
    }

    public async Task<ArtistCandidatesResult> FindCandidatesAsync(
        Guid artistId,
        Guid? musicBrainzArtistId,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return ArtistCandidatesResult.Failed(ArtistLookupStatus.Disabled);
        }

        ArtistLookupRow? artist = await pictures.FindLookupAsync(artistId, cancellationToken);
        if (artist is null)
        {
            return ArtistCandidatesResult.Failed(ArtistLookupStatus.ArtistNotFound);
        }

        if ((musicBrainzArtistId ?? artist.MusicBrainzArtistId) is not { } chosen || chosen == Guid.Empty)
        {
            return ArtistCandidatesResult.Failed(ArtistLookupStatus.MusicBrainzArtistMissing);
        }

        if (!cache.IsAvailable)
        {
            // Candidates are only referenced through the cache.
            logger.LogWarning("The cache cannot be written; artist pictures cannot be looked up online");
            return ArtistCandidatesResult.Failed(ArtistLookupStatus.ProcessingUnavailable);
        }

        List<ArtistCandidateRecord> candidates = [];
        Source wikimedia = await FindWikimediaAsync(artistId, chosen, candidates, cancellationToken);
        Source fanart = await FindFanartTvAsync(artistId, chosen, candidates, cancellationToken);
        if (wikimedia.Status == OnlineStatus.Disabled || fanart.Status == OnlineStatus.Disabled)
        {
            return ArtistCandidatesResult.Failed(ArtistLookupStatus.Disabled);
        }

        if (candidates.Count == 0 && (wikimedia.Failed || fanart.Failed))
        {
            return ArtistCandidatesResult.Failed(ArtistLookupStatus.Unavailable);
        }

        foreach (ArtistCandidateRecord candidate in candidates)
        {
            if (!await cache.WriteAsync(CandidateName(candidate.Id), candidate, cancellationToken))
            {
                return ArtistCandidatesResult.Failed(ArtistLookupStatus.ProcessingUnavailable);
            }
        }

        return new ArtistCandidatesResult(
            ArtistLookupStatus.Found,
            [.. candidates.Select(candidate => candidate.ToCandidate())]);
    }

    public async Task<ArtistCandidatePreview> GetPreviewAsync(
        Guid artistId,
        string candidateId,
        int? size,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return ArtistCandidatePreview.Failed(ArtistLookupStatus.Disabled);
        }

        (ArtistLookupStatus status, ArtistCandidateRecord? candidate) =
            await FindCandidateAsync(artistId, candidateId, cancellationToken);
        if (candidate is null)
        {
            return ArtistCandidatePreview.Failed(status);
        }

        int snapped = PreviewSizes.FirstOrDefault(preview => preview >= (size ?? PreviewSizes[^1]), PreviewSizes[^1]);
        string prefix = "previews/" + ArtistLookupCache.Hash(candidate.PreviewUrl);
        if (cache.Open(PreviewName(prefix, snapped)) is { } cached)
        {
            return new ArtistCandidatePreview(ArtistLookupStatus.Found, cached);
        }

        if (!Uri.TryCreate(candidate.PreviewUrl, UriKind.Absolute, out Uri? url))
        {
            return ArtistCandidatePreview.Failed(ArtistLookupStatus.Unavailable);
        }

        OnlineResponse<byte[]> download = await DownloadAsync(candidate.Provider, url, cancellationToken);
        if (!download.IsFound)
        {
            return ArtistCandidatePreview.Failed(StatusOf(download.Status));
        }

        ArtistLookupStatus encoded = await EncodePreviewsAsync(prefix, download.Value, cancellationToken);
        if (encoded != ArtistLookupStatus.Found)
        {
            return ArtistCandidatePreview.Failed(encoded);
        }

        return cache.Open(PreviewName(prefix, snapped)) is { } preview
            ? new ArtistCandidatePreview(ArtistLookupStatus.Found, preview)
            : ArtistCandidatePreview.Failed(ArtistLookupStatus.ProcessingUnavailable);
    }

    public async Task<ArtistLookupStatus> TakeOverAsync(
        Guid artistId,
        string candidateId,
        CoverCrop? crop,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return ArtistLookupStatus.Disabled;
        }

        (ArtistLookupStatus status, ArtistCandidateRecord? candidate) =
            await FindCandidateAsync(artistId, candidateId, cancellationToken);
        if (candidate is null)
        {
            return status;
        }

        OnlineResponse<Uri> address = await FullPictureUrlAsync(candidate, cancellationToken);
        if (!address.IsFound)
        {
            return StatusOf(address.Status);
        }

        OnlineResponse<byte[]> download = await DownloadAsync(candidate.Provider, address.Value, cancellationToken);
        if (!download.IsFound)
        {
            return StatusOf(download.Status);
        }

        var pictureId = Guid.CreateVersion7();
        ArtistPictureSaveResult saved;
        await using (MemoryStream content = new(download.Value, writable: false))
        {
            saved = await store.SaveAsync(pictureId, content, crop, cancellationToken);
        }

        switch (saved.Status)
        {
            case ArtistPictureSaveStatus.Saved:
                break;
            case ArtistPictureSaveStatus.Unavailable:
                return ArtistLookupStatus.ProcessingUnavailable;
            default:
                logger.LogWarning(
                    "The picture of {Provider} for the artist {ArtistId} is no usable JPEG, PNG or WebP picture",
                    candidate.Provider,
                    artistId);
                return ArtistLookupStatus.Unavailable;
        }

        ArtistPicture picture = new()
        {
            Id = pictureId,
            ArtistId = artistId,
            Kind = ArtistPictureKind.Online,
            CreatedAt = TruncateToMicroseconds(timeProvider.GetUtcNow()),
            Width = saved.Width,
            Height = saved.Height,
            Provider = candidate.Provider,
            MusicBrainzArtistId = candidate.MusicBrainzArtistId,
            Author = candidate.Author,
            License = candidate.License,
            LicenseUrl = candidate.LicenseUrl,
            SourceUrl = candidate.SourceUrl,
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
            // The artist was removed during the download.
            store.Delete(pictureId);
            return ArtistLookupStatus.ArtistNotFound;
        }

        await pictures.RememberMusicBrainzArtistAsync(artistId, candidate.MusicBrainzArtistId, CancellationToken.None);
        return ArtistLookupStatus.Found;
    }

    /// <summary>
    /// Whether the text can be the id of a candidate: 32 lower-case hexadecimal digits.
    /// </summary>
    internal static bool IsCandidateId(string? id)
    {
        return id is { Length: 32 } && id.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');
    }

    private bool IsEnabled => settings.Current.OnlineLookupEnabled;

    private static ArtistLookupStatus StatusOf(OnlineStatus status)
    {
        return status == OnlineStatus.Disabled ? ArtistLookupStatus.Disabled : ArtistLookupStatus.Unavailable;
    }

    private static string CandidateName(string candidateId)
    {
        return $"candidates/{candidateId}.json";
    }

    private static string PreviewName(string prefix, int size)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}-{size}.webp");
    }

    private static string NewCandidateId()
    {
        return Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    }

    private async Task<(ArtistLookupStatus Status, ArtistCandidateRecord? Candidate)> FindCandidateAsync(
        Guid artistId,
        string candidateId,
        CancellationToken cancellationToken)
    {
        if (await pictures.FindLookupAsync(artistId, cancellationToken) is null)
        {
            return (ArtistLookupStatus.ArtistNotFound, null);
        }

        if (!IsCandidateId(candidateId))
        {
            return (ArtistLookupStatus.CandidateNotFound, null);
        }

        ArtistCandidateRecord? candidate =
            await cache.ReadAsync<ArtistCandidateRecord>(CandidateName(candidateId), cancellationToken);
        return candidate is not null && candidate.ArtistId == artistId && candidate.Id == candidateId
            ? (ArtistLookupStatus.Found, candidate)
            : (ArtistLookupStatus.CandidateNotFound, null);
    }

    private async Task<Source> FindWikimediaAsync(
        Guid artistId,
        Guid musicBrainzArtistId,
        List<ArtistCandidateRecord> candidates,
        CancellationToken cancellationToken)
    {
        string linkName = "musicbrainz/wikidata-"
            + ArtistLookupCache.Hash(onlineSettings.Current.MusicBrainzUrl, musicBrainzArtistId.ToString("N")) + ".json";
        WikidataLink? link = await cache.ReadAsync<WikidataLink>(linkName, cancellationToken);
        if (link is null)
        {
            OnlineResponse<string> response = await musicBrainz.GetWikidataItemAsync(
                musicBrainzArtistId,
                cancellationToken);
            if (response.Status is not (OnlineStatus.Found or OnlineStatus.NotFound))
            {
                return new Source(response.Status);
            }

            link = new WikidataLink(response.Value);
            await cache.WriteAsync(linkName, link, cancellationToken);
        }

        if (link.ItemId is not { } itemId)
        {
            return new Source(OnlineStatus.Found);
        }

        string imagesName = $"wikidata/images-{itemId}.json";
        string[]? fileNames = await cache.ReadAsync<string[]>(imagesName, cancellationToken);
        if (fileNames is null)
        {
            OnlineResponse<IReadOnlyList<string>> response =
                await wikidata.GetImageFileNamesAsync(itemId, cancellationToken);
            if (response.Status is not (OnlineStatus.Found or OnlineStatus.NotFound))
            {
                return new Source(response.Status);
            }

            fileNames = [.. response.Value ?? []];
            await cache.WriteAsync(imagesName, fileNames, cancellationToken);
        }

        if (fileNames.Length == 0)
        {
            return new Source(OnlineStatus.Found);
        }

        OnlineResponse<WikimediaImage[]> images =
            await ImagesAsync(fileNames, IWikimediaCommonsClient.PreviewWidth, cancellationToken);
        if (!images.IsFound)
        {
            return new Source(images.Status);
        }

        foreach (WikimediaImage image in images.Value)
        {
            if (image.ThumbnailUrl is not { } preview || !IsPicture(image.MimeType))
            {
                continue;
            }

            candidates.Add(new ArtistCandidateRecord(
                NewCandidateId(),
                artistId,
                ArtistPictureProvider.Wikimedia,
                musicBrainzArtistId,
                image.FileName,
                null,
                preview.AbsoluteUri,
                image.Width,
                image.Height,
                UntrustedText.PlainText(image.Artist, UntrustedText.MaxAuthorLength),
                UntrustedText.PlainText(image.LicenseShortName, UntrustedText.MaxLicenseLength),
                UntrustedText.HttpsUrl(image.LicenseUrl),
                UntrustedText.HttpsUrl(image.DescriptionUrl?.OriginalString)));
        }

        return new Source(OnlineStatus.Found);
    }

    private async Task<Source> FindFanartTvAsync(
        Guid artistId,
        Guid musicBrainzArtistId,
        List<ArtistCandidateRecord> candidates,
        CancellationToken cancellationToken)
    {
        if (settings.Current.FanartTvApiKey is not { Length: > 0 } apiKey)
        {
            // Without the key of the admin, fanart.tv is never asked and its cached answers are not used.
            return new Source(OnlineStatus.Found);
        }

        string name = $"fanarttv/pictures-{musicBrainzArtistId:N}.json";
        FanartTvPicture[]? found = await cache.ReadAsync<FanartTvPicture[]>(name, cancellationToken);
        if (found is null)
        {
            OnlineResponse<IReadOnlyList<FanartTvPicture>> response =
                await fanartTv.GetArtistPicturesAsync(musicBrainzArtistId, apiKey, cancellationToken);
            if (!response.IsFound)
            {
                return new Source(response.Status);
            }

            found = [.. response.Value];
            await cache.WriteAsync(name, found, cancellationToken);
        }

        string source = string.Create(
            CultureInfo.InvariantCulture,
            $"https://fanart.tv/artist/{musicBrainzArtistId:D}");
        foreach (FanartTvPicture picture in found)
        {
            if (UntrustedText.HttpsUrl(picture.Url.OriginalString) is not { } url)
            {
                continue;
            }

            candidates.Add(new ArtistCandidateRecord(
                NewCandidateId(),
                artistId,
                ArtistPictureProvider.FanartTv,
                musicBrainzArtistId,
                null,
                url,
                url,
                null,
                null,
                null,
                null,
                null,
                source));
        }

        return new Source(OnlineStatus.Found);
    }

    /// <summary>
    /// The picture information of files on Commons, cached per file list and width.
    /// </summary>
    private async Task<OnlineResponse<WikimediaImage[]>> ImagesAsync(
        IReadOnlyList<string> fileNames,
        int width,
        CancellationToken cancellationToken)
    {
        string name = "commons/images-"
            + ArtistLookupCache.Hash([width.ToString(CultureInfo.InvariantCulture), .. fileNames]) + ".json";
        WikimediaImage[]? images = await cache.ReadAsync<WikimediaImage[]>(name, cancellationToken);
        if (images is not null)
        {
            return OnlineResponse<WikimediaImage[]>.Found(images);
        }

        OnlineResponse<IReadOnlyList<WikimediaImage>> response =
            await commons.GetImagesAsync(fileNames, width, cancellationToken);
        if (!response.IsFound)
        {
            return OnlineResponse<WikimediaImage[]>.Failed(response.Status);
        }

        images = [.. response.Value];
        await cache.WriteAsync(name, images, cancellationToken);
        return OnlineResponse<WikimediaImage[]>.Found(images);
    }

    /// <summary>
    /// The address of the picture that is taken over: the thumbnail of the full width on Commons, the picture itself
    /// on fanart.tv.
    /// </summary>
    private async Task<OnlineResponse<Uri>> FullPictureUrlAsync(
        ArtistCandidateRecord candidate,
        CancellationToken cancellationToken)
    {
        if (candidate.Provider == ArtistPictureProvider.FanartTv)
        {
            return Uri.TryCreate(candidate.ImageUrl, UriKind.Absolute, out Uri? url)
                ? OnlineResponse<Uri>.Found(url)
                : OnlineResponse<Uri>.Failed(OnlineStatus.Unavailable);
        }

        if (candidate.FileName is not { } fileName)
        {
            return OnlineResponse<Uri>.Failed(OnlineStatus.Unavailable);
        }

        OnlineResponse<WikimediaImage[]> images =
            await ImagesAsync([fileName], IWikimediaCommonsClient.FullWidth, cancellationToken);
        if (!images.IsFound)
        {
            return OnlineResponse<Uri>.Failed(images.Status);
        }

        return images.Value.FirstOrDefault()?.ThumbnailUrl is { } thumbnail
            ? OnlineResponse<Uri>.Found(thumbnail)
            : OnlineResponse<Uri>.Failed(OnlineStatus.Unavailable);
    }

    private Task<OnlineResponse<byte[]>> DownloadAsync(
        ArtistPictureProvider provider,
        Uri url,
        CancellationToken cancellationToken)
    {
        return provider == ArtistPictureProvider.FanartTv
            ? fanartTv.DownloadAsync(url, cancellationToken)
            : commons.DownloadAsync(url, cancellationToken);
    }

    /// <summary>
    /// Re-encodes a downloaded picture into the preview sizes in the cache.
    /// </summary>
    private async Task<ArtistLookupStatus> EncodePreviewsAsync(
        string prefix,
        byte[] picture,
        CancellationToken cancellationToken)
    {
        string? contentType = ImageFormats.Detect(picture.AsSpan(0, Math.Min(picture.Length, ImageFormats.HeaderLength)));
        if (contentType is null || !_pictureFormats.Contains(contentType))
        {
            logger.LogWarning("A picture of the artist lookup is no JPEG, PNG or WebP picture");
            return ArtistLookupStatus.Unavailable;
        }

        CoverEncodeTarget[] targets =
        [
            .. PreviewSizes.Select(size => new CoverEncodeTarget(size, cache.CreateTemporaryPath(".webp"))),
        ];
        try
        {
            CoverEncodeStatus status;
            await using (MemoryStream content = new(picture, writable: false))
            {
                status = await encoder.EncodeAsync(content, contentType, targets, cancellationToken);
            }

            switch (status)
            {
                case CoverEncodeStatus.Unavailable:
                    return ArtistLookupStatus.ProcessingUnavailable;
                case CoverEncodeStatus.Failed:
                    logger.LogWarning("A picture of the artist lookup cannot be read");
                    return ArtistLookupStatus.Unavailable;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                if (!cache.Commit(PreviewName(prefix, PreviewSizes[index]), targets[index].OutputPath))
                {
                    return ArtistLookupStatus.ProcessingUnavailable;
                }
            }

            return ArtistLookupStatus.Found;
        }
        finally
        {
            foreach (CoverEncodeTarget target in targets)
            {
                cache.DeleteTemporary(target.OutputPath);
            }
        }
    }

    private static bool IsPicture(string? mimeType)
    {
        // Commons renders other formats (e.g. SVG or TIFF) as PNG or JPEG thumbnails; the content decides later.
        return mimeType is null || mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        return new DateTimeOffset(value.UtcTicks - (value.UtcTicks % TimeSpan.TicksPerMicrosecond), TimeSpan.Zero);
    }

    /// <summary>
    /// The Wikidata item of a MusicBrainz artist as cached; <c>null</c> if it has none.
    /// </summary>
    internal sealed record WikidataLink(string? ItemId);

    /// <summary>
    /// How asking one service went.
    /// </summary>
    private sealed record Source(OnlineStatus Status)
    {
        public bool Failed => Status is OnlineStatus.Unavailable;
    }
}
