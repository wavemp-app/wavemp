using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.OnlineMetadata.Options;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Calls <c>GET https://www.wikidata.org/w/api.php?action=wbgetentities</c> with JSON answers. The answer is read
/// leniently: claims of another shape are skipped.
/// </summary>
internal sealed class WikidataClient(
    OnlineRequestSender sender,
    IOptions<ArtistLookupClientOptions> options,
    ILogger<WikidataClient> logger) : IWikidataClient
{
    /// <summary>
    /// The property "image" of Wikidata.
    /// </summary>
    internal const string ImageProperty = "P18";

    /// <summary>
    /// Most pictures of one item that are used.
    /// </summary>
    internal const int MaxImages = 10;

    /// <summary>
    /// Shortest wait before asking Wikimedia again after it was busy, as its API etiquette asks for.
    /// </summary>
    internal static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(5);

    public async Task<OnlineResponse<IReadOnlyList<string>>> GetImageFileNamesAsync(
        string itemId,
        CancellationToken cancellationToken = default)
    {
        if (!IsItemId(itemId))
        {
            return OnlineResponse<IReadOnlyList<string>>.Failed(OnlineStatus.NotFound);
        }

        OnlineResponse<byte[]> response = await sender.SendAsync(
            new OnlineRequest(EntityUrl(itemId), OnlineGate.ArtistPictures, options.Value.MaxJsonBytes)
            {
                Lane = OnlineLane.Wikimedia,
                MinRetryDelay = MinRetryDelay,
            },
            cancellationToken);
        if (!response.IsFound)
        {
            return OnlineResponse<IReadOnlyList<string>>.Failed(response.Status);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Value);
            if (!document.RootElement.TryGetProperty("entities", out JsonElement entities)
                || entities.ValueKind != JsonValueKind.Object)
            {
                return OnlineResponse<IReadOnlyList<string>>.Failed(OnlineStatus.Unavailable);
            }

            List<(string Name, int Rank)> images = [];
            bool known = false;
            foreach (JsonProperty entity in entities.EnumerateObject())
            {
                if (entity.Value.ValueKind != JsonValueKind.Object || entity.Value.TryGetProperty("missing", out _))
                {
                    continue;
                }

                known = true;
                images.AddRange(ImagesOf(entity.Value));
            }

            if (!known)
            {
                return OnlineResponse<IReadOnlyList<string>>.Failed(OnlineStatus.NotFound);
            }

            string[] names =
            [
                .. images
                    .Select((image, index) => (image.Name, image.Rank, Index: index))
                    .OrderBy(image => image.Rank)
                    .ThenBy(image => image.Index)
                    .Select(image => image.Name)
                    .Distinct(StringComparer.Ordinal)
                    .Take(MaxImages),
            ];
            return OnlineResponse<IReadOnlyList<string>>.Found(names);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The answer of Wikidata cannot be read");
            return OnlineResponse<IReadOnlyList<string>>.Failed(OnlineStatus.Unavailable);
        }
    }

    /// <summary>
    /// Whether the text is the id of an item: <c>Q</c> and a number without leading zero.
    /// </summary>
    internal static bool IsItemId(string? id)
    {
        return id is { Length: >= 2 and <= 13 } && id[0] == 'Q' && id[1] != '0' && id.Skip(1).All(char.IsAsciiDigit);
    }

    /// <summary>
    /// The address of the claims of an item.
    /// </summary>
    internal static Uri EntityUrl(string itemId)
    {
        return new Uri(
            $"https://{OnlineHostAllowlist.Wikidata}/w/api.php?action=wbgetentities&ids={itemId}"
            + "&props=claims&format=json");
    }

    /// <summary>
    /// The picture names of the claims P18 of an entity with their order: preferred 0, normal 1; deprecated ones and
    /// file names Commons cannot have are left out.
    /// </summary>
    private static IEnumerable<(string Name, int Rank)> ImagesOf(JsonElement entity)
    {
        if (!entity.TryGetProperty("claims", out JsonElement claims)
            || claims.ValueKind != JsonValueKind.Object
            || !claims.TryGetProperty(ImageProperty, out JsonElement images)
            || images.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement claim in images.EnumerateArray())
        {
            if (claim.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? rank = claim.TryGetProperty("rank", out JsonElement rankValue)
                && rankValue.ValueKind == JsonValueKind.String
                    ? rankValue.GetString()
                    : null;
            if (rank == "deprecated")
            {
                continue;
            }

            if (claim.TryGetProperty("mainsnak", out JsonElement snak)
                && snak.ValueKind == JsonValueKind.Object
                && snak.TryGetProperty("datavalue", out JsonElement data)
                && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("value", out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && WikimediaCommonsClient.IsFileName(value.GetString()))
            {
                yield return (value.GetString()!.Trim(), rank == "preferred" ? 0 : 1);
            }
        }
    }
}
