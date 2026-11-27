using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Calls <c>GET ws/2/artist</c> of MusicBrainz with JSON answers for the online lookup of artist pictures.
/// </summary>
internal sealed class MusicBrainzArtistClient(
    OnlineRequestSender sender,
    ISettingsSnapshot<OnlineMetadataSettings> settings,
    IOptions<ArtistLookupClientOptions> options,
    ILogger<MusicBrainzArtistClient> logger) : IMusicBrainzArtistClient
{
    /// <summary>
    /// Most results MusicBrainz returns per search.
    /// </summary>
    internal const int MaxLimit = 100;

    /// <summary>
    /// Most aliases taken of one artist; well-known artists have many names in other languages.
    /// </summary>
    internal const int MaxAliases = 100;

    private const string WikidataItemPrefix = "https://www.wikidata.org/wiki/";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<OnlineResponse<MusicBrainzArtistPage>> SearchAsync(
        string name,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        Uri url = SearchUrl(settings.Current.MusicBrainzUrl, name, offset, limit);
        OnlineResponse<ArtistList> response = await GetAsync<ArtistList>(url, cancellationToken);
        if (!response.IsFound)
        {
            // A search that finds nothing is an empty list; 404 only comes from a wrong address.
            return OnlineResponse<MusicBrainzArtistPage>.Failed(
                response.Status == OnlineStatus.NotFound ? OnlineStatus.Unavailable : response.Status);
        }

        MusicBrainzArtistMatch[] artists =
        [
            .. response.Value.Artists
                .Where(artist => artist.Id != Guid.Empty && !string.IsNullOrWhiteSpace(artist.Name))
                .Select(artist => new MusicBrainzArtistMatch(
                    artist.Id,
                    artist.Name!.Trim(),
                    Text(artist.SortName),
                    Text(artist.Disambiguation),
                    Text(artist.Type),
                    Text(artist.Country),
                    Text(artist.LifeSpan?.Begin),
                    Text(artist.LifeSpan?.End),
                    Math.Clamp(artist.Score, 0, 100))),
        ];
        return OnlineResponse<MusicBrainzArtistPage>.Found(
            new MusicBrainzArtistPage(Math.Max(response.Value.Count, offset + artists.Length), artists));
    }

    public async Task<OnlineResponse<MusicBrainzArtistDetails>> GetArtistAsync(
        Guid artistId,
        CancellationToken cancellationToken = default)
    {
        Uri url = ArtistUrl(settings.Current.MusicBrainzUrl, artistId);
        OnlineResponse<ArtistWithRelations> response = await GetAsync<ArtistWithRelations>(url, cancellationToken);
        if (!response.IsFound)
        {
            return OnlineResponse<MusicBrainzArtistDetails>.Failed(response.Status);
        }

        string? item = response.Value.Relations
            .Where(relation => string.Equals(relation.Type, "wikidata", StringComparison.OrdinalIgnoreCase))
            .Select(relation => WikidataItemOf(relation.Url?.Resource))
            .FirstOrDefault(id => id is not null);
        string[] aliases =
        [
            .. response.Value.Aliases
                .Select(alias => Text(alias.Name))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .Take(MaxAliases),
        ];
        return OnlineResponse<MusicBrainzArtistDetails>.Found(
            new MusicBrainzArtistDetails(item, Text(response.Value.SortName), aliases));
    }

    /// <summary>
    /// The address of an artist search:
    /// <c>{base}/ws/2/artist?query=artist:"…"&amp;fmt=json&amp;limit=…&amp;offset=…</c>.
    /// </summary>
    internal static Uri SearchUrl(string baseUrl, string name, int offset, int limit)
    {
        string query = "artist:" + MusicBrainzQuery.Phrase(name);
        return new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"{baseUrl.TrimEnd('/')}/ws/2/artist?query={Uri.EscapeDataString(query)}&fmt=json"
            + $"&limit={Math.Clamp(limit, 1, MaxLimit)}&offset={Math.Max(0, offset)}"));
    }

    /// <summary>
    /// The address of an artist with its links and aliases:
    /// <c>{base}/ws/2/artist/{id}?inc=url-rels+aliases&amp;fmt=json</c>.
    /// </summary>
    internal static Uri ArtistUrl(string baseUrl, Guid artistId)
    {
        return new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"{baseUrl.TrimEnd('/')}/ws/2/artist/{artistId:D}?inc=url-rels+aliases&fmt=json"));
    }

    /// <summary>
    /// The id of a Wikidata item from the address of its page (<c>https://www.wikidata.org/wiki/Q…</c>); <c>null</c>
    /// for any other address.
    /// </summary>
    internal static string? WikidataItemOf(string? url)
    {
        if (url is null || !url.StartsWith(WikidataItemPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string id = url[WikidataItemPrefix.Length..].TrimEnd('/');
        return WikidataClient.IsItemId(id) ? id : null;
    }

    private static string? Text(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<OnlineResponse<T>> GetAsync<T>(Uri url, CancellationToken cancellationToken)
        where T : class
    {
        OnlineResponse<byte[]> response = await sender.SendAsync(
            new OnlineRequest(url, OnlineGate.ArtistPictures, options.Value.MaxJsonBytes) { Queued = true },
            cancellationToken);
        if (!response.IsFound)
        {
            return OnlineResponse<T>.Failed(response.Status);
        }

        try
        {
            T? value = JsonSerializer.Deserialize<T>(response.Value, _jsonOptions);
            return value is null
                ? OnlineResponse<T>.Failed(OnlineStatus.Unavailable)
                : OnlineResponse<T>.Found(value);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The answer of MusicBrainz cannot be read");
            return OnlineResponse<T>.Failed(OnlineStatus.Unavailable);
        }
    }

    private sealed class ArtistList
    {
        [JsonPropertyName("count")]
        public int Count { get; init; }

        [JsonPropertyName("artists")]
        public IReadOnlyList<Artist> Artists { get; init; } = [];
    }

    private sealed class Artist
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("score")]
        public int Score { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("sort-name")]
        public string? SortName { get; init; }

        [JsonPropertyName("disambiguation")]
        public string? Disambiguation { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }

        [JsonPropertyName("life-span")]
        public LifeSpan? LifeSpan { get; init; }
    }

    private sealed class LifeSpan
    {
        [JsonPropertyName("begin")]
        public string? Begin { get; init; }

        [JsonPropertyName("end")]
        public string? End { get; init; }
    }

    private sealed class ArtistWithRelations
    {
        [JsonPropertyName("sort-name")]
        public string? SortName { get; init; }

        [JsonPropertyName("relations")]
        public IReadOnlyList<Relation> Relations { get; init; } = [];

        [JsonPropertyName("aliases")]
        public IReadOnlyList<Alias> Aliases { get; init; } = [];
    }

    private sealed class Alias
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class Relation
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("url")]
        public RelationUrl? Url { get; init; }
    }

    private sealed class RelationUrl
    {
        [JsonPropertyName("resource")]
        public string? Resource { get; init; }
    }
}
