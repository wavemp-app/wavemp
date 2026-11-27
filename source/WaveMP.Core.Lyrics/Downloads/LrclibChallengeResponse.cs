namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// The challenge LRCLIB asks to solve before it takes published lyrics, as its API returns it.
/// </summary>
internal sealed record LrclibChallengeResponse
{
    public string? Prefix { get; init; }

    /// <summary>
    /// The largest allowed hash as hexadecimal text.
    /// </summary>
    public string? Target { get; init; }
}
