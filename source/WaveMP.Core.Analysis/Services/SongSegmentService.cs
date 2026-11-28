using Microsoft.Extensions.Logging;
using WaveMP.Core.Analysis.Options;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Data.Segments;

namespace WaveMP.Core.Analysis.Services;

internal sealed class SongSegmentService(
    ISongAnalysisRepository repository,
    ITrackRepository tracks,
    IJobService jobs,
    ILogger<SongSegmentService> logger) : ISongSegmentService
{
    /// <summary>
    /// A start may lie at most this far before the beginning of a file (the file starts that far into the song).
    /// </summary>
    internal const int MinStartMilliseconds = -600_000;

    /// <summary>
    /// Largest position when the duration of the file is unknown (24 hours).
    /// </summary>
    internal const int MaxPositionMilliseconds = 24 * 60 * 60 * 1000;

    public const string StartField = "startMilliseconds";

    public const string EndField = "endMilliseconds";

    public async Task<TrackSongSegments?> GetAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        SongAnalysisTrackRow? row = await repository.FindTrackAsync(trackId, cancellationToken);
        return row is null ? null : ToSegments(row);
    }

    public async Task<SongSegmentUpdate> SetAsync(
        Guid trackId,
        MediaFileKind kind,
        int? startMilliseconds,
        int? endMilliseconds,
        CancellationToken cancellationToken = default)
    {
        if (kind is not (MediaFileKind.Audio or MediaFileKind.Video))
        {
            return new SongSegmentUpdate(SongSegmentUpdateStatus.NoFile);
        }

        SongAnalysisTrackRow? row = await repository.FindTrackAsync(trackId, cancellationToken);
        if (row is null)
        {
            return new SongSegmentUpdate(SongSegmentUpdateStatus.TrackNotFound);
        }

        MediaFile? file = kind == MediaFileKind.Audio ? row.Audio : row.Video;
        if (file is null)
        {
            return new SongSegmentUpdate(SongSegmentUpdateStatus.NoFile);
        }

        SongSegmentUpdate? invalid = Validate(file, startMilliseconds, endMilliseconds);
        if (invalid is not null)
        {
            return invalid;
        }

        await tracks.SetManualSongSegmentAsync(trackId, kind, startMilliseconds, endMilliseconds, cancellationToken);
        SongAnalysisTrackRow? updated = await repository.FindTrackAsync(trackId, cancellationToken);
        return updated is null
            ? new SongSegmentUpdate(SongSegmentUpdateStatus.TrackNotFound)
            : new SongSegmentUpdate(SongSegmentUpdateStatus.Saved, ToSegments(updated));
    }

    public async Task<bool> AnalyzeAgainAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        SongAnalysisTrackRow? row = await repository.FindTrackAsync(trackId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        await repository.MarkOutdatedAsync(trackId, cancellationToken);
        await StartAsync(cancellationToken);
        return true;
    }

    public async Task<int> AnalyzeAllAgainAsync(CancellationToken cancellationToken = default)
    {
        int marked = await repository.MarkOutdatedAsync(null, cancellationToken);
        await StartAsync(cancellationToken);
        return marked;
    }

    public Task<IReadOnlyDictionary<Guid, SongSegmentNotice>> FindNoticesAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        return repository.FindNoticesAsync(trackIds, cancellationToken);
    }

    /// <summary>
    /// The song segments of the files of a track.
    /// </summary>
    internal static TrackSongSegments ToSegments(SongAnalysisTrackRow row)
    {
        return new TrackSongSegments(
            row.TrackId,
            row.Audio is { } audio
                ? ToSegment(audio, row.AudioSongStartMilliseconds, row.AudioSongEndMilliseconds, row.AudioSegment)
                : null,
            row.Video is { } video
                ? ToSegment(video, row.VideoSongStartMilliseconds, row.VideoSongEndMilliseconds, row.VideoSegment)
                : null);
    }

    /// <summary>
    /// Whether a file waits for a (new) analysis: never analyzed, analyzed by an older version or changed since.
    /// </summary>
    internal static bool IsPending(MediaFile file)
    {
        return file.Analysis is not { } analysis
            || analysis.Version < AnalysisOptions.Version
            || analysis.FileSize != file.Size
            || analysis.FileModifiedAt != file.ModifiedAt;
    }

    private static FileSongSegment ToSegment(MediaFile file, int? manualStart, int? manualEnd, SongSegment effective)
    {
        return new FileSongSegment(
            file.Kind,
            file.Id,
            file.Duration,
            file.Analysis,
            IsPending(file),
            SongSegment.Of(file.Analysis, null, null, file.Duration),
            manualStart,
            manualEnd,
            effective);
    }

    /// <summary>
    /// Checks start and end against the file: the start may lie before the beginning of the file (up to
    /// <see cref="MinStartMilliseconds"/>), but not at or after its end; the end lies after the start and after the
    /// beginning of the file, at most at its end.
    /// </summary>
    private static SongSegmentUpdate? Validate(MediaFile file, int? start, int? end)
    {
        int fileEnd = file.Duration is { } duration && duration > TimeSpan.Zero
            ? (int)Math.Min(MaxPositionMilliseconds, Math.Round(duration.TotalMilliseconds))
            : MaxPositionMilliseconds;
        if (start is { } startValue && (startValue < MinStartMilliseconds || startValue >= fileEnd))
        {
            return Invalid(StartField, MinStartMilliseconds, fileEnd - 1);
        }

        if (end is { } endValue)
        {
            int minimum = Math.Max(0, start ?? 0) + 1;
            if (endValue < minimum || endValue > fileEnd)
            {
                return Invalid(EndField, minimum, fileEnd);
            }
        }

        return null;
    }

    private static SongSegmentUpdate Invalid(string field, int minimum, int maximum)
    {
        return new SongSegmentUpdate(SongSegmentUpdateStatus.Invalid, null, field, minimum, maximum);
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        JobRunStartResult started = await jobs.StartRunAsync(
            new JobRunRequest(JobIds.SongAnalysis, JobRunTrigger.Manual),
            cancellationToken);
        if (!started.IsSuccess)
        {
            // A running analysis picks the marked files up before it ends.
            logger.LogDebug("The song analysis is not started: {Reason}", started.Error);
        }
    }
}
