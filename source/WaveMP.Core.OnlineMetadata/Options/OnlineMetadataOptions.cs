namespace WaveMP.Core.OnlineMetadata.Options;

/// <summary>
/// Technical settings of the online search for album info (configuration section <c>OnlineMetadata</c>, e.g.
/// <c>OnlineMetadata__RequestTimeout</c>). Whether the search is on, the addresses of the services and the time between
/// two requests to MusicBrainz are settings the admin changes in the app.
/// </summary>
public sealed class OnlineMetadataOptions
{
    public const string SectionName = "OnlineMetadata";

    /// <summary>
    /// How long a request to MusicBrainz or the Cover Art Archive may take.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How often a request is sent when the service answers 503 (MusicBrainz does so when it gets too many requests),
    /// before the search gives up.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Wait before the second attempt after a 503, unless the service names one (Retry-After); it doubles with every
    /// further attempt.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Longest wait before another attempt, also when the service asks for a longer one, so an admin does not wait
    /// for minutes.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How many results MusicBrainz is asked for per search.
    /// </summary>
    public int SearchLimit { get; set; } = 25;

    /// <summary>
    /// How many matches a search returns at most, the best first.
    /// </summary>
    public int MaxMatches { get; set; } = 10;

    /// <summary>
    /// How many releases of one recording become matches of a song at most.
    /// </summary>
    public int MaxReleasesPerRecording { get; set; } = 3;

    /// <summary>
    /// Largest cover that is downloaded, in bytes.
    /// </summary>
    public long MaxCoverBytes { get; set; } = 20 * 1024 * 1024;
}
