namespace WaveMP.Core.Lyrics.Lookups;

/// <summary>
/// The stored results of the online lookup of lyrics. The download job looks up a track whose lookup found no lyrics
/// (not found or instrumental) again only after <see cref="Options.LyricsOptions.LookUpAgainAfter"/>; admins forget
/// these results, for all tracks or for one track, so the next run of the job looks the tracks up again at once.
/// </summary>
public interface ILyricsLookupService
{
    /// <summary>
    /// How many tracks have a stored lookup that found no lyrics.
    /// </summary>
    Task<LyricsLookupSummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The stored result of the lookup of a track.
    /// </summary>
    Task<LyricsLookupResult> GetAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the lookups of all tracks that found no lyrics (not found or instrumental). Results that found lyrics
    /// are kept, also lyrics stored in the database. Returns the number of forgotten lookups.
    /// </summary>
    Task<int> ForgetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the lookup of a track if it found no lyrics (not found or instrumental).
    /// </summary>
    Task<LyricsLookupForgetOutcome> ForgetAsync(Guid trackId, CancellationToken cancellationToken = default);
}
