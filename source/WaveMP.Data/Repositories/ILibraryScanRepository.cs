using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Writes the library during a scan. Changes of a folder are collected on tracked entities and saved together with
/// <see cref="SaveChangesAsync"/>, so every folder is updated atomically.
/// </summary>
public interface ILibraryScanRepository
{
    /// <summary>
    /// The stored files of a source in the folders and all their subfolders; <c>null</c> for the whole source.
    /// </summary>
    Task<IReadOnlyList<MediaFileSnapshot>> GetFileSnapshotsAsync(
        Guid sourceId,
        IReadOnlyCollection<string>? directories,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The stored files of one folder of a source, without its subfolders; an empty path for the folder of the
    /// source itself.
    /// </summary>
    Task<IReadOnlyList<MediaFileSnapshot>> GetFolderSnapshotsAsync(
        Guid sourceId,
        string directoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The number of stored files of a source.
    /// </summary>
    Task<int> CountFilesAsync(Guid sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The media sources whose albums were formed with an older version of the rules than <paramref name="version"/>.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetSourcesToRegroupAsync(int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// The folders of a source that contain tracks, in order.
    /// </summary>
    Task<IReadOnlyList<string>> GetTrackDirectoriesAsync(Guid sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the version of the rules the albums of a source were formed with.
    /// </summary>
    Task SetAlbumGroupingVersionAsync(Guid sourceId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// The files and tracks of one folder of a source (not its subfolders), tracked.
    /// </summary>
    Task<StoredFolder> LoadFolderAsync(
        Guid sourceId,
        string directoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// An artist by its normalized name, tracked.
    /// </summary>
    Task<Artist?> FindArtistAsync(string normalizedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// An album by its key, tracked.
    /// </summary>
    Task<Album?> FindAlbumAsync(string albumKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new library entity; saved with <see cref="SaveChangesAsync"/>.
    /// </summary>
    void Add<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>
    /// Removes a tracked library entity; saved with <see cref="SaveChangesAsync"/>.
    /// </summary>
    void Remove<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>
    /// Saves all collected changes in one transaction and stops tracking the entities.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the files and tracks of folders of a source that no longer exist (not their subfolders). Returns the
    /// number of deleted files and tracks.
    /// </summary>
    Task<(int Files, int Tracks)> DeleteDirectoriesAsync(
        Guid sourceId,
        IReadOnlyCollection<string> directories,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The folders of all sources that contain tracks of an album.
    /// </summary>
    Task<IReadOnlyList<AlbumFolder>> GetAlbumFoldersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The pictures whose normalized base name is one of <paramref name="normalizedBaseNames"/>.
    /// </summary>
    Task<IReadOnlyList<AlbumCoverImage>> GetAlbumCoverImagesAsync(
        IReadOnlyCollection<string> normalizedBaseNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The covers of all albums, tracked.
    /// </summary>
    Task<IReadOnlyList<Cover>> LoadAlbumCoversAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes albums without tracks and artists without tracks and albums. Returns the number of deleted albums and
    /// artists.
    /// </summary>
    Task<(int Albums, int Artists)> DeleteOrphansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every album as a compilation whose artist has the normalized name
    /// <paramref name="compilationArtistKey"/> or one of whose tracks is marked as part of a compilation in the tags
    /// of the file it takes its metadata from (the primary audio file, otherwise the primary video), and every other
    /// album as none. Albums whose value changes get <paramref name="now"/> as their change time. Returns the number
    /// of changed albums.
    /// </summary>
    Task<int> UpdateAlbumCompilationsAsync(
        string compilationArtistKey,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<LibraryTotals> CountLibraryAsync(CancellationToken cancellationToken = default);
}
