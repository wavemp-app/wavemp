namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// The answer of LRCLIB: the tracks it found, or why there are none.
/// </summary>
/// <param name="Status">The kind of answer.</param>
/// <param name="Records">The tracks; empty unless <see cref="LrclibStatus.Found"/>.</param>
/// <param name="RetryAfter">How long LRCLIB asks to wait before the next request, if it says so.</param>
public sealed record LrclibResponse(LrclibStatus Status, IReadOnlyList<LrclibRecord> Records, TimeSpan? RetryAfter = null)
{
    public static LrclibResponse NotFound { get; } = new(LrclibStatus.NotFound, []);

    public static LrclibResponse Unavailable { get; } = new(LrclibStatus.Unavailable, []);

    public static LrclibResponse Found(IReadOnlyList<LrclibRecord> records)
    {
        return records.Count == 0 ? NotFound : new LrclibResponse(LrclibStatus.Found, records);
    }

    public static LrclibResponse RateLimited(TimeSpan? retryAfter)
    {
        return new LrclibResponse(LrclibStatus.RateLimited, [], retryAfter);
    }
}
