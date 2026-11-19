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
/// Calls <c>GET ws/2/recording</c> and <c>GET ws/2/release</c> of MusicBrainz with JSON answers.
/// </summary>
internal sealed class MusicBrainzClient(
    OnlineRequestSender sender,
    ISettingsSnapshot<OnlineMetadataSettings> settings,
    IOptions<OnlineMetadataOptions> options,
    ILogger<MusicBrainzClient> logger) : IMusicBrainzClient
{
    /// <summary>
    /// Largest answer of a search that is read.
    /// </summary>
    internal const long MaxAnswerBytes = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<OnlineResponse<IReadOnlyList<MusicBrainzRecording>>> SearchRecordingsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        OnlineResponse<MusicBrainzRecordingList> response =
            await SearchAsync<MusicBrainzRecordingList>("recording", query, cancellationToken);
        return response.IsFound
            ? OnlineResponse<IReadOnlyList<MusicBrainzRecording>>.Found(response.Value.Recordings)
            : OnlineResponse<IReadOnlyList<MusicBrainzRecording>>.Failed(response.Status);
    }

    public async Task<OnlineResponse<IReadOnlyList<MusicBrainzRelease>>> SearchReleasesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        OnlineResponse<MusicBrainzReleaseList> response =
            await SearchAsync<MusicBrainzReleaseList>("release", query, cancellationToken);
        return response.IsFound
            ? OnlineResponse<IReadOnlyList<MusicBrainzRelease>>.Found(response.Value.Releases)
            : OnlineResponse<IReadOnlyList<MusicBrainzRelease>>.Failed(response.Status);
    }

    /// <summary>
    /// The address of a search: <c>{base}/ws/2/{entity}?query=…&amp;fmt=json&amp;limit=…</c>.
    /// </summary>
    internal static Uri SearchUrl(string baseUrl, string entity, string query, int limit)
    {
        return new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"{baseUrl.TrimEnd('/')}/ws/2/{entity}?query={Uri.EscapeDataString(query)}&fmt=json&limit={limit}"));
    }

    private async Task<OnlineResponse<T>> SearchAsync<T>(
        string entity,
        string query,
        CancellationToken cancellationToken)
        where T : class
    {
        Uri url = SearchUrl(
            settings.Current.MusicBrainzUrl,
            entity,
            query,
            Math.Clamp(options.Value.SearchLimit, 1, 100));
        OnlineResponse<byte[]> response = await sender.GetAsync(url, queued: true, MaxAnswerBytes, cancellationToken);
        if (!response.IsFound)
        {
            // A search that finds nothing is an empty list; 404 only comes from a wrong address.
            return OnlineResponse<T>.Failed(
                response.Status == OnlineStatus.NotFound ? OnlineStatus.Unavailable : response.Status);
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
}
