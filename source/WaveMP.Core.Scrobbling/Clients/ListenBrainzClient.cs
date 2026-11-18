using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// Calls the ListenBrainz API over HTTP. Every request carries the user token in the header
/// <c>Authorization: Token ...</c>.
/// </summary>
internal sealed class ListenBrainzClient(HttpClient http, ILogger<ListenBrainzClient> logger) : IListenBrainzClient
{
    private const string TokenScheme = "Token";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<ScrobbleAuthorization> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "1/validate-token");
        request.Headers.Authorization = new AuthenticationHeaderValue(TokenScheme, token);
        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                return ScrobbleAuthorization.Invalid;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("ListenBrainz answered validate-token with {StatusCode}", (int)response.StatusCode);
                return ScrobbleAuthorization.Unavailable;
            }

            ValidateTokenResponse? answer = await response.Content.ReadFromJsonAsync<ValidateTokenResponse>(
                _jsonOptions,
                cancellationToken);
            return answer is { Valid: true, UserName: { Length: > 0 } name }
                ? ScrobbleAuthorization.Authorized(name, token)
                : ScrobbleAuthorization.Invalid;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("ListenBrainz cannot be reached: {Reason}", exception.Message);
            return ScrobbleAuthorization.Unavailable;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("ListenBrainz did not answer in time");
            return ScrobbleAuthorization.Unavailable;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The answer of ListenBrainz cannot be read");
            return ScrobbleAuthorization.Unavailable;
        }
    }

    public Task<ScrobbleSendStatus> SubmitPlayingNowAsync(
        string token,
        ScrobbleTrack track,
        CancellationToken cancellationToken = default)
    {
        SubmitListens body = new("playing_now", [new Listen(null, Metadata(track))]);
        return SubmitAsync(token, body, cancellationToken);
    }

    public Task<ScrobbleSendStatus> SubmitListensAsync(
        string token,
        IReadOnlyList<ScrobblePlay> plays,
        CancellationToken cancellationToken = default)
    {
        if (plays.Count == 0)
        {
            return Task.FromResult(ScrobbleSendStatus.Accepted);
        }

        SubmitListens body = new(
            plays.Count == 1 ? "single" : "import",
            [.. plays.Select(play => new Listen(play.PlayedAt.ToUnixTimeSeconds(), Metadata(play.Track)))]);
        return SubmitAsync(token, body, cancellationToken);
    }

    private static TrackMetadata Metadata(ScrobbleTrack track)
    {
        return new TrackMetadata(
            track.Artist,
            track.Title,
            string.IsNullOrWhiteSpace(track.Album) ? null : track.Album,
            new AdditionalInfo(
                track.Duration is { } duration && duration > TimeSpan.Zero
                    ? (long)Math.Round(duration.TotalMilliseconds)
                    : null,
                track.TrackNumber is > 0 ? track.TrackNumber : null,
                string.IsNullOrWhiteSpace(track.AlbumArtist) ? null : track.AlbumArtist,
                ScrobbleHttp.ClientName,
                ScrobbleHttp.ClientName,
                ScrobbleHttp.Version));
    }

    private async Task<ScrobbleSendStatus> SubmitAsync(
        string token,
        SubmitListens body,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "1/submit-listens")
        {
            Content = JsonContent.Create(body, options: _jsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TokenScheme, token);
        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ScrobbleSendStatus.Accepted;
            }

            int status = (int)response.StatusCode;
            logger.LogInformation(
                "ListenBrainz answered submit-listens ({Type}) with {StatusCode}",
                body.ListenType,
                status);
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ScrobbleSendStatus.Unauthorized,
                HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout => ScrobbleSendStatus.Unavailable,
                _ when status >= 500 => ScrobbleSendStatus.Unavailable,
                _ => ScrobbleSendStatus.Rejected,
            };
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("ListenBrainz cannot be reached: {Reason}", exception.Message);
            return ScrobbleSendStatus.Unavailable;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("ListenBrainz did not answer in time");
            return ScrobbleSendStatus.Unavailable;
        }
    }

    private sealed record ValidateTokenResponse(
        [property: JsonPropertyName("valid")] bool Valid,
        [property: JsonPropertyName("user_name")] string? UserName);

    private sealed record SubmitListens(
        [property: JsonPropertyName("listen_type")] string ListenType,
        [property: JsonPropertyName("payload")] IReadOnlyList<Listen> Payload);

    private sealed record Listen(
        [property: JsonPropertyName("listened_at")] long? ListenedAt,
        [property: JsonPropertyName("track_metadata")] TrackMetadata TrackMetadata);

    private sealed record TrackMetadata(
        [property: JsonPropertyName("artist_name")] string ArtistName,
        [property: JsonPropertyName("track_name")] string TrackName,
        [property: JsonPropertyName("release_name")] string? ReleaseName,
        [property: JsonPropertyName("additional_info")] AdditionalInfo AdditionalInfo);

    private sealed record AdditionalInfo(
        [property: JsonPropertyName("duration_ms")] long? DurationMs,
        [property: JsonPropertyName("tracknumber")] int? TrackNumber,
        [property: JsonPropertyName("release_artist_name")] string? ReleaseArtistName,
        [property: JsonPropertyName("media_player")] string MediaPlayer,
        [property: JsonPropertyName("submission_client")] string SubmissionClient,
        [property: JsonPropertyName("submission_client_version")] string SubmissionClientVersion);
}
