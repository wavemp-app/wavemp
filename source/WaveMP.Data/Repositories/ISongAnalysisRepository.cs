using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The results of the song analysis per media file (see <see cref="MediaFileAnalysis"/>).
/// </summary>
public interface ISongAnalysisRepository
{
    /// <summary>
    /// Ids of tracks whose primary audio file or primary video was never analyzed, was analyzed by an older version
    /// than <paramref name="version"/> or changed since (size or time of change), newest tracks first.
    /// </summary>
    /// <param name="version">The current version of the analysis.</param>
    /// <param name="limit">Maximum number of tracks.</param>
    /// <param name="excluded">Tracks to leave out, e.g. those handled in this run already.</param>
    /// <param name="cancellationToken">Aborts the query.</param>
    Task<IReadOnlyList<Guid>> ListTracksToAnalyzeAsync(
        int version,
        int limit,
        IReadOnlyCollection<Guid> excluded,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Number of tracks <see cref="ListTracksToAnalyzeAsync"/> would list.
    /// </summary>
    Task<int> CountTracksToAnalyzeAsync(int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// The track with its primary audio file and primary video (each with its analysis) and the corrections of the
    /// admin; <c>null</c> if the track does not exist.
    /// </summary>
    Task<SongAnalysisTrackRow?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the analyses, replacing earlier ones of the same files. Analyses of files that are gone meanwhile are
    /// left out.
    /// </summary>
    Task SaveAsync(IReadOnlyCollection<MediaFileAnalysis> analyses, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the analyses of the files of a track, or of all files with <c>null</c>, as outdated, so the next run
    /// analyzes them again. The corrections of the admin stay. Returns the number of marked analyses.
    /// </summary>
    Task<int> MarkOutdatedAsync(Guid? trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Of the tracks, those whose song segment needs a look of the admin: of the primary audio file or the primary
    /// video, a found segment the analysis is not sure about or a file that did not match the other recording (see
    /// <see cref="Segments.SongSegment.NeedsReview"/>), with the reason.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SongSegmentNotice>> FindNoticesAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default);
}
