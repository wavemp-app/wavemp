namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// The result of publishing lyrics to LRCLIB.
/// </summary>
/// <param name="Status">The answer of LRCLIB.</param>
/// <param name="RetryAfter">How long LRCLIB asks to wait, for <see cref="LrclibPublishStatus.RateLimited"/>.</param>
public sealed record LrclibPublishResult(LrclibPublishStatus Status, TimeSpan? RetryAfter = null);
