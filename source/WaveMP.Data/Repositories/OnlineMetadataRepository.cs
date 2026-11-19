using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

internal sealed class OnlineMetadataRepository(WaveMpDbContext dbContext) : IOnlineMetadataRepository
{
    public async Task<OnlineTrackRow?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Tracks.AsNoTracking()
            .Where(track => track.Id == trackId)
            .Select(track => new OnlineTrackRow(
                track.Id,
                track.SourceId,
                track.DirectoryPath,
                track.BaseName,
                track.AlbumId,
                track.Duration,
                track.CoverPriority,
                track.Album != null ? track.Album.CoverPriority : null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OnlineAlbumRow?> FindAlbumAsync(Guid albumId, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Albums.AnyAsync(album => album.Id == albumId, cancellationToken))
        {
            return null;
        }

        List<OnlineAlbumTrackRow> tracks = await dbContext.Tracks.AsNoTracking()
            .Where(track => track.AlbumId == albumId)
            .OrderBy(track => track.DiscNumber)
            .ThenBy(track => track.TrackNumber)
            .ThenBy(track => track.Id)
            .Select(track => new OnlineAlbumTrackRow(track.Id, track.SourceId, track.DirectoryPath, track.Duration))
            .ToListAsync(cancellationToken);
        return new OnlineAlbumRow(albumId, tracks);
    }

    public async Task<Guid?> FindAlbumIdOfTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Tracks.AsNoTracking()
            .Where(track => track.Id == trackId)
            .Select(track => track.AlbumId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Saved, string? ReplacedFileName)> SetOnlineCoverAsync(
        OnlineCover cover,
        CancellationToken cancellationToken = default)
    {
        bool ownerExists = cover.AlbumId is { } albumId
            ? await dbContext.Albums.AnyAsync(album => album.Id == albumId, cancellationToken)
            : await dbContext.Tracks.AnyAsync(track => track.Id == cover.TrackId, cancellationToken);
        if (!ownerExists)
        {
            return (false, null);
        }

        string? replaced = await FindFileNameAsync(cover.AlbumId, cover.TrackId, cancellationToken);
        await OwnedBy(cover.AlbumId, cover.TrackId).ExecuteDeleteAsync(cancellationToken);
        dbContext.OnlineCovers.Add(cover);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The album or track was removed in the meantime (foreign key), or another cover was stored for it at the
            // same moment (unique index); the earlier cover is gone either way.
            return (false, replaced);
        }
        finally
        {
            dbContext.Entry(cover).State = EntityState.Detached;
        }

        return (true, replaced);
    }

    public async Task<string?> RemoveOnlineCoverAsync(
        Guid? albumId,
        Guid? trackId,
        CancellationToken cancellationToken = default)
    {
        string? fileName = await FindFileNameAsync(albumId, trackId, cancellationToken);
        if (fileName is not null)
        {
            await OwnedBy(albumId, trackId).ExecuteDeleteAsync(cancellationToken);
        }

        return fileName;
    }

    public async Task<IReadOnlySet<string>> ListOnlineCoverFileNamesAsync(
        CancellationToken cancellationToken = default)
    {
        List<string> names = await dbContext.OnlineCovers.AsNoTracking()
            .Select(cover => cover.FileName)
            .ToListAsync(cancellationToken);
        return names.ToHashSet(StringComparer.Ordinal);
    }

    private Task<string?> FindFileNameAsync(Guid? albumId, Guid? trackId, CancellationToken cancellationToken)
    {
        return OwnedBy(albumId, trackId)
            .AsNoTracking()
            .Select(cover => cover.FileName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<OnlineCover> OwnedBy(Guid? albumId, Guid? trackId)
    {
        return albumId is { } album
            ? dbContext.OnlineCovers.Where(cover => cover.AlbumId == album)
            : dbContext.OnlineCovers.Where(cover => cover.TrackId == trackId);
    }
}
