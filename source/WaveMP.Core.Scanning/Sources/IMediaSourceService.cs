namespace WaveMP.Core.Scanning.Sources;

/// <summary>
/// Management of the media sources by the admin. A new or reactivated source and a source with a new folder are
/// scanned right away (or after a running scan); the file watcher follows every change.
/// </summary>
public interface IMediaSourceService
{
    /// <summary>
    /// All sources, sorted by name.
    /// </summary>
    Task<IReadOnlyList<MediaSourceOverview>> ListAsync(CancellationToken cancellationToken = default);

    Task<MediaSourceOverview?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MediaSourceResult> CreateAsync(
        CreateMediaSourceCommand command,
        CancellationToken cancellationToken = default);

    Task<MediaSourceResult> UpdateAsync(
        Guid id,
        UpdateMediaSourceCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a source with its tracks and files from the library; the files on disk stay. Returns the reason if
    /// nothing was removed.
    /// </summary>
    Task<MediaSourceError?> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
