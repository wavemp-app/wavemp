using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Analysis.Services;

/// <summary>
/// The song segments of a track for the admin: what the analysis found in the primary audio file and the primary
/// video, the corrections of the admin and a new analysis on request.
/// </summary>
public interface ISongSegmentService
{
    /// <summary>
    /// The song segments of the primary audio file and the primary video; <c>null</c> if the track does not exist.
    /// </summary>
    Task<TrackSongSegments?> GetAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the start and the end of the song in the primary file of the kind (audio or video) as the admin corrected
    /// them, in milliseconds of the file; both <c>null</c> go back to what the analysis found. No later analysis
    /// changes them.
    /// </summary>
    Task<SongSegmentUpdate> SetAsync(
        Guid trackId,
        MediaFileKind kind,
        int? startMilliseconds,
        int? endMilliseconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets the song analysis analyze the files of the track again, in the background. Returns <c>false</c> if the
    /// track does not exist.
    /// </summary>
    Task<bool> AnalyzeAgainAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets the song analysis analyze all files of the library again, in the background. Returns the number of
    /// analyses that are renewed.
    /// </summary>
    Task<int> AnalyzeAllAgainAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Of the tracks, those whose song segment needs a look of the admin, with the reason.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SongSegmentNotice>> FindNoticesAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default);
}
