using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Reads the tracks of the library, their files and the genres and years of the tracks, and sets the video offset of
/// tracks. Nothing is tracked.
/// </summary>
public interface ITrackRepository
{
    /// <summary>
    /// Lists the tracks sorted and filtered as requested; ties are sorted by id.
    /// </summary>
    Task<Page<TrackRow>> ListAsync(TrackListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the groups of the track list need of every track of <see cref="ListAsync"/> with the same filters and
    /// sort order, without paging (offset and limit of the query do not apply): album, artist and year of the track
    /// and the first character of the text of the sort key (album artist, for tracks without album the artist of the
    /// track, for <see cref="TrackSortField.AlbumArtist"/>; album title, artist name, genre or title for
    /// <see cref="TrackSortField.Album"/>, <see cref="TrackSortField.Artist"/>, <see cref="TrackSortField.Genre"/> and
    /// <see cref="TrackSortField.Title"/>; none for the other keys).
    /// </summary>
    Task<IReadOnlyList<GroupKeyRow>> ListGroupKeysAsync(
        TrackListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The track with the favorite and rating of <paramref name="userId"/>.
    /// </summary>
    Task<TrackRow?> FindAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tracks with these ids, in no particular order, with the favorites and ratings of <paramref name="userId"/>;
    /// ids of missing tracks are left out.
    /// </summary>
    Task<IReadOnlyList<TrackRow>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The file of the kind the track plays (its primary file of the kind), or <c>null</c> if the track does not
    /// exist. <see cref="TrackFileRow.File"/> is <c>null</c> if the track has no file of the kind.
    /// </summary>
    Task<TrackFileRow?> FindPrimaryFileAsync(
        Guid trackId,
        MediaFileKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the start and the end of the song in the primary file of the kind (audio or video) as the admin corrected
    /// them, in milliseconds of the file; <c>null</c> takes the value the song analysis found. Returns <c>false</c> if
    /// the track does not exist.
    /// </summary>
    Task<bool> SetManualSongSegmentAsync(
        Guid trackId,
        MediaFileKind kind,
        int? startMilliseconds,
        int? endMilliseconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the files of a track: audio, video and its own pictures, also duplicates of lower quality.
    /// </summary>
    Task<Page<MediaFile>> ListFilesAsync(TrackFileListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the genres of the tracks; genres that differ only in case are one genre.
    /// </summary>
    Task<Page<GenreRow>> ListGenresAsync(GenreListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the years of the tracks.
    /// </summary>
    Task<Page<YearRow>> ListYearsAsync(YearListQuery query, CancellationToken cancellationToken = default);
}
