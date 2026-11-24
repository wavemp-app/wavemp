using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.OnlineMetadata.Options;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Calls <c>GET https://webservice.fanart.tv/v3/music/{id}</c> with the API key in the header <c>api-key</c> and
/// downloads the pictures from <c>assets.fanart.tv</c>. The answer is read leniently: entries without an address are
/// skipped. The key never appears in an address, a log or an exception.
/// </summary>
internal sealed class FanartTvClient(
    OnlineRequestSender sender,
    IOptions<ArtistLookupClientOptions> options,
    ILogger<FanartTvClient> logger) : IFanartTvClient
{
    /// <summary>
    /// The header of the API key.
    /// </summary>
    internal const string ApiKeyHeader = "api-key";

    /// <summary>
    /// Most pictures of one artist that are used.
    /// </summary>
    internal const int MaxPictures = 20;

    public async Task<OnlineResponse<IReadOnlyList<FanartTvPicture>>> GetArtistPicturesAsync(
        Guid musicBrainzArtistId,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return OnlineResponse<IReadOnlyList<FanartTvPicture>>.Failed(OnlineStatus.Unavailable);
        }

        OnlineResponse<byte[]> response = await sender.SendAsync(
            new OnlineRequest(ArtistUrl(musicBrainzArtistId), OnlineGate.ArtistPictures, options.Value.MaxJsonBytes)
            {
                Lane = OnlineLane.FanartTv,
                Headers = [new KeyValuePair<string, string>(ApiKeyHeader, apiKey.Trim())],
            },
            cancellationToken);
        if (response.Status == OnlineStatus.NotFound)
        {
            return OnlineResponse<IReadOnlyList<FanartTvPicture>>.Found([]);
        }

        if (!response.IsFound)
        {
            return OnlineResponse<IReadOnlyList<FanartTvPicture>>.Failed(response.Status);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Value);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("artistthumb", out JsonElement thumbs)
                || thumbs.ValueKind != JsonValueKind.Array)
            {
                return OnlineResponse<IReadOnlyList<FanartTvPicture>>.Found([]);
            }

            FanartTvPicture[] pictures =
            [
                .. thumbs.EnumerateArray()
                    .Select(PictureOf)
                    .OfType<FanartTvPicture>()
                    .DistinctBy(picture => picture.Url)
                    .OrderByDescending(picture => picture.Likes)
                    .Take(MaxPictures),
            ];
            return OnlineResponse<IReadOnlyList<FanartTvPicture>>.Found(pictures);
        }
        catch (JsonException)
        {
            // The text of the exception may quote the answer; the warning alone is enough.
            logger.LogWarning("The answer of fanart.tv cannot be read");
            return OnlineResponse<IReadOnlyList<FanartTvPicture>>.Failed(OnlineStatus.Unavailable);
        }
    }

    public Task<OnlineResponse<byte[]>> DownloadAsync(Uri url, CancellationToken cancellationToken = default)
    {
        return sender.SendAsync(
            new OnlineRequest(url, OnlineGate.ArtistPictures, options.Value.MaxDownloadBytes)
            {
                Lane = OnlineLane.FanartTv,
            },
            cancellationToken);
    }

    /// <summary>
    /// The address of the pictures of an artist.
    /// </summary>
    internal static Uri ArtistUrl(Guid musicBrainzArtistId)
    {
        return new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"https://{OnlineHostAllowlist.FanartTvService}/v3/music/{musicBrainzArtistId:D}"));
    }

    private static FanartTvPicture? PictureOf(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty("url", out JsonElement urlValue)
            || urlValue.ValueKind != JsonValueKind.String
            || !Uri.TryCreate(urlValue.GetString(), UriKind.Absolute, out Uri? url)
            || url.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        string id = entry.TryGetProperty("id", out JsonElement idValue)
            ? idValue.ValueKind switch
            {
                JsonValueKind.String => idValue.GetString() ?? string.Empty,
                JsonValueKind.Number => idValue.GetRawText(),
                _ => string.Empty,
            }
            : string.Empty;
        return new FanartTvPicture(id, url, Likes(entry));
    }

    private static int Likes(JsonElement entry)
    {
        if (!entry.TryGetProperty("likes", out JsonElement likes))
        {
            return 0;
        }

        return likes.ValueKind switch
        {
            JsonValueKind.Number when likes.TryGetInt32(out int number) => Math.Max(0, number),
            JsonValueKind.String when int.TryParse(
                likes.GetString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int number) => number,
            _ => 0,
        };
    }
}
