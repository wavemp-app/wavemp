using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Texts;

namespace WaveMP.Data.Repositories;

internal sealed class ArtistAliasRepository(WaveMpDbContext dbContext) : IArtistAliasRepository
{
    public Task<Page<ArtistAliasRow>> ListAsync(
        Guid artistId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return Project(Shown(artistId).OrderBy(alias => alias.NormalizedName).ThenBy(alias => alias.Id))
            .ToPageAsync(offset, limit, cancellationToken);
    }

    public Task<ArtistAliasRow?> FindAsync(Guid artistId, Guid aliasId, CancellationToken cancellationToken = default)
    {
        return Project(Shown(artistId).Where(alias => alias.Id == aliasId)).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ArtistAliasAddResult> AddAsync(
        Guid artistId,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (NameKeys.Of(name) is not { } key)
        {
            return new ArtistAliasAddResult(ArtistAliasAddStatus.Empty);
        }

        string? artistKey = await dbContext.Artists
            .Where(artist => artist.Id == artistId)
            .Select(artist => artist.NormalizedName)
            .FirstOrDefaultAsync(cancellationToken);
        if (artistKey is null)
        {
            return new ArtistAliasAddResult(ArtistAliasAddStatus.ArtistNotFound);
        }

        if (artistKey == key)
        {
            return new ArtistAliasAddResult(ArtistAliasAddStatus.Duplicate);
        }

        ArtistAlias? alias = await dbContext.ArtistAliases
            .FirstOrDefaultAsync(alias => alias.ArtistId == artistId && alias.NormalizedName == key, cancellationToken);
        if (alias is { IsRemoved: false })
        {
            return new ArtistAliasAddResult(ArtistAliasAddStatus.Duplicate);
        }

        if (alias is null)
        {
            alias = new ArtistAlias
            {
                Id = Guid.CreateVersion7(now),
                ArtistId = artistId,
                Name = name,
                NormalizedName = key,
                Source = ArtistAliasSource.Admin,
                CreatedAt = now,
            };
            dbContext.ArtistAliases.Add(alias);
        }
        else
        {
            alias.Name = name;
            alias.Source = ArtistAliasSource.Admin;
            alias.IsRemoved = false;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation,
        } postgres)
        {
            // The same alias was added or the artist removed at the same moment.
            return new ArtistAliasAddResult(postgres.SqlState == PostgresErrorCodes.UniqueViolation
                ? ArtistAliasAddStatus.Duplicate
                : ArtistAliasAddStatus.ArtistNotFound);
        }
        finally
        {
            dbContext.Entry(alias).State = EntityState.Detached;
        }

        return new ArtistAliasAddResult(
            ArtistAliasAddStatus.Added,
            new ArtistAliasRow(alias.Id, alias.ArtistId, alias.Name, alias.Source));
    }

    public async Task<bool> RemoveAsync(Guid artistId, Guid aliasId, CancellationToken cancellationToken = default)
    {
        IQueryable<ArtistAlias> found = Shown(artistId).Where(alias => alias.Id == aliasId);
        int deleted = await found
            .Where(alias => alias.Source == ArtistAliasSource.Admin)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
        {
            return true;
        }

        int hidden = await found
            .Where(alias => alias.Source == ArtistAliasSource.MusicBrainz)
            .ExecuteUpdateAsync(setters => setters.SetProperty(alias => alias.IsRemoved, true), cancellationToken);
        return hidden > 0;
    }

    public Task<IReadOnlyList<ArtistAliasRow>?> ReplaceMusicBrainzAliasesAsync(
        Guid artistId,
        IReadOnlyList<string> names,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);

                // Locks the artist, so two lookups of the same artist write one after the other.
                Artist? artist = (await dbContext.Artists
                        .FromSql($"SELECT * FROM artists WHERE id = {artistId} FOR UPDATE")
                        .AsNoTracking()
                        .ToListAsync(token))
                    .FirstOrDefault();
                if (artist is null)
                {
                    return null;
                }

                Dictionary<string, string> wanted = new(StringComparer.Ordinal);
                foreach (string name in names)
                {
                    if (NameKeys.Of(name) is { } key && key != artist.NormalizedName)
                    {
                        wanted.TryAdd(key, name);
                    }
                }

                string[] wantedKeys = [.. wanted.Keys];
                await dbContext.ArtistAliases
                    .Where(alias => alias.ArtistId == artistId
                        && alias.Source == ArtistAliasSource.MusicBrainz
                        && !alias.IsRemoved
                        && !wantedKeys.Contains(alias.NormalizedName))
                    .ExecuteDeleteAsync(token);
                HashSet<string> known = [.. await dbContext.ArtistAliases
                    .Where(alias => alias.ArtistId == artistId)
                    .Select(alias => alias.NormalizedName)
                    .ToListAsync(token)];
                List<ArtistAlias> added = [.. wanted
                    .Where(pair => !known.Contains(pair.Key))
                    .Select(pair => new ArtistAlias
                    {
                        Id = Guid.CreateVersion7(now),
                        ArtistId = artistId,
                        Name = pair.Value,
                        NormalizedName = pair.Key,
                        Source = ArtistAliasSource.MusicBrainz,
                        CreatedAt = now,
                    })];
                if (added.Count > 0)
                {
                    dbContext.ArtistAliases.AddRange(added);
                    try
                    {
                        await dbContext.SaveChangesAsync(token);
                    }
                    finally
                    {
                        foreach (ArtistAlias alias in added)
                        {
                            dbContext.Entry(alias).State = EntityState.Detached;
                        }
                    }
                }

                await transaction.CommitAsync(token);
                return (IReadOnlyList<ArtistAliasRow>?)await Project(Shown(artistId)
                        .OrderBy(alias => alias.NormalizedName)
                        .ThenBy(alias => alias.Id))
                    .ToListAsync(token);
            },
            cancellationToken);
    }

    private IQueryable<ArtistAlias> Shown(Guid artistId)
    {
        return dbContext.ArtistAliases.AsNoTracking().Where(alias => alias.ArtistId == artistId && !alias.IsRemoved);
    }

    private static IQueryable<ArtistAliasRow> Project(IQueryable<ArtistAlias> aliases)
    {
        return aliases.Select(alias => new ArtistAliasRow(alias.Id, alias.ArtistId, alias.Name, alias.Source));
    }
}
