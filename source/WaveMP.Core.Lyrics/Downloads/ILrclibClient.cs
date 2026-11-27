namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// The API of the online lyrics database LRCLIB (lrclib.net). Requests are spaced by the request interval of the
/// lyrics options. Never throws for network errors; the answer says what happened.
/// </summary>
public interface ILrclibClient
{
    /// <summary>
    /// The track that matches title, artist, album and duration (LRCLIB allows two seconds of difference); the query
    /// must be complete (<see cref="LrclibQuery.IsComplete"/>).
    /// </summary>
    Task<LrclibResponse> GetAsync(LrclibQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks whose title (and artist, if known) match the query, at most 20.
    /// </summary>
    Task<LrclibResponse> SearchAsync(LrclibQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks whose title, artist or album contain the words of the text (free-text search), at most 20.
    /// </summary>
    Task<LrclibResponse> SearchTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// The track of LRCLIB with the id.
    /// </summary>
    Task<LrclibResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes lyrics through the publish API of LRCLIB: asks for a challenge, solves it and sends the lyrics with
    /// the solution.
    /// </summary>
    Task<LrclibPublishResult> PublishAsync(
        LrclibPublication publication,
        CancellationToken cancellationToken = default);
}
