using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using WaveMP.Data.Configurations;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

internal sealed class MediaSourceRepository(WaveMpDbContext dbContext) : IMediaSourceRepository
{
    public async Task<IReadOnlyList<MediaSource>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.MediaSources.AsNoTracking()
            .OrderBy(source => source.NormalizedName)
            .ThenBy(source => source.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<MediaSource?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.MediaSources.FirstOrDefaultAsync(source => source.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        string normalizedName,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        return dbContext.MediaSources.AnyAsync(
            source => source.NormalizedName == normalizedName && source.Id != exceptId,
            cancellationToken);
    }

    public async Task AddAsync(MediaSource source, CancellationToken cancellationToken = default)
    {
        dbContext.MediaSources.Add(source);
        await SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(MediaSource source, CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);

                // The tracks and files go with the source (foreign keys with cascade); their covers, file selections,
                // playlist entries and favorites go with the tracks and files.
                int deleted = await dbContext.MediaSources
                    .Where(source => source.Id == id)
                    .ExecuteDeleteAsync(token);
                if (deleted == 0)
                {
                    return false;
                }

                await dbContext.Albums
                    .Where(album => !dbContext.Tracks.Any(track => track.AlbumId == album.Id))
                    .ExecuteDeleteAsync(token);
                await dbContext.Artists
                    .Where(artist => !dbContext.Tracks.Any(track => track.ArtistId == artist.Id)
                        && !dbContext.Albums.Any(album => album.AlbumArtistId == artist.Id))
                    .ExecuteDeleteAsync(token);
                await transaction.CommitAsync(token);
                dbContext.ChangeTracker.Clear();
                return true;
            },
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, MediaSourceCounts>> CountAsync(
        CancellationToken cancellationToken = default)
    {
        Dictionary<Guid, int> tracks = await dbContext.Tracks
            .GroupBy(track => track.SourceId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Count, cancellationToken);
        Dictionary<Guid, int> files = await dbContext.MediaFiles
            .GroupBy(file => file.SourceId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Count, cancellationToken);
        return tracks.Keys.Union(files.Keys).ToDictionary(
            id => id,
            id => new MediaSourceCounts(tracks.GetValueOrDefault(id), files.GetValueOrDefault(id)));
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: MediaSourceConfiguration.NormalizedNameIndex,
        })
        {
            dbContext.ChangeTracker.Clear();
            throw new MediaSourceNameConflictException(exception);
        }
    }
}
