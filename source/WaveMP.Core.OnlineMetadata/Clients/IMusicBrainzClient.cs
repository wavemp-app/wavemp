namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The search of the MusicBrainz API (read-only, no API key) at the address of the settings. Requests wait for their
/// turn in the queue of MusicBrainz and are sent only while the online search is switched on. Never throws for network
/// errors; the answer says what happened.
/// </summary>
public interface IMusicBrainzClient
{
    /// <summary>
    /// Recordings that match the query (see <see cref="MusicBrainzQuery.Recording"/>), the most relevant first.
    /// </summary>
    Task<OnlineResponse<IReadOnlyList<MusicBrainzRecording>>> SearchRecordingsAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases that match the query (see <see cref="MusicBrainzQuery.Release"/>), the most relevant first.
    /// </summary>
    Task<OnlineResponse<IReadOnlyList<MusicBrainzRelease>>> SearchReleasesAsync(
        string query,
        CancellationToken cancellationToken = default);
}
