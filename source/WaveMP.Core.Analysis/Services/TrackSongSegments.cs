using WaveMP.Data.Entities;
using WaveMP.Data.Segments;

namespace WaveMP.Core.Analysis.Services;

/// <summary>
/// The song segments of a track.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="Audio">The primary audio file; <c>null</c> without one.</param>
/// <param name="Video">The primary video; <c>null</c> without one.</param>
public sealed record TrackSongSegments(Guid TrackId, FileSongSegment? Audio, FileSongSegment? Video);

/// <summary>
/// The song segment of a file.
/// </summary>
/// <param name="Kind">Audio file or video.</param>
/// <param name="FileId">Id of the file.</param>
/// <param name="Duration">Duration of the file, if known.</param>
/// <param name="Analysis">What the analysis found; <c>null</c> while the file waits for its analysis.</param>
/// <param name="Pending">Whether the file waits for a (new) analysis.</param>
/// <param name="Detected">The segment the analysis found (the whole file without one).</param>
/// <param name="ManualStartMilliseconds">Start as the admin corrected it.</param>
/// <param name="ManualEndMilliseconds">End as the admin corrected it.</param>
/// <param name="Effective">The segment the players use.</param>
public sealed record FileSongSegment(
    MediaFileKind Kind,
    Guid FileId,
    TimeSpan? Duration,
    MediaFileAnalysis? Analysis,
    bool Pending,
    SongSegment Detected,
    int? ManualStartMilliseconds,
    int? ManualEndMilliseconds,
    SongSegment Effective);

/// <summary>
/// The result of a correction.
/// </summary>
/// <param name="Status">Whether it was saved.</param>
/// <param name="Segments">The song segments of the track after the correction, for
/// <see cref="SongSegmentUpdateStatus.Saved"/>.</param>
/// <param name="InvalidField">The field that is out of range, for <see cref="SongSegmentUpdateStatus.Invalid"/>:
/// <c>startMilliseconds</c> or <c>endMilliseconds</c>.</param>
/// <param name="Minimum">The smallest allowed value of the invalid field.</param>
/// <param name="Maximum">The largest allowed value of the invalid field.</param>
public sealed record SongSegmentUpdate(
    SongSegmentUpdateStatus Status,
    TrackSongSegments? Segments = null,
    string? InvalidField = null,
    int? Minimum = null,
    int? Maximum = null);

/// <summary>
/// Whether a correction was saved.
/// </summary>
public enum SongSegmentUpdateStatus
{
    Saved,
    TrackNotFound,

    /// <summary>
    /// The track has no file of the kind.
    /// </summary>
    NoFile,

    /// <summary>
    /// Start or end lie outside the file, or the end does not lie after the start.
    /// </summary>
    Invalid,
}
