namespace WaveMP.Core.Metadata.Sources;

/// <summary>
/// The media sources of the library, kept in memory: loaded on start and again after every change of a source.
/// </summary>
public interface IMediaSourceCatalog
{
    /// <summary>
    /// Raised after the sources were loaded again with <see cref="RefreshAsync"/>.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// All sources, sorted by name; loaded on first use.
    /// </summary>
    IReadOnlyList<MediaSourceInfo> Sources { get; }

    /// <summary>
    /// A source by its id, or <c>null</c>.
    /// </summary>
    MediaSourceInfo? Find(Guid id);

    /// <summary>
    /// Loads the sources again from the database and raises <see cref="Changed"/>.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
