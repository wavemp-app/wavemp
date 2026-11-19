using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Sends the GET requests of the online search: never while the search is switched off, requests to MusicBrainz
/// through the <see cref="MusicBrainzRequestQueue"/>, and again with a growing wait when the service answers 503. The
/// request carries the User-Agent of <see cref="OnlineUserAgent"/>. Never throws for network errors; the answer
/// says what happened.
/// </summary>
internal sealed class OnlineRequestSender(
    HttpClient http,
    MusicBrainzRequestQueue queue,
    ISettingsSnapshot<OnlineMetadataSettings> settings,
    IOptions<OnlineMetadataOptions> options,
    TimeProvider timeProvider,
    ILogger<OnlineRequestSender> logger)
{
    /// <summary>
    /// The content of the answer to a GET request of <paramref name="url"/>, at most <paramref name="maxBytes"/>
    /// long.
    /// </summary>
    /// <param name="url">The address of the request.</param>
    /// <param name="queued">Whether the request waits for its turn in the queue of MusicBrainz.</param>
    /// <param name="maxBytes">Largest content that is read; larger answers count as unavailable.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    public async Task<OnlineResponse<byte[]>> GetAsync(
        Uri url,
        bool queued,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        OnlineMetadataOptions current = options.Value;
        int attempts = Math.Max(1, current.MaxAttempts);
        for (int attempt = 1; ; attempt++)
        {
            if (!settings.Current.Enabled)
            {
                return OnlineResponse<byte[]>.Failed(OnlineStatus.Disabled);
            }

            if (queued)
            {
                await queue.WaitAsync(cancellationToken);
            }

            TimeSpan? retryAfter;
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                foreach (ProductInfoHeaderValue product in OnlineUserAgent.Products)
                {
                    request.Headers.UserAgent.Add(product);
                }

                using HttpResponseMessage response = await http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return OnlineResponse<byte[]>.Failed(OnlineStatus.NotFound);
                }

                if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
                {
                    return await ReadAsync(url, response, maxBytes, cancellationToken);
                }

                retryAfter = RetryAfter(response);
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning("{Host} cannot be reached: {Reason}", url.Host, exception.Message);
                return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("{Host} did not answer in time", url.Host);
                return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
            }

            if (attempt >= attempts)
            {
                logger.LogWarning("{Host} refused the request {Attempts} times (503)", url.Host, attempts);
                return OnlineResponse<byte[]>.Failed(OnlineStatus.Unavailable);
            }

            TimeSpan wait = RetryDelay(attempt, retryAfter, current.RetryDelay, current.MaxRetryDelay);
            logger.LogInformation("{Host} answered 503; trying again in {Wait}", url.Host, wait);
            await Task.Delay(wait, timeProvider, cancellationToken);
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

    private async Task<OnlineResponse<byte[]>> ReadAsync(
        Uri url,
        HttpResponseMessage response,
        long maxBytes,
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
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using MemoryStream content = new();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
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
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
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
}
