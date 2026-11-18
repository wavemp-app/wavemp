using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Scrobbling.Options;

namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// Calls the Last.fm API over HTTP: every method is a signed <c>POST</c> with form data to the API address, answered
/// as JSON. Errors are reported by Last.fm as <c>{"error": code, "message": text}</c>, also with HTTP status 200.
/// </summary>
internal sealed class LastFmClient(
    HttpClient http,
    IOptions<ScrobblingOptions> options,
    ILogger<LastFmClient> logger) : ILastFmClient
{
    // Error codes of the Last.fm API, see https://www.last.fm/api/errorcodes.
    private const int InvalidParametersError = 6;
    private const int AuthenticationFailedError = 4;
    private const int InvalidSessionKeyError = 9;
    private const int UnauthorizedTokenError = 14;
    private const int ExpiredTokenError = 15;
    private const int OperationFailedError = 8;
    private const int ServiceOfflineError = 11;
    private const int TemporarilyUnavailableError = 16;
    private const int RateLimitError = 29;

    public async Task<ScrobbleAuthorization> GetSessionAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        LastFmResponse response = await SendAsync(
            "auth.getSession",
            [new("token", token)],
            cancellationToken);
        switch (response.Kind)
        {
            case LastFmResponseKind.Success:
                if (response.Body.TryGetProperty("session", out JsonElement session)
                    && ReadString(session, "name") is { Length: > 0 } name
                    && ReadString(session, "key") is { Length: > 0 } key)
                {
                    return ScrobbleAuthorization.Authorized(name, key);
                }

                logger.LogWarning("Last.fm answered auth.getSession without a session");
                return ScrobbleAuthorization.Unavailable;
            case LastFmResponseKind.Error when response.ErrorCode is AuthenticationFailedError
                or UnauthorizedTokenError
                or ExpiredTokenError
                or InvalidParametersError:
                return ScrobbleAuthorization.Invalid;
            default:
                return ScrobbleAuthorization.Unavailable;
        }
    }

    public async Task<ScrobbleSendStatus> UpdateNowPlayingAsync(
        string sessionKey,
        ScrobbleTrack track,
        CancellationToken cancellationToken = default)
    {
        List<KeyValuePair<string, string>> parameters = [new("sk", sessionKey)];
        AddTrack(parameters, track, index: null);
        LastFmResponse response = await SendAsync("track.updateNowPlaying", parameters, cancellationToken);
        return ToSendStatus(response);
    }

    public async Task<ScrobbleSendStatus> ScrobbleAsync(
        string sessionKey,
        IReadOnlyList<ScrobblePlay> plays,
        CancellationToken cancellationToken = default)
    {
        if (plays.Count == 0)
        {
            return ScrobbleSendStatus.Accepted;
        }

        List<KeyValuePair<string, string>> parameters = [new("sk", sessionKey)];
        for (int index = 0; index < plays.Count; index++)
        {
            ScrobblePlay play = plays[index];
            AddTrack(parameters, play.Track, index);
            parameters.Add(new(
                Indexed("timestamp", index),
                play.PlayedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        }

        LastFmResponse response = await SendAsync("track.scrobble", parameters, cancellationToken);
        if (response.Kind == LastFmResponseKind.Success
            && response.Body.TryGetProperty("scrobbles", out JsonElement scrobbles)
            && scrobbles.TryGetProperty("@attr", out JsonElement attributes)
            && ReadInt(attributes, "ignored") is > 0 and int ignored)
        {
            logger.LogInformation("Last.fm ignored {Ignored} of {Count} scrobbles", ignored, plays.Count);
        }

        return ToSendStatus(response);
    }

    /// <summary>
    /// The signature of a request: the MD5 hash (hexadecimal, lower case) of all parameters except <c>format</c> and
    /// <c>callback</c>, sorted by name, written as name and value without separators, followed by the shared secret.
    /// </summary>
    internal static string Sign(IEnumerable<KeyValuePair<string, string>> parameters, string sharedSecret)
    {
        StringBuilder builder = new();
        foreach (KeyValuePair<string, string> parameter in parameters
            .Where(parameter => parameter.Key is not "format" and not "callback")
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal))
        {
            builder.Append(parameter.Key).Append(parameter.Value);
        }

        builder.Append(sharedSecret);

        // Last.fm prescribes MD5 for the signature; it protects nothing of WaveMP.
#pragma warning disable CA5351
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
#pragma warning restore CA5351
        return Convert.ToHexStringLower(hash);
    }

    private static void AddTrack(List<KeyValuePair<string, string>> parameters, ScrobbleTrack track, int? index)
    {
        parameters.Add(new(Indexed("artist", index), track.Artist));
        parameters.Add(new(Indexed("track", index), track.Title));
        if (!string.IsNullOrWhiteSpace(track.Album))
        {
            parameters.Add(new(Indexed("album", index), track.Album));
        }

        if (!string.IsNullOrWhiteSpace(track.AlbumArtist))
        {
            parameters.Add(new(Indexed("albumArtist", index), track.AlbumArtist));
        }

        if (track.TrackNumber is > 0 and int number)
        {
            parameters.Add(new(Indexed("trackNumber", index), number.ToString(CultureInfo.InvariantCulture)));
        }

        if (track.Duration is { } duration && duration > TimeSpan.Zero)
        {
            parameters.Add(new(
                Indexed("duration", index),
                ((long)Math.Round(duration.TotalSeconds)).ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static string Indexed(string name, int? index)
    {
        return index is { } value ? string.Create(CultureInfo.InvariantCulture, $"{name}[{value}]") : name;
    }

    private ScrobbleSendStatus ToSendStatus(LastFmResponse response)
    {
        return response.Kind switch
        {
            LastFmResponseKind.Success => ScrobbleSendStatus.Accepted,
            LastFmResponseKind.Error => response.ErrorCode switch
            {
                InvalidSessionKeyError => ScrobbleSendStatus.Unauthorized,
                InvalidParametersError => ScrobbleSendStatus.Rejected,
                OperationFailedError or ServiceOfflineError or TemporarilyUnavailableError or RateLimitError =>
                    ScrobbleSendStatus.Unavailable,
                _ => LogConfigurationError(response.ErrorCode),
            },
            _ => ScrobbleSendStatus.Unavailable,
        };
    }

    /// <summary>
    /// Other errors concern the API account of WaveMP (invalid API key or signature, suspended key) or are unknown;
    /// the scrobbles are kept and sent again later.
    /// </summary>
    private ScrobbleSendStatus LogConfigurationError(int? code)
    {
        logger.LogWarning(
            "Last.fm rejected the request with error {ErrorCode}; check LASTFM_API_KEY and LASTFM_SHARED_SECRET",
            code);
        return ScrobbleSendStatus.Unavailable;
    }

    private async Task<LastFmResponse> SendAsync(
        string method,
        IReadOnlyList<KeyValuePair<string, string>> methodParameters,
        CancellationToken cancellationToken)
    {
        ScrobblingOptions settings = options.Value;
        if (!settings.IsLastFmConfigured)
        {
            logger.LogWarning("Last.fm is not configured; set LASTFM_API_KEY and LASTFM_SHARED_SECRET");
            return LastFmResponse.Unavailable;
        }

        List<KeyValuePair<string, string>> parameters =
        [
            new("method", method),
            new("api_key", settings.LastFmApiKey!),
            .. methodParameters,
        ];
        parameters.Add(new("api_sig", Sign(parameters, settings.LastFmSharedSecret!)));
        parameters.Add(new("format", "json"));

        try
        {
            using FormUrlEncodedContent content = new(parameters);
            using HttpResponseMessage response = await http.PostAsync(string.Empty, content, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonElement? json = Parse(body);
            if (json is { ValueKind: JsonValueKind.Object } root
                && root.TryGetProperty("error", out JsonElement error)
                && error.TryGetInt32(out int code))
            {
                logger.LogInformation("Last.fm answered {Method} with error {ErrorCode}", method, code);
                return new LastFmResponse(LastFmResponseKind.Error, root, code);
            }

            if (response.IsSuccessStatusCode && json is { ValueKind: JsonValueKind.Object } success)
            {
                return new LastFmResponse(LastFmResponseKind.Success, success, null);
            }

            logger.LogWarning("Last.fm answered {Method} with {StatusCode}", method, (int)response.StatusCode);
            return LastFmResponse.Unavailable;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Last.fm cannot be reached: {Reason}", exception.Message);
            return LastFmResponse.Unavailable;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Last.fm did not answer in time");
            return LastFmResponse.Unavailable;
        }
    }

    private static JsonElement? Parse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        // Last.fm writes some numbers as strings.
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out int number) => number,
            JsonValueKind.String when int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed) => parsed,
            _ => null,
        };
    }

    private enum LastFmResponseKind
    {
        Success,
        Error,
        Unavailable,
    }

    private sealed record LastFmResponse(LastFmResponseKind Kind, JsonElement Body, int? ErrorCode)
    {
        public static LastFmResponse Unavailable { get; } = new(LastFmResponseKind.Unavailable, default, null);
    }
}
