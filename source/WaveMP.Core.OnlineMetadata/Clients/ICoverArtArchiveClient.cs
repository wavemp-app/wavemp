namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The front covers of releases in the Cover Art Archive at the address of the settings. Sent only while the online
/// search is switched on. Never throws for network errors; the answer says what happened.
/// </summary>
public interface ICoverArtArchiveClient
{
    /// <summary>
    /// The front cover of a release in the size, as delivered (usually JPEG); <see cref="OnlineStatus.NotFound"/> if
    /// the release has none.
    /// </summary>
    Task<OnlineResponse<byte[]>> GetFrontCoverAsync(
        Guid releaseId,
        CoverArtSize size,
        CancellationToken cancellationToken = default);
}
