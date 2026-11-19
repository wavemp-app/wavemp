using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

internal sealed class CoverRepository(WaveMpDbContext dbContext) : ICoverRepository
{
    public async Task<IReadOnlyList<CoverCandidateRow>?> GetTrackCandidatesAsync(
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        var track = await dbContext.Tracks.AsNoTracking()
            .Where(track => track.Id == trackId)
            .Select(track => new { track.AlbumId })
            .FirstOrDefaultAsync(cancellationToken);
        if (track is null)
        {
            return null;
        }

        if (track.AlbumId is { } albumId)
        {
            return await ListAsync(
                dbContext.Covers.Where(cover =>
                    cover.TrackId == trackId
                    || cover.AlbumId == albumId
                    || (cover.Track != null && cover.Track.AlbumId == albumId)),
                dbContext.OnlineCovers.Where(cover =>
                    cover.TrackId == trackId
                    || cover.AlbumId == albumId
                    || (cover.Track != null && cover.Track.AlbumId == albumId)),
                cancellationToken);
        }

        return await ListAsync(
            dbContext.Covers.Where(cover => cover.TrackId == trackId),
            dbContext.OnlineCovers.Where(cover => cover.TrackId == trackId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoverCandidateRow>?> GetAlbumCandidatesAsync(
        Guid albumId,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Albums.AnyAsync(album => album.Id == albumId, cancellationToken))
        {
            return null;
        }

        return await ListAsync(
            dbContext.Covers.Where(cover =>
                cover.AlbumId == albumId || (cover.Track != null && cover.Track.AlbumId == albumId)),
            dbContext.OnlineCovers.Where(cover =>
                cover.AlbumId == albumId || (cover.Track != null && cover.Track.AlbumId == albumId)),
            cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListAlbumIdsWithCoversAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Albums.AsNoTracking()
            .Where(album => album.Covers.Any()
                || album.Tracks.Any(track => track.Covers.Any())
                || dbContext.OnlineCovers.Any(cover =>
                    cover.AlbumId == album.Id || (cover.Track != null && cover.Track.AlbumId == album.Id)))
            .OrderBy(album => album.Id)
            .Select(album => album.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListLooseTrackIdsWithCoversAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Tracks.AsNoTracking()
            .Where(track => track.AlbumId == null
                && (track.Covers.Any() || dbContext.OnlineCovers.Any(cover => cover.TrackId == track.Id)))
            .OrderBy(track => track.Id)
            .Select(track => track.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CoverCandidateRow>> GetAlbumBatchCandidatesAsync(
        IReadOnlyCollection<Guid> albumIds,
        CancellationToken cancellationToken = default)
    {
        Guid[] ids = [.. albumIds];
        return await ListAsync(
            dbContext.Covers.Where(cover =>
                (cover.AlbumId != null && ids.Contains(cover.AlbumId.Value))
                || (cover.Track != null && cover.Track.AlbumId != null && ids.Contains(cover.Track.AlbumId.Value))),
            dbContext.OnlineCovers.Where(cover =>
                (cover.AlbumId != null && ids.Contains(cover.AlbumId.Value))
                || (cover.Track != null && cover.Track.AlbumId != null && ids.Contains(cover.Track.AlbumId.Value))),
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoverCandidateRow>> GetTrackBatchCandidatesAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        Guid[] ids = [.. trackIds];
        return await ListAsync(
            dbContext.Covers.Where(cover => cover.TrackId != null && ids.Contains(cover.TrackId.Value)),
            dbContext.OnlineCovers.Where(cover => cover.TrackId != null && ids.Contains(cover.TrackId.Value)),
            cancellationToken);
    }

    public async Task<CoverRuleRow?> GetAlbumRuleAsync(Guid albumId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Albums.AsNoTracking()
            .Where(album => album.Id == albumId)
            .Select(album => new CoverRuleRow(album.CoverPriority, null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CoverRuleRow?> GetTrackRuleAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Tracks.AsNoTracking()
            .Where(track => track.Id == trackId)
            .Select(track => new CoverRuleRow(
                track.CoverPriority,
                track.Album != null ? track.Album.CoverPriority : null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SetAlbumRuleAsync(
        Guid albumId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default)
    {
        // Not a change of the content of the library, so the time of the last change stays.
        int changed = await dbContext.Albums
            .Where(album => album.Id == albumId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(album => album.CoverPriority, rule), cancellationToken);
        return changed > 0;
    }

    public async Task<bool> SetTrackRuleAsync(
        Guid trackId,
        CoverPriority? rule,
        CancellationToken cancellationToken = default)
    {
        int changed = await dbContext.Tracks
            .Where(track => track.Id == trackId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(track => track.CoverPriority, rule), cancellationToken);
        return changed > 0;
    }

    public async Task<IReadOnlyList<CoverSourceRow>> ListCoverSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        var sources = await dbContext.Covers.AsNoTracking()
            .Select(cover => new
            {
                cover.MediaFileId,
                cover.Source,
                cover.MediaFile!.Size,
                cover.MediaFile.ModifiedAt,
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        List<CoverSourceRow> online = await dbContext.OnlineCovers.AsNoTracking()
            .Select(cover => new CoverSourceRow(cover.Id, CoverSource.Online, cover.Size, cover.CreatedAt))
            .ToListAsync(cancellationToken);
        return [.. sources.Select(source =>
            new CoverSourceRow(source.MediaFileId, source.Source, source.Size, source.ModifiedAt)), .. online];
    }

    /// <summary>
    /// The covers of the media files and the online covers, as one list.
    /// </summary>
    private static async Task<IReadOnlyList<CoverCandidateRow>> ListAsync(
        IQueryable<Cover> covers,
        IQueryable<OnlineCover> online,
        CancellationToken cancellationToken)
    {
        List<CoverCandidateRow> rows = await Project(covers).ToListAsync(cancellationToken);
        rows.AddRange(await ProjectOnline(online).ToListAsync(cancellationToken));
        return rows;
    }

    /// <summary>
    /// An online cover as candidate: the cover and its "file" share the id, the path is the name of the picture in the
    /// folder of the online covers, and it is the primary cover of its album or track.
    /// </summary>
    private static IQueryable<CoverCandidateRow> ProjectOnline(IQueryable<OnlineCover> covers)
    {
        return covers.AsNoTracking().Select(cover => new CoverCandidateRow(
            cover.Id,
            cover.Id,
            cover.AlbumId,
            cover.TrackId,
            cover.Track != null ? cover.Track.AlbumId : null,
            cover.Track != null ? cover.Track.DiscNumber : null,
            cover.Track != null ? cover.Track.TrackNumber : null,
            cover.Track != null ? cover.Track.Title : null,
            true,
            CoverSource.Online,
            Guid.Empty,
            cover.FileName,
            cover.MimeType,
            cover.Size,
            cover.CreatedAt,
            false,
            cover.Album != null
                ? cover.Album.CoverPriority
                : cover.Track != null && cover.Track.Album != null
                    ? cover.Track.Album.CoverPriority
                    : null,
            cover.Track != null ? cover.Track.CoverPriority : null));
    }

    private static IQueryable<CoverCandidateRow> Project(IQueryable<Cover> covers)
    {
        return covers.AsNoTracking().Select(cover => new CoverCandidateRow(
            cover.Id,
            cover.MediaFileId,
            cover.AlbumId,
            cover.TrackId,
            cover.Track != null ? cover.Track.AlbumId : null,
            cover.Track != null ? cover.Track.DiscNumber : null,
            cover.Track != null ? cover.Track.TrackNumber : null,
            cover.Track != null ? cover.Track.Title : null,
            cover.IsPrimary,
            cover.Source,
            cover.MediaFile!.SourceId,
            cover.MediaFile.RelativePath,
            cover.MimeType,
            cover.MediaFile.Size,
            cover.MediaFile.ModifiedAt,
            cover.Track != null
                && cover.IsPrimary
                && cover.Track.FileSelections.Any(selection =>
                    selection.Category == FileSelectionCategory.Cover
                    && !selection.IsMissing
                    && selection.MediaFileId == cover.MediaFileId),
            cover.Album != null
                ? cover.Album.CoverPriority
                : cover.Track != null && cover.Track.Album != null
                    ? cover.Track.Album.CoverPriority
                    : null,
            cover.Track != null ? cover.Track.CoverPriority : null));
    }
}
