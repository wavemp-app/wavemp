using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Segments;

namespace WaveMP.Data.Repositories;

internal sealed class SongAnalysisRepository(WaveMpDbContext dbContext) : ISongAnalysisRepository
{
    public async Task<IReadOnlyList<Guid>> ListTracksToAnalyzeAsync(
        int version,
        int limit,
        IReadOnlyCollection<Guid> excluded,
        CancellationToken cancellationToken = default)
    {
        Guid[] skipped = [.. excluded];
        return await TracksToAnalyze(version)
            .Where(track => !skipped.Contains(track.Id))
            .OrderByDescending(track => track.FirstSeenAt)
            .ThenBy(track => track.Id)
            .Select(track => track.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountTracksToAnalyzeAsync(int version, CancellationToken cancellationToken = default)
    {
        return TracksToAnalyze(version).CountAsync(cancellationToken);
    }

    public async Task<SongAnalysisTrackRow?> FindTrackAsync(
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        var track = await dbContext.Tracks.AsNoTracking()
            .Where(track => track.Id == trackId)
            .Select(track => new
            {
                track.AudioSongStartMilliseconds,
                track.AudioSongEndMilliseconds,
                track.VideoSongStartMilliseconds,
                track.VideoSongEndMilliseconds,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (track is null)
        {
            return null;
        }

        List<MediaFile> files = await dbContext.MediaFiles.AsNoTracking()
            .Include(file => file.Analysis)
            .Where(file => file.TrackId == trackId
                && file.IsPrimary
                && (file.Kind == MediaFileKind.Audio || file.Kind == MediaFileKind.Video))
            .OrderBy(file => file.Id)
            .ToListAsync(cancellationToken);
        return new SongAnalysisTrackRow(
            trackId,
            files.FirstOrDefault(file => file.Kind == MediaFileKind.Audio),
            files.FirstOrDefault(file => file.Kind == MediaFileKind.Video),
            track.AudioSongStartMilliseconds,
            track.AudioSongEndMilliseconds,
            track.VideoSongStartMilliseconds,
            track.VideoSongEndMilliseconds);
    }

    public async Task SaveAsync(
        IReadOnlyCollection<MediaFileAnalysis> analyses,
        CancellationToken cancellationToken = default)
    {
        if (analyses.Count == 0)
        {
            return;
        }

        Guid[] ids = [.. analyses.Select(analysis => analysis.MediaFileId)];
        HashSet<Guid> existingFiles = [.. await dbContext.MediaFiles
            .Where(file => ids.Contains(file.Id))
            .Select(file => file.Id)
            .ToListAsync(cancellationToken)];
        Dictionary<Guid, MediaFileAnalysis> stored = await dbContext.MediaFileAnalyses
            .Where(analysis => ids.Contains(analysis.MediaFileId))
            .ToDictionaryAsync(analysis => analysis.MediaFileId, cancellationToken);
        foreach (MediaFileAnalysis analysis in analyses.Where(analysis => existingFiles.Contains(analysis.MediaFileId)))
        {
            if (stored.TryGetValue(analysis.MediaFileId, out MediaFileAnalysis? current))
            {
                dbContext.Entry(current).CurrentValues.SetValues(analysis);
                current.Pieces = [.. analysis.Pieces];
            }
            else
            {
                dbContext.MediaFileAnalyses.Add(analysis);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A scan removed a file in the meantime; the next run analyzes what is left.
            dbContext.ChangeTracker.Clear();
        }
    }

    public Task<int> MarkOutdatedAsync(Guid? trackId, CancellationToken cancellationToken = default)
    {
        IQueryable<MediaFileAnalysis> analyses = dbContext.MediaFileAnalyses;
        if (trackId is { } id)
        {
            analyses = analyses.Where(analysis => analysis.MediaFile!.TrackId == id);
        }

        return analyses.ExecuteUpdateAsync(
            setters => setters.SetProperty(analysis => analysis.Version, 0),
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, SongSegmentNotice>> FindNoticesAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
        {
            return new Dictionary<Guid, SongSegmentNotice>();
        }

        Guid[] ids = [.. trackIds.Distinct()];
        var files = await dbContext.MediaFiles.AsNoTracking()
            .Where(file => file.TrackId != null
                && ids.Contains(file.TrackId.Value)
                && file.IsPrimary
                && (file.Kind == MediaFileKind.Audio || file.Kind == MediaFileKind.Video)
                && file.Analysis != null)
            .Select(file => new
            {
                TrackId = file.TrackId!.Value,
                file.Kind,
                file.Duration,
                file.Analysis,
                AudioStart = file.Track!.AudioSongStartMilliseconds,
                AudioEnd = file.Track.AudioSongEndMilliseconds,
                VideoStart = file.Track.VideoSongStartMilliseconds,
                VideoEnd = file.Track.VideoSongEndMilliseconds,
            })
            .ToListAsync(cancellationToken);
        Dictionary<Guid, SongSegmentNotice> notices = [];
        foreach (var file in files)
        {
            bool video = file.Kind == MediaFileKind.Video;
            var segment = SongSegment.Of(
                file.Analysis,
                video ? file.VideoStart : file.AudioStart,
                video ? file.VideoEnd : file.AudioEnd,
                file.Duration);
            if (!segment.NeedsReview)
            {
                continue;
            }

            // A mismatch weighs more than an unsure segment.
            SongSegmentNotice notice = segment.Mismatch ? SongSegmentNotice.Mismatch : SongSegmentNotice.LowConfidence;
            if (!notices.TryGetValue(file.TrackId, out SongSegmentNotice known) || known != SongSegmentNotice.Mismatch)
            {
                notices[file.TrackId] = notice;
            }
        }

        return notices;
    }

    /// <summary>
    /// Tracks with a primary audio file or video without a current analysis.
    /// </summary>
    private IQueryable<Track> TracksToAnalyze(int version)
    {
        return dbContext.Tracks.AsNoTracking()
            .Where(track => track.Files.Any(file => file.IsPrimary
                && (file.Kind == MediaFileKind.Audio || file.Kind == MediaFileKind.Video)
                && !dbContext.MediaFileAnalyses.Any(analysis => analysis.MediaFileId == file.Id
                    && analysis.Version >= version
                    && analysis.FileSize == file.Size
                    && analysis.FileModifiedAt == file.ModifiedAt)));
    }
}
