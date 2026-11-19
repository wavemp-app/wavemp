using WaveMP.Core.OnlineMetadata.Clients;

namespace WaveMP.Core.OnlineMetadata.Searches;

/// <summary>
/// Searches songs and albums of the library in MusicBrainz by their tags (title, artist, album) and duration, and
/// rates every match by how well it agrees. Nothing is changed. While the online search is switched off, nothing is
/// sent.
/// </summary>
public interface IOnlineMetadataSearchService
{
    /// <summary>
    /// The recordings that may be the song, on their releases, the best first.
    /// </summary>
    Task<TrackSearchResult> SearchTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The releases that may be the album, the best first.
    /// </summary>
    Task<AlbumSearchResult> SearchAlbumAsync(Guid albumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The front cover of a release from the Cover Art Archive, for the preview of a match.
    /// </summary>
    Task<OnlineCoverPreview> GetCoverAsync(
        Guid releaseId,
        CoverArtSize size,
        CancellationToken cancellationToken = default);
}
