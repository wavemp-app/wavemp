using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Calls the API of LRCLIB over HTTP: <c>GET api/get</c> for the exact lookup, <c>GET api/get/{id}</c> for a chosen
/// track, <c>GET api/search</c> for the search by fields and the free-text search, and
/// <c>POST api/request-challenge</c> and <c>POST api/publish</c> to publish lyrics. Every request names WaveMP and its
/// version in the User-Agent, as LRCLIB asks clients to, and waits for its turn at the
/// <see cref="LrclibRequestPacer"/>.
/// </summary>
internal sealed class LrclibClient(
    HttpClient http,
    LrclibRequestPacer pacer,
    TimeSpan challengeTimeout,
    ILogger<LrclibClient> logger) : ILrclibClient
{
    /// <summary>
    /// The header that carries the solved challenge of a publication.
    /// </summary>
    internal const string PublishTokenHeader = "X-Publish-Token";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The User-Agent of the requests, e.g. <c>WaveMP/1.0.42 (https://github.com/wavemp-app/wavemp)</c>.
    /// </summary>
    public static ProductInfoHeaderValue[] UserAgent { get; } =
    [
        new("WaveMP", Version()),
        new("(https://github.com/wavemp-app/wavemp)"),
    ];

    public Task<LrclibResponse> GetAsync(LrclibQuery query, CancellationToken cancellationToken = default)
    {
        if (!query.IsComplete)
        {
            throw new ArgumentException("The exact lookup needs title, artist, album and duration.", nameof(query));
        }

        string url = "api/get?" + QueryString(
            ("track_name", query.TrackName),
            ("artist_name", query.ArtistName),
            ("album_name", query.AlbumName),
            ("duration", Seconds(query.Duration!.Value)));
        return SendAsync(url, single: true, cancellationToken);
    }

    public Task<LrclibResponse> SearchAsync(LrclibQuery query, CancellationToken cancellationToken = default)
    {
        string url = "api/search?" + QueryString(("track_name", query.TrackName), ("artist_name", query.ArtistName));
        return SendAsync(url, single: false, cancellationToken);
    }

    public Task<LrclibResponse> SearchTextAsync(string text, CancellationToken cancellationToken = default)
    {
        string url = "api/search?" + QueryString(("q", text));
        return SendAsync(url, single: false, cancellationToken);
    }

    public Task<LrclibResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        string url = "api/get/" + id.ToString(CultureInfo.InvariantCulture);
        return SendAsync(url, single: true, cancellationToken);
    }

    public async Task<LrclibPublishResult> PublishAsync(
        LrclibPublication publication,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await pacer.WaitAsync(cancellationToken);
            LrclibChallengeResponse? challenge;
            using (HttpResponseMessage response = await http.PostAsync(
                "api/request-challenge",
                content: null,
                cancellationToken))
            {
                if (Failure(response) is { } failure)
                {
                    return failure;
                }

                challenge = await response.Content.ReadFromJsonAsync<LrclibChallengeResponse>(
                    _jsonOptions,
                    cancellationToken);
            }

            if (challenge?.Prefix is not { } prefix || challenge.Target is not { } target)
            {
                logger.LogWarning("LRCLIB sent no challenge to publish lyrics");
                return new LrclibPublishResult(LrclibPublishStatus.Unavailable);
            }

            long nonce = await SolveAsync(prefix, target, cancellationToken);
            await pacer.WaitAsync(cancellationToken);
            using HttpRequestMessage request = new(HttpMethod.Post, "api/publish")
            {
                Content = JsonContent.Create(
                    new LrclibPublishRequest(
                        publication.TrackName,
                        publication.ArtistName,
                        publication.AlbumName,
                        Math.Round(publication.Duration.TotalSeconds),
                        publication.PlainLyrics,
                        publication.SyncedLyrics),
                    options: _jsonOptions),
            };
            request.Headers.Add(PublishTokenHeader, prefix + ":" + nonce.ToString(CultureInfo.InvariantCulture));
            using HttpResponseMessage published = await http.SendAsync(request, cancellationToken);
            if (published.StatusCode == HttpStatusCode.BadRequest)
            {
                logger.LogWarning(
                    "LRCLIB refused the published lyrics: {Answer}",
                    await published.Content.ReadAsStringAsync(cancellationToken));
                return new LrclibPublishResult(LrclibPublishStatus.Rejected);
            }

            return Failure(published) ?? new LrclibPublishResult(LrclibPublishStatus.Published);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("LRCLIB cannot be reached: {Reason}", exception.Message);
            return new LrclibPublishResult(LrclibPublishStatus.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("LRCLIB did not answer in time, or its challenge took too long");
            return new LrclibPublishResult(LrclibPublishStatus.Unavailable);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            logger.LogWarning(exception, "The challenge of LRCLIB cannot be read");
            return new LrclibPublishResult(LrclibPublishStatus.Unavailable);
        }
    }

    /// <summary>
    /// The query string of the parameters that have a value, encoded.
    /// </summary>
    internal static string QueryString(params (string Name, string? Value)[] parameters)
    {
        StringBuilder builder = new();
        foreach ((string name, string? value) in parameters)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            builder.Append(name).Append('=').Append(Uri.EscapeDataString(value.Trim()));
        }

        return builder.ToString();
    }

    private static string Seconds(TimeSpan duration)
    {
        return ((long)Math.Round(duration.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    }

    private async Task<LrclibResponse> SendAsync(string url, bool single, CancellationToken cancellationToken)
    {
        await pacer.WaitAsync(cancellationToken);
        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return LrclibResponse.NotFound;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogInformation("LRCLIB refuses requests for a while (429)");
                return LrclibResponse.RateLimited(RetryAfter(response));
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("LRCLIB answered {StatusCode}", (int)response.StatusCode);
                return LrclibResponse.Unavailable;
            }

            if (single)
            {
                LrclibRecord? record = await response.Content.ReadFromJsonAsync<LrclibRecord>(
                    _jsonOptions,
                    cancellationToken);
                return record is null ? LrclibResponse.NotFound : LrclibResponse.Found([record]);
            }

            LrclibRecord[]? records = await response.Content.ReadFromJsonAsync<LrclibRecord[]>(
                _jsonOptions,
                cancellationToken);
            return LrclibResponse.Found(records ?? []);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("LRCLIB cannot be reached: {Reason}", exception.Message);
            return LrclibResponse.Unavailable;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("LRCLIB did not answer in time");
            return LrclibResponse.Unavailable;
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The answer of LRCLIB cannot be read");
            return LrclibResponse.Unavailable;
        }
    }

    /// <summary>
    /// Solves the challenge on the thread pool, at most for the configured time.
    /// </summary>
    private async Task<long> SolveAsync(string prefix, string target, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(challengeTimeout);
        long started = Stopwatch.GetTimestamp();
        long nonce = await Task.Run(() => LrclibChallenge.Solve(prefix, target, timeout.Token), timeout.Token);
        logger.LogInformation("Solved the challenge of LRCLIB in {Elapsed}", Stopwatch.GetElapsedTime(started));
        return nonce;
    }

    /// <summary>
    /// The result of a failed answer to a publication, or <c>null</c> if it succeeded.
    /// </summary>
    private LrclibPublishResult? Failure(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogInformation("LRCLIB refuses requests for a while (429)");
            return new LrclibPublishResult(LrclibPublishStatus.RateLimited, RetryAfter(response));
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("LRCLIB answered {StatusCode}", (int)response.StatusCode);
            return new LrclibPublishResult(LrclibPublishStatus.Unavailable);
        }

        return null;
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        return retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null;
    }

    private static string Version()
    {
        string version = typeof(LrclibClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";

        // The source revision after "+" is no part of the product version.
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? version[..plus] : version;
    }
}
