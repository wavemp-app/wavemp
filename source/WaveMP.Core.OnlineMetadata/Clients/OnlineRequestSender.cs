using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Sends the GET requests to the online services. Every request depends on a switch (<see cref="OnlineGate"/>) that is
/// checked immediately before every request that leaves, so nothing is sent while the feature is off. Requests to
/// MusicBrainz wait in the <see cref="MusicBrainzRequestQueue"/>, requests of a <see cref="OnlineLane"/> are sent one
/// after the other, and a busy service (503, for artist pictures also 429) is asked again after a growing wait or the
/// one it names. The request carries the User-Agent of <see cref="OnlineUserAgent"/>. Requests of the artist picture
/// lookup use a client of their own that never follows redirects on its own: up to <see cref="MaxRedirects"/>
/// redirects are followed here, and only to addresses of the <see cref="OnlineHostAllowlist"/>. Never throws for
/// network errors; the answer says what happened. Logs name the host only, never the address or a header.
/// </summary>
internal sealed class OnlineRequestSender(
    HttpClient http,
    HttpClient artistHttp,
    MusicBrainzRequestQueue queue,
    ISettingsSnapshot<OnlineMetadataSettings> settings,
    ISettingsSnapshot<ArtistPictureSettings> artistSettings,
    IOptions<OnlineMetadataOptions> options,
    IOptions<ArtistLookupClientOptions> artistOptions,
    TimeProvider timeProvider,
    ILogger<OnlineRequestSender> logger)
{
    /// <summary>
    /// Most redirects one request of the artist picture lookup follows.
    /// </summary>
    internal const int MaxRedirects = 3;

    private readonly SemaphoreSlim[] _lanes = [.. Enum.GetValues<OnlineLane>().Select(_ => new SemaphoreSlim(1, 1))];

    /// <summary>
    /// The content of the answer to a GET request of the online search for album info, at most
    /// <paramref name="maxBytes"/> long.
    /// </summary>
    /// <param name="url">The address of the request.</param>
    /// <param name="queued">Whether the request waits for its turn in the queue of MusicBrainz.</param>
    /// <param name="maxBytes">Largest content that is read; larger answers count as unavailable.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    public Task<OnlineResponse<byte[]>> GetAsync(
        Uri url,
        bool queued,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            new OnlineRequest(url, OnlineGate.AlbumSearch, maxBytes) { Queued = queued },
            cancellationToken);
    }

    /// <summary>
    /// The content of the answer to the request.
    /// </summary>
    public async Task<OnlineResponse<byte[]>> SendAsync(OnlineRequest request, CancellationToken cancellationToken)
    {
        if (!IsOpen(request.Gate))
        {
            return OnlineResponse<byte[]>.Failed(OnlineStatus.Disabled);
        }

        SemaphoreSlim? lane = request.Lane is { } named ? _lanes[(int)named] : null;
        if (lane is null)
        {
            return await SendInLaneAsync(request, cancellationToken);
        }

        await lane.WaitAsync(cancellationToken);
        try
        {
            return await SendInLaneAsync(request, cancellationToken);
        }
        finally
        {
            lane.Release();
        }
    }

    /// <summary>
    /// The wait after the attempt <paramref name="attempt"/> (1 for the first): the one the service names, otherwise
    /// <paramref name="baseDelay"/> doubled with every attempt; never longer than <paramref name="maxDelay"/>.
    /// </summary>
    internal static TimeSpan RetryDelay(int attempt, TimeSpan? retryAfter, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        TimeSpan wait = retryAfter is { } named && named > TimeSpan.Zero
            ? named
            : baseDelay * Math.Pow(2, Math.Max(0, attempt - 1));
        return wait > maxDelay ? maxDelay : wait;
    }

    private bool IsOpen(OnlineGate gate)
    {
        return gate switch
        {
            OnlineGate.AlbumSearch => settings.Current.Enabled,
            OnlineGate.ArtistPictures => artistSettings.Current.OnlineLookupEnabled,
            _ => false,
        };
    }

    private async Task<OnlineResponse<byte[]>> SendInLaneAsync(
        OnlineRequest request,
        CancellationToken cancellationToken)
    {
        bool artist = request.Gate == OnlineGate.ArtistPictures;
        OnlineMetadataOptions current = options.Value;
        int attempts = Math.Max(1, current.MaxAttempts);
        Uri url = request.Url;
        int redirects = 0;
        for (int attempt = 1; ; attempt++)
        {
            Exchange exchange;
            do
            {
                // Checked before every request that leaves, also after a wait or a redirect.
                if (!IsOpen(request.Gate))
                {
                    return OnlineResponse<byte[]>.Failed(OnlineStatus.Disabled);
                }

                if (artist && !OnlineHostAllowlist.IsAllowed(url, settings.Current.MusicBrainzUrl))
                {
                    logger.LogWarning(
                        "Refused a request of the artist picture lookup to {Host}: not an allowed https address",
                        url.Host);
                    return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
                }

                exchange = await ExchangeAsync(request, url, artist, cancellationToken);
                if (exchange.Redirect is { } next)
                {
                    if (++redirects > MaxRedirects)
                    {
                        logger.LogWarning("{Host} redirected the request too often", url.Host);
                        return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
                    }

                    url = next;
                }
            }
            while (exchange.Redirect is not null);

            if (exchange.Response is { } done)
            {
                return done;
            }

            if (attempt >= attempts)
            {
                logger.LogWarning("{Host} refused the request {Attempts} times as busy", url.Host, attempts);
                return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
            }

            if (artist && exchange.RetryAfter > current.MaxRetryDelay)
            {
                // Asking again before the named time would ignore the service; the admin tries again later.
                logger.LogWarning("{Host} asks to wait {Wait}; giving up", url.Host, exchange.RetryAfter);
                return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
            }

            TimeSpan wait = RetryDelay(attempt, exchange.RetryAfter, current.RetryDelay, current.MaxRetryDelay);
            if (wait < request.MinRetryDelay)
            {
                wait = request.MinRetryDelay;
            }

            logger.LogInformation("{Host} is busy; trying again in {Wait}", url.Host, wait);
            await Task.Delay(wait, timeProvider, cancellationToken);
        }
    }

    /// <summary>
    /// Sends one request: the final answer, a redirect to follow or a busy service.
    /// </summary>
    private async Task<Exchange> ExchangeAsync(
        OnlineRequest request,
        Uri url,
        bool artist,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource? timeout = artist
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        timeout?.CancelAfter(artistOptions.Value.RequestTimeout);
        CancellationToken token = timeout?.Token ?? cancellationToken;
        try
        {
            using HttpRequestMessage message = Message(request, url);
            using HttpResponseMessage response = await SendAsync(
                artist ? artistHttp : http,
                message,
                request.Queued,
                token);
            if (artist && IsRedirect(response.StatusCode))
            {
                if (response.Headers.Location is not { } location)
                {
                    logger.LogWarning("{Host} redirected the request without a target", url.Host);
                    return Exchange.Done(OnlineStatus.Unavailable);
                }

                return new Exchange(null, new Uri(url, location), null);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Exchange.Done(OnlineStatus.NotFound);
            }

            if (IsBusy(response.StatusCode, artist))
            {
                return new Exchange(null, null, RetryAfter(response));
            }

            return new Exchange(await ReadAsync(url, response, request.MaxBytes, token, cancellationToken), null, null);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("{Host} cannot be reached: {Reason}", url.Host, exception.Message);
            return Exchange.Done(OnlineStatus.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("{Host} did not answer in time", url.Host);
            return Exchange.Done(OnlineStatus.Unavailable);
        }
    }

    /// <summary>
    /// The request message; the further headers only go to the host of the original address.
    /// </summary>
    private static HttpRequestMessage Message(OnlineRequest request, Uri url)
    {
        HttpRequestMessage message = new(HttpMethod.Get, url);
        foreach (ProductInfoHeaderValue product in OnlineUserAgent.Products)
        {
            message.Headers.UserAgent.Add(product);
        }

        if (string.Equals(url.IdnHost, request.Url.IdnHost, StringComparison.OrdinalIgnoreCase))
        {
            foreach ((string name, string value) in request.Headers)
            {
                message.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return message;
    }

    private static bool IsRedirect(HttpStatusCode status)
    {
        return status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
    }

    private static bool IsBusy(HttpStatusCode status, bool artist)
    {
        return status == HttpStatusCode.ServiceUnavailable || (artist && status == HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Sends <paramref name="request"/> and returns once the headers of the answer are there; a queued request holds
    /// its turn in the queue of MusicBrainz until then, so requests start strictly one after the other.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpRequestMessage request,
        bool queued,
        CancellationToken cancellationToken)
    {
        if (!queued)
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        using IDisposable turn = await queue.EnterAsync(cancellationToken);
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private async Task<OnlineResponse<byte[]>> ReadAsync(
        Uri url,
        HttpResponseMessage response,
        long maxBytes,
        CancellationToken readToken,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("{Host} answered {StatusCode}", url.Host, (int)response.StatusCode);
            return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
        }

        if (response.Content.Headers.ContentLength > maxBytes)
        {
            logger.LogWarning("The answer of {Host} is larger than {MaxBytes} bytes", url.Host, maxBytes);
            return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
        }

        try
        {
            await using Stream stream = await response.Content.ReadAsStreamAsync(readToken);
            using MemoryStream content = new();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, readToken)) > 0)
            {
                if (content.Length + read > maxBytes)
                {
                    logger.LogWarning("The answer of {Host} is larger than {MaxBytes} bytes", url.Host, maxBytes);
                    return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
                }

                content.Write(buffer, 0, read);
            }

            return OnlineResponse<byte[]>.Found(content.ToArray());
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning("The answer of {Host} broke off: {Reason}", url.Host, exception.Message);
            return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
        }
    }

    private TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        return retryAfter?.Date is { } date ? date - timeProvider.GetUtcNow() : null;
    }

    /// <summary>
    /// Outcome of one request: the final answer, the target of a redirect, or a busy service with the wait it named.
    /// </summary>
    private sealed record Exchange(OnlineResponse<byte[]>? Response, Uri? Redirect, TimeSpan? RetryAfter)
    {
        public static Exchange Done(OnlineStatus status)
        {
            return new Exchange(OnlineResponse<byte[]>.Failed(status), null, null);
        }
    }
}
