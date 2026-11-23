using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Writes the library during a scan. Changes of a folder are collected on tracked entities and saved together with
/// <see cref="SaveChangesAsync"/>, so every folder is updated atomically.
/// </summary>
public interface ILibraryScanRepository
{
    /// <summary>
    /// The stored files of a source in the folders and all their subfolders; <c>null</c> for the whole source. Each
    /// with its track and whether the track plays it, so a scan can recognize songs that were renamed or moved.
    /// </summary>
    Task<IReadOnlyList<MediaFileSnapshot>> GetFileSnapshotsAsync(
        Guid sourceId,
        IReadOnlyCollection<string>? directories,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The stored files of one folder of a source, without its subfolders; an empty path for the folder of the
    /// source itself; like <see cref="GetFileSnapshotsAsync"/> with the track of each file.
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
    /// Tracks by their ids, tracked, with all their files (and their tags), their own covers and their file
    /// selections, in one query; ids without a stored track are left out. A scan loads the songs it found at a new
    /// place with it, so it can keep them (and all their data) there.
    /// </summary>
    Task<IReadOnlyList<Track>> LoadTracksAsync(
        IReadOnlyCollection<Guid> trackIds,
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
    /// The album artist of each of the stored albums (<c>null</c> for an album without one), untracked; ids without a
    /// stored album are left out. A scan asks for the albums its songs left, to see which artists they left.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid?>> GetAlbumArtistIdsAsync(
        IReadOnlyCollection<Guid> albumIds,
        CancellationToken cancellationToken = default);

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
    /// Records that songs left an album for another one, with the number of songs per pair of albums: adds the count
    /// to the stored move of the pair or adds a new one. The moves are tracked entities saved with
    /// <see cref="SaveChangesAsync"/>, in the same transaction as the tracks that moved, so a move survives an
    /// interrupted scan. Pairs of the same album and counts below one are ignored.
    /// </summary>
    Task RecordAlbumMovesAsync(
        IReadOnlyDictionary<(Guid FromAlbumId, Guid ToAlbumId), int> moves,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that songs left an artist for another one, with the number of songs per pair of artists, like
    /// <see cref="RecordAlbumMovesAsync"/>: adds the count to the stored move of the pair or adds a new one, as tracked
    /// entities saved with <see cref="SaveChangesAsync"/> in the same transaction as the tracks that moved. A song
    /// counts for an artist as its track artist and as the album artist of its album; the caller counts a pair only
    /// when the old artist has neither role on the song any more. Pairs of the same artist and counts below one are
    /// ignored.
    /// </summary>
    Task RecordArtistMovesAsync(
        IReadOnlyDictionary<(Guid FromArtistId, Guid ToArtistId), int> moves,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the files and tracks of folders of a source that no longer exist (not their subfolders). Returns the
    /// number of deleted files and tracks. In the same transaction, it first records a move (see
    /// <see cref="RecordAlbumMovesAsync"/>) from the album of every deleted song to the album of a song elsewhere in
    /// the library whose primary file has the same kind, file name and size, so the data of an album whose folder was
    /// renamed or moved follows its songs once <see cref="DeleteOrphansAsync"/> removes it. Likewise it records a move
    /// (see <see cref="RecordArtistMovesAsync"/>) from each artist of the deleted song to the artist of the song found
    /// elsewhere, by the track artist and album artist roles and only when the old artist has neither role on that
    /// song. Folders written before (the new folder of a scan) are therefore found; a folder that appears only later is
    /// not. Songs the scan kept at a new place (see <see cref="DeleteVanishedTracksAsync"/>) have left these folders
    /// before, with their id, all their data and their album and artist moves recorded by the writer, so this only
    /// concerns the songs that are deleted.
    /// </summary>
    Task<(int Files, int Tracks)> DeleteDirectoriesAsync(
        Guid sourceId,
        IReadOnlyCollection<string> directories,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the tracks whose files a scan saw gone and that it did not keep at a new place, in one transaction:
    /// first the listed files that still lie at their listed path in the source of their track (a file the scan kept
    /// at its new place has another path and stays), then the listed tracks that still lie in their listed source and
    /// folder and that no file belongs to any more (a track the scan kept at a new place has its files and stays).
    /// Their data goes with them, as with every deleted track. Returns the number of deleted tracks.
    /// </summary>
    Task<int> DeleteVanishedTracksAsync(
        IReadOnlyCollection<VanishedTrack> tracks,
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
    /// Deletes albums without tracks and artists without tracks and albums, in one transaction. Before an album without
    /// tracks is deleted, its data goes to the albums its songs moved to (see <see cref="RecordAlbumMovesAsync"/>):
    /// the album favorites of all profiles, the cover rule, the online cover and the first seen date, by the rules of
    /// <see cref="AlbumSuccessionPlanner"/>; a list in the player that was started from it continues with the album
    /// that got most of its songs. Successors whose values change get <paramref name="now"/> as their change time.
    /// Then, after the albums are deleted (so an artist whose last album went is included), the data of every artist
    /// without tracks and albums goes to the artists its songs moved to (see <see cref="RecordArtistMovesAsync"/>) by
    /// the same rules (<see cref="ArtistSuccessionPlanner"/>): the artist favorites of all profiles and the first seen
    /// date to every artist that got its songs, a list in the player started from it to the artist that got most of
    /// them. Returns the number of deleted albums and artists.
    /// </summary>
    Task<(int Albums, int Artists)> DeleteOrphansAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

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
