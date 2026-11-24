namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// A GET request to an online service.
/// </summary>
/// <param name="Url">The address of the request.</param>
/// <param name="Gate">The switch the request depends on.</param>
/// <param name="MaxBytes">Largest content that is read; larger answers count as unavailable.</param>
internal sealed record OnlineRequest(Uri Url, OnlineGate Gate, long MaxBytes)
{
    /// <summary>
    /// Whether the request waits for its turn in the queue of MusicBrainz.
    /// </summary>
    public bool Queued { get; init; }

    /// <summary>
    /// The service whose requests are sent one after the other; <c>null</c> for none.
    /// </summary>
    public OnlineLane? Lane { get; init; }

    /// <summary>
    /// Shortest wait before another attempt after the service was busy (429 or 503), also when it names a shorter one.
    /// </summary>
    public TimeSpan MinRetryDelay { get; init; }

    /// <summary>
    /// Further headers of the request, e.g. an API key. They are sent to the host of <see cref="Url"/> only, never
    /// to the host of a redirect, and never written to the log.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; } = [];
}
