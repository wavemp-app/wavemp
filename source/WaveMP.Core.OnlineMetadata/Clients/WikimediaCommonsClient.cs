using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.OnlineMetadata.Options;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Calls <c>GET https://commons.wikimedia.org/w/api.php?action=query&amp;prop=imageinfo</c> with JSON answers
/// (format version 2) and downloads the thumbnails from <c>upload.wikimedia.org</c>. Thumbnails are only requested in
/// the standard widths of Wikimedia; the size of a downloaded picture is measured after decoding, never taken from the
/// answer. The answer is read leniently: pages of another shape are skipped.
/// </summary>
internal sealed class WikimediaCommonsClient(
    OnlineRequestSender sender,
    IOptions<ArtistLookupClientOptions> options,
    ILogger<WikimediaCommonsClient> logger) : IWikimediaCommonsClient
{
    /// <summary>
    /// Most files of one request, as the API allows.
    /// </summary>
    internal const int MaxFiles = 50;

    private const string FilePrefix = "File:";

    private const string MetadataFilter = "Artist|Credit|LicenseShortName|LicenseUrl|AttributionRequired";

    public async Task<OnlineResponse<IReadOnlyList<WikimediaImage>>> GetImagesAsync(
        IReadOnlyList<string> fileNames,
        int thumbnailWidth,
        CancellationToken cancellationToken = default)
    {
        string[] names = [.. fileNames.Where(IsFileName).Select(name => name.Trim()).Distinct().Take(MaxFiles)];
        if (names.Length == 0)
        {
            return OnlineResponse<IReadOnlyList<WikimediaImage>>.Found([]);
        }

        int width = thumbnailWidth == IWikimediaCommonsClient.FullWidth
            ? IWikimediaCommonsClient.FullWidth
            : IWikimediaCommonsClient.PreviewWidth;
        OnlineResponse<byte[]> response = await sender.SendAsync(
            new OnlineRequest(ImageInfoUrl(names, width), OnlineGate.ArtistPictures, options.Value.MaxJsonBytes)
            {
                Lane = OnlineLane.Wikimedia,
                MinRetryDelay = WikidataClient.MinRetryDelay,
            },
            cancellationToken);
        if (!response.IsFound)
        {
            return OnlineResponse<IReadOnlyList<WikimediaImage>>.Failed(
                response.Status == OnlineStatus.NotFound ? OnlineStatus.Unavailable : response.Status);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Value);
            Dictionary<string, WikimediaImage> found = new(StringComparer.Ordinal);
            if (document.RootElement.TryGetProperty("query", out JsonElement query)
                && query.ValueKind == JsonValueKind.Object
                && query.TryGetProperty("pages", out JsonElement pages)
                && pages.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement page in pages.EnumerateArray())
                {
                    if (ImageOf(page) is { } image)
                    {
                        found[Normalize(image.FileName)] = image;
                    }
                }
            }

            WikimediaImage[] images =
            [
                .. names
                    .Select(name => found.GetValueOrDefault(Normalize(name)))
                    .OfType<WikimediaImage>()
                    .DistinctBy(image => image.FileName),
            ];
            return OnlineResponse<IReadOnlyList<WikimediaImage>>.Found(images);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The answer of Wikimedia Commons cannot be read");
            return OnlineResponse<IReadOnlyList<WikimediaImage>>.Failed(OnlineStatus.Unavailable);
        }
    }

    public Task<OnlineResponse<byte[]>> DownloadAsync(Uri url, CancellationToken cancellationToken = default)
    {
        return sender.SendAsync(
            new OnlineRequest(url, OnlineGate.ArtistPictures, options.Value.MaxDownloadBytes)
            {
                Lane = OnlineLane.Wikimedia,
                MinRetryDelay = WikidataClient.MinRetryDelay,
            },
            cancellationToken);
    }

    /// <summary>
    /// Whether the text can be the name of a file on Commons: not empty, at most 240 characters, without the
    /// characters titles cannot have.
    /// </summary>
    internal static bool IsFileName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name.Length <= 240
            && !name.Any(character => char.IsControl(character) || character is '|' or '#' or '<' or '>' or '[' or ']'
                or '{' or '}');
    }

    /// <summary>
    /// The address of the picture information of the files.
    /// </summary>
    internal static Uri ImageInfoUrl(IReadOnlyList<string> fileNames, int thumbnailWidth)
    {
        string titles = string.Join("%7C", fileNames.Select(name => Uri.EscapeDataString(FilePrefix + name)));
        return new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"https://{OnlineHostAllowlist.WikimediaCommons}/w/api.php?action=query&format=json&formatversion=2"
            + $"&prop=imageinfo&titles={titles}&iiprop=url%7Csize%7Cmime%7Cextmetadata&iiurlwidth={thumbnailWidth}"
            + $"&iiextmetadatafilter={Uri.EscapeDataString(MetadataFilter)}"));
    }

    /// <summary>
    /// Names compared like titles of Commons: underscores are spaces and the first letter is upper case.
    /// </summary>
    private static string Normalize(string name)
    {
        string spaced = name.Trim().Replace('_', ' ');
        return spaced.Length == 0 ? spaced : char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    private static WikimediaImage? ImageOf(JsonElement page)
    {
        if (page.ValueKind != JsonValueKind.Object
            || page.TryGetProperty("missing", out _)
            || String(page, "title") is not { } title
            || !page.TryGetProperty("imageinfo", out JsonElement infos)
            || infos.ValueKind != JsonValueKind.Array
            || infos.GetArrayLength() == 0)
        {
            return null;
        }

        JsonElement info = infos[0];
        if (info.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string fileName = title.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase)
            ? title[FilePrefix.Length..]
            : title;
        JsonElement metadata = info.TryGetProperty("extmetadata", out JsonElement extmetadata)
            && extmetadata.ValueKind == JsonValueKind.Object
                ? extmetadata
                : default;
        return new WikimediaImage(
            fileName,
            Address(String(info, "thumburl")),
            Address(String(info, "descriptionurl")),
            Number(info, "width"),
            Number(info, "height"),
            String(info, "mime"),
            MetadataOf(metadata, "Artist"),
            MetadataOf(metadata, "Credit"),
            MetadataOf(metadata, "LicenseShortName"),
            MetadataOf(metadata, "LicenseUrl"),
            MetadataOf(metadata, "AttributionRequired") is { } required
                ? string.Equals(required.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                : null);
    }

    private static string? MetadataOf(JsonElement metadata, string name)
    {
        if (metadata.ValueKind != JsonValueKind.Object
            || !metadata.TryGetProperty(name, out JsonElement entry)
            || entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty("value", out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null,
        };
    }

    private static string? String(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? Number(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int number)
            && number > 0
                ? number
                : null;
    }

    private static Uri? Address(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? url) ? url : null;
    }
}
