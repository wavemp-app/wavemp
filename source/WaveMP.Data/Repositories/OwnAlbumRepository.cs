using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

internal sealed class OwnAlbumRepository(WaveMpDbContext dbContext) : IOwnAlbumRepository
{
    public async Task<Page<OwnAlbumRow>> ListAsync(
        OwnAlbumListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<OwnAlbum> albums = dbContext.OwnAlbums.AsNoTracking();
        IOrderedQueryable<OwnAlbum> sorted = query.Sort switch
        {
            OwnAlbumSortField.UpdatedAt => albums
                .OrderBy(album => album.UpdatedAt, query.Direction)
                .ThenBy(album => album.Id),
            _ => albums
                .OrderBy(album => album.NormalizedTitle, query.Direction)
                .ThenBy(album => album.Id),
        };
        return await Project(sorted).ToPageAsync(query.Offset, query.Limit, cancellationToken);
    }

    public Task<OwnAlbumRow?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Project(dbContext.OwnAlbums.AsNoTracking().Where(album => album.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(OwnAlbum album, CancellationToken cancellationToken = default)
    {
        dbContext.OwnAlbums.Add(album);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        string title,
        string albumArtist,
        int? year,
        string? genre,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        string normalizedTitle = title.ToUpperInvariant();
        int changed = await dbContext.OwnAlbums
            .Where(album => album.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(album => album.Title, title)
                    .SetProperty(album => album.NormalizedTitle, normalizedTitle)
                    .SetProperty(album => album.AlbumArtist, albumArtist)
                    .SetProperty(album => album.Year, year)
                    .SetProperty(album => album.Genre, genre)
                    .SetProperty(album => album.UpdatedAt, updatedAt),
                cancellationToken);
        return changed > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.OwnAlbums.Where(album => album.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    public async Task<bool> SetCoverAsync(
        Guid id,
        string? fileName,
        string? contentType,
        DateTimeOffset? coverUpdatedAt,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        int changed = await dbContext.OwnAlbums
            .Where(album => album.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(album => album.CoverFileName, fileName)
                    .SetProperty(album => album.CoverContentType, contentType)
                    .SetProperty(album => album.CoverUpdatedAt, coverUpdatedAt)
                    .SetProperty(album => album.UpdatedAt, updatedAt),
                cancellationToken);
        return changed > 0;
    }

    public async Task<IReadOnlyList<OwnAlbumMemberRow>> ListMembersAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.OwnAlbumTracks.AsNoTracking()
            .Where(track => track.OwnAlbumId == id)
            .OrderBy(track => track.State == OwnAlbumTrackState.Removed)
            .ThenBy(track => track.Position)
            .ThenBy(track => track.TrackId)
            .Select(track => new OwnAlbumMemberRow(
                track.TrackId,
                track.Position,
                track.State,
                track.OriginalTagBackupId,
                track.AddedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<OwnAlbumSaveStatus> SaveMembersAsync(
        Guid id,
        IReadOnlyList<OwnAlbumMemberRow> members,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                int found = await dbContext.OwnAlbums
                    .Where(album => album.Id == id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(album => album.UpdatedAt, updatedAt), token);
                if (found == 0)
                {
                    return OwnAlbumSaveStatus.NotFound;
                }

                await dbContext.OwnAlbumTracks.Where(track => track.OwnAlbumId == id).ExecuteDeleteAsync(token);
                dbContext.OwnAlbumTracks.AddRange(members.Select(member => new OwnAlbumTrack
                {
                    OwnAlbumId = id,
                    TrackId = member.TrackId,
                    Position = member.Position,
                    State = member.State,
                    OriginalTagBackupId = member.OriginalTagBackupId,
                    AddedAt = member.AddedAt,
                }));
                try
                {
                    await dbContext.SaveChangesAsync(token);
                }
                catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation,
                })
                {
                    // Another album took a song, or a song left the library in the meantime.
                    dbContext.ChangeTracker.Clear();
                    return OwnAlbumSaveStatus.TrackTaken;
                }

                await transaction.CommitAsync(token);
                dbContext.ChangeTracker.Clear();
                return OwnAlbumSaveStatus.Saved;
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<OwnAlbumCandidateRow>> ListCandidatesAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = [.. trackIds.Distinct()];
        return await dbContext.Tracks.AsNoTracking()
            .Where(track => ids.Contains(track.Id))
            .Select(track => new OwnAlbumCandidateRow(
                track.Id,
                track.HasAudio,
                dbContext.OwnAlbumTracks
                    .Where(member => member.TrackId == track.Id && member.State == OwnAlbumTrackState.Member)
                    .Select(member => (Guid?)member.OwnAlbumId)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> ListCoverFileNamesAsync(CancellationToken cancellationToken = default)
    {
        List<string> names = await dbContext.OwnAlbums.AsNoTracking()
            .Where(album => album.CoverFileName != null)
            .Select(album => album.CoverFileName!)
            .ToListAsync(cancellationToken);
        return names.ToHashSet(StringComparer.Ordinal);
    }

    private IQueryable<OwnAlbumRow> Project(IQueryable<OwnAlbum> albums)
    {
        return albums.Select(album => new OwnAlbumRow(
            album.Id,
            album.Title,
            album.AlbumArtist,
            album.Year,
            album.Genre,
            album.CoverFileName,
            album.CoverContentType,
            album.CoverUpdatedAt,
            album.Tracks.Count(track => track.State == OwnAlbumTrackState.Member),
            album.Tracks.Count(track => track.State == OwnAlbumTrackState.Removed),
            album.CreatedAt,
            album.UpdatedAt,
            album.AppliedAt,
            album.AppliedAt == null
                ? null
                : album.Tracks
                    .Where(track => track.State == OwnAlbumTrackState.Member)
                    .OrderBy(track => track.Position)
                    .Select(track => track.Track!.AlbumId)
                    .FirstOrDefault()));
    }
}
