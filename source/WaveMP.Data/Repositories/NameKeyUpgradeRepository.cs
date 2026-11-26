using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

internal sealed class NameKeyUpgradeRepository(WaveMpDbContext dbContext) : INameKeyUpgradeRepository
{
    public Task<bool> IsPendingAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.DataUpgrades.AnyAsync(
            upgrade => upgrade.Name == DataUpgrade.NameKeys && upgrade.CompletedAt == null,
            cancellationToken);
    }

    public async Task<NameKeyUpgradeInput> LoadAsync(CancellationToken cancellationToken = default)
    {
        List<NameKeyUpgradeArtist> artists = await dbContext.Artists.AsNoTracking()
            .OrderBy(artist => artist.Id)
            .Select(artist => new NameKeyUpgradeArtist(artist.Id, artist.Name, artist.FirstSeenAt))
            .ToListAsync(cancellationToken);
        List<NameKeyUpgradeAlbum> albums = await dbContext.Albums.AsNoTracking()
            .OrderBy(album => album.Id)
            .Select(album => new NameKeyUpgradeAlbum(
                album.Id,
                album.Title,
                album.AlbumKey,
                album.AlbumArtistId,
                album.FirstSeenAt,
                album.Tracks.OrderBy(track => track.Id).Select(track => (Guid?)track.SourceId).FirstOrDefault(),
                album.Tracks.OrderBy(track => track.Id).Select(track => track.DirectoryPath).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        List<NameKeyUpgradeGenre> genres = await dbContext.Genres.AsNoTracking()
            .OrderBy(genre => genre.Id)
            .Select(genre => new NameKeyUpgradeGenre(
                genre.Id,
                genre.Name,
                dbContext.Tracks.Count(track => track.GenreId == genre.Id)))
            .ToListAsync(cancellationToken);
        List<NameKeyUpgradeSmartPlaylist> smartPlaylists = [.. (await dbContext.SmartPlaylists.AsNoTracking()
                .OrderBy(playlist => playlist.Id)
                .Select(playlist => new { playlist.Id, playlist.Rules })
                .ToListAsync(cancellationToken))
            .Select(playlist => new NameKeyUpgradeSmartPlaylist(playlist.Id, playlist.Rules))];
        return new NameKeyUpgradeInput(artists, albums, genres, smartPlaylists);
    }

    public Task ApplyAsync(NameKeyUpgradePlan plan, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await ApplyArtistsAsync(plan, now, token);
                await ApplyAlbumsAsync(plan, now, token);
                await ApplyGenresAsync(plan, token);
                foreach ((Guid playlistId, IReadOnlyList<SmartPlaylistRule> rules) in plan.SmartPlaylistRules)
                {
                    List<SmartPlaylistRule> changed = [.. rules];
                    await dbContext.SmartPlaylists
                        .Where(playlist => playlist.Id == playlistId)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(playlist => playlist.Rules, changed),
                            token);
                }

                // The joined entries are left without songs: the existing merge logic passes their data on.
                await new LibraryScanRepository(dbContext).DeleteOrphansInTransactionAsync(now, token);
                await dbContext.DataUpgrades
                    .Where(upgrade => upgrade.Name == DataUpgrade.NameKeys)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(upgrade => upgrade.CompletedAt, now),
                        token);
                await transaction.CommitAsync(token);
            },
            cancellationToken);
    }

    /// <summary>
    /// Every artist first gets a placeholder (<c>#</c> and its id, never a normalized name), so the unique index holds
    /// while the names change; the artists that stay then get their new names, while the joined ones keep the
    /// placeholder until they are removed.
    /// </summary>
    private async Task ApplyArtistsAsync(NameKeyUpgradePlan plan, DateTimeOffset now, CancellationToken token)
    {
        await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE artists SET normalized_name = '#' || id::text",
            token);
        Guid[] ids = [.. plan.ArtistKeys.Keys];
        string[] keys = [.. ids.Select(id => plan.ArtistKeys[id])];
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE artists SET normalized_name = changed.key
            FROM unnest({ids}, {keys}) AS changed(id, key)
            WHERE artists.id = changed.id
            """,
            token);

        if (plan.ArtistMerges.Count == 0)
        {
            return;
        }

        Guid[] fromIds = [.. plan.ArtistMerges.Keys];
        Guid[] toIds = [.. fromIds.Select(id => plan.ArtistMerges[id])];

        // A song counts for an artist as its track artist and as the album artist of its album.
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO artist_moves (from_artist_id, to_artist_id, track_count, moved_at)
            SELECT merge.from_id, merge.to_id, GREATEST(1, (
                SELECT count(*) FROM tracks
                LEFT JOIN albums ON albums.id = tracks.album_id
                WHERE tracks.artist_id = merge.from_id OR albums.album_artist_id = merge.from_id)), {now}
            FROM unnest({fromIds}, {toIds}) AS merge(from_id, to_id)
            ON CONFLICT (from_artist_id, to_artist_id)
            DO UPDATE SET track_count = artist_moves.track_count + excluded.track_count, moved_at = excluded.moved_at
            """,
            token);
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE tracks SET artist_id = merge.to_id
            FROM unnest({fromIds}, {toIds}) AS merge(from_id, to_id)
            WHERE tracks.artist_id = merge.from_id
            """,
            token);
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE albums SET album_artist_id = merge.to_id
            FROM unnest({fromIds}, {toIds}) AS merge(from_id, to_id)
            WHERE albums.album_artist_id = merge.from_id
            """,
            token);
    }

    /// <summary>
    /// Like <see cref="ApplyArtistsAsync"/> for the keys of the albums; the songs of joined albums move to the album
    /// that stays, with a recorded move.
    /// </summary>
    private async Task ApplyAlbumsAsync(NameKeyUpgradePlan plan, DateTimeOffset now, CancellationToken token)
    {
        await dbContext.Database.ExecuteSqlAsync($"UPDATE albums SET album_key = '#' || id::text", token);
        Guid[] ids = [.. plan.AlbumKeys.Keys];
        string[] keys = [.. ids.Select(id => plan.AlbumKeys[id].AlbumKey)];
        string[] titles = [.. ids.Select(id => plan.AlbumKeys[id].NormalizedTitle)];
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE albums SET album_key = changed.key, normalized_title = changed.title
            FROM unnest({ids}, {keys}, {titles}) AS changed(id, key, title)
            WHERE albums.id = changed.id
            """,
            token);

        if (plan.AlbumMerges.Count == 0)
        {
            return;
        }

        Guid[] fromIds = [.. plan.AlbumMerges.Keys];
        Guid[] toIds = [.. fromIds.Select(id => plan.AlbumMerges[id])];
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO album_moves (from_album_id, to_album_id, track_count, moved_at)
            SELECT merge.from_id, merge.to_id,
                GREATEST(1, (SELECT count(*) FROM tracks WHERE tracks.album_id = merge.from_id)), {now}
            FROM unnest({fromIds}, {toIds}) AS merge(from_id, to_id)
            ON CONFLICT (from_album_id, to_album_id)
            DO UPDATE SET track_count = album_moves.track_count + excluded.track_count, moved_at = excluded.moved_at
            """,
            token);
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE tracks SET album_id = merge.to_id
            FROM unnest({fromIds}, {toIds}) AS merge(from_id, to_id)
            WHERE tracks.album_id = merge.from_id
            """,
            token);
    }

    /// <summary>
    /// Like <see cref="ApplyArtistsAsync"/> for the genres; joined genres hold nothing but their tracks, which move.
    /// </summary>
    private async Task ApplyGenresAsync(NameKeyUpgradePlan plan, CancellationToken token)
    {
        await dbContext.Database.ExecuteSqlAsync($"UPDATE genres SET normalized_name = '#' || id::text", token);
        Guid[] ids = [.. plan.GenreKeys.Keys];
        string[] keys = [.. ids.Select(id => plan.GenreKeys[id])];
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE genres SET normalized_name = changed.key
            FROM unnest({ids}, {keys}) AS changed(id, key)
            WHERE genres.id = changed.id
            """,
            token);

        if (plan.GenreMerges.Count == 0)
        {
            return;
        }

        Guid[] fromIds = [.. plan.GenreMerges.Keys];
        Guid[] toIds = [.. fromIds.Select(id => plan.GenreMerges[id])];
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE tracks SET genre_id = merge.to_id
            FROM unnest({fromIds}, {toIds}) AS merge(from_id, to_id)
            WHERE tracks.genre_id = merge.from_id
            """,
            token);
    }
}
