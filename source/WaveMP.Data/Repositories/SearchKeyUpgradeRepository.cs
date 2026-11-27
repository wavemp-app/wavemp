using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WaveMP.Data.Entities;
using WaveMP.Data.Texts;

namespace WaveMP.Data.Repositories;

internal sealed class SearchKeyUpgradeRepository(WaveMpDbContext dbContext) : ISearchKeyUpgradeRepository
{
    /// <summary>
    /// Number of entries written by one statement.
    /// </summary>
    internal const int BatchSize = 5000;

    public Task<bool> IsPendingAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.DataUpgrades.AnyAsync(
            upgrade => upgrade.Name == DataUpgrade.SearchKeys && upgrade.CompletedAt == null,
            cancellationToken);
    }

    public Task<int> ApplyAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                int count = 0;
                count += await UpdateAsync(
                    await dbContext.Artists.AsNoTracking()
                        .Select(artist => new NamedRow(artist.Id, artist.Name))
                        .ToListAsync(token),
                    (ids, keys) => $"""
                        UPDATE artists SET search_key = changed.key
                        FROM unnest({ids}, {keys}) AS changed(id, key)
                        WHERE artists.id = changed.id
                        """,
                    token);
                count += await UpdateAsync(
                    await dbContext.Albums.AsNoTracking()
                        .Select(album => new NamedRow(album.Id, album.Title))
                        .ToListAsync(token),
                    (ids, keys) => $"""
                        UPDATE albums SET search_key = changed.key
                        FROM unnest({ids}, {keys}) AS changed(id, key)
                        WHERE albums.id = changed.id
                        """,
                    token);
                count += await UpdateAsync(
                    await dbContext.Tracks.AsNoTracking()
                        .Select(track => new NamedRow(track.Id, track.Title))
                        .ToListAsync(token),
                    (ids, keys) => $"""
                        UPDATE tracks SET search_key = changed.key
                        FROM unnest({ids}, {keys}) AS changed(id, key)
                        WHERE tracks.id = changed.id
                        """,
                    token);
                count += await UpdateAsync(
                    await dbContext.ArtistAliases.AsNoTracking()
                        .Select(alias => new NamedRow(alias.Id, alias.Name))
                        .ToListAsync(token),
                    (ids, keys) => $"""
                        UPDATE artist_aliases SET search_key = changed.key
                        FROM unnest({ids}, {keys}) AS changed(id, key)
                        WHERE artist_aliases.id = changed.id
                        """,
                    token);
                await dbContext.DataUpgrades
                    .Where(upgrade => upgrade.Name == DataUpgrade.SearchKeys)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(upgrade => upgrade.CompletedAt, now), token);
                await transaction.CommitAsync(token);
                return count;
            },
            cancellationToken);
    }

    private async Task<int> UpdateAsync(
        List<NamedRow> rows,
        Func<Guid[], string[], FormattableString> statement,
        CancellationToken cancellationToken)
    {
        foreach (NamedRow[] batch in rows.Chunk(BatchSize))
        {
            Guid[] ids = [.. batch.Select(row => row.Id)];
            string[] keys = [.. batch.Select(row => SearchKeys.Of(row.Name) ?? string.Empty)];
            await dbContext.Database.ExecuteSqlAsync(statement(ids, keys), cancellationToken);
        }

        return rows.Count;
    }

    private sealed record NamedRow(Guid Id, string Name);
}
