using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Texts;

namespace WaveMP.Data.Repositories;

internal sealed class SmartPlaylistRepository(WaveMpDbContext dbContext) : ISmartPlaylistRepository
{
    public Task<Page<SmartPlaylist>> ListAsync(PlaylistListQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<SmartPlaylist> playlists = dbContext.SmartPlaylists.AsNoTracking()
            .Where(playlist => playlist.UserId == query.UserId);
        if (query.Search is { } search)
        {
            string pattern = LikePatterns.Contains(search);
            playlists = playlists.Where(playlist =>
                EF.Functions.ILike(playlist.Name, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.TrigramsAreWordSimilar(search, playlist.Name));
        }

        IOrderedQueryable<SmartPlaylist> sorted = query.Sort switch
        {
            PlaylistSortField.CreatedAt => playlists.OrderBy(playlist => playlist.CreatedAt, query.Direction),
            PlaylistSortField.UpdatedAt => playlists.OrderBy(playlist => playlist.UpdatedAt, query.Direction),
            _ => playlists.OrderBy(playlist => playlist.NormalizedName, query.Direction),
        };

        return sorted.ThenBy(playlist => playlist.Id).ToPageAsync(query.Offset, query.Limit, cancellationToken);
    }

    public Task<SmartPlaylist?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.SmartPlaylists.AsNoTracking()
            .FirstOrDefaultAsync(playlist => playlist.Id == id && playlist.UserId == userId, cancellationToken);
    }

    public Task CreateAsync(SmartPlaylist playlist, CancellationToken cancellationToken = default)
    {
        return dbContext.InUserTransactionAsync(
            playlist.UserId,
            async token =>
            {
                dbContext.ChangeTracker.Clear();
                dbContext.SmartPlaylists.Add(playlist);
                await dbContext.SaveChangesAsync(token);
                dbContext.ChangeTracker.Clear();
                return true;
            },
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(SmartPlaylist playlist, CancellationToken cancellationToken = default)
    {
        int changed = await dbContext.SmartPlaylists
            .Where(candidate => candidate.Id == playlist.Id && candidate.UserId == playlist.UserId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Name, playlist.Name)
                    .SetProperty(candidate => candidate.NormalizedName, playlist.NormalizedName)
                    .SetProperty(candidate => candidate.Match, playlist.Match)
                    .SetProperty(candidate => candidate.Rules, playlist.Rules)
                    .SetProperty(candidate => candidate.Sort, playlist.Sort)
                    .SetProperty(candidate => candidate.Direction, playlist.Direction)
                    .SetProperty(candidate => candidate.Limit, playlist.Limit)
                    .SetProperty(candidate => candidate.UpdatedAt, playlist.UpdatedAt),
                cancellationToken);
        return changed > 0;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        int deleted = await dbContext.SmartPlaylists
            .Where(playlist => playlist.Id == id && playlist.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<IReadOnlyList<SmartPlaylistRule>> LinkAsync(
        IReadOnlyList<SmartPlaylistRule> rules,
        CancellationToken cancellationToken = default)
    {
        List<SmartPlaylistRule> linked = [];
        foreach (SmartPlaylistRule rule in rules)
        {
            if (!rule.CanReference())
            {
                linked.Add(rule);
                continue;
            }

            Guid? id = rule.ReferenceId is { } referenceId
                && (await NamesOfAsync(rule.Field, [referenceId], cancellationToken)).ContainsKey(referenceId)
                    ? referenceId
                    : await FindByNameAsync(rule.Field, rule.Text, cancellationToken);
            linked.Add(rule with { ReferenceId = id });
        }

        return linked;
    }

    public async Task<IReadOnlyList<SmartPlaylistRule>> NameReferencesAsync(
        IReadOnlyList<SmartPlaylistRule> rules,
        CancellationToken cancellationToken = default)
    {
        Dictionary<SmartPlaylistField, IReadOnlyDictionary<Guid, string>> names = [];
        foreach (IGrouping<SmartPlaylistField, SmartPlaylistRule> field in rules
            .Where(rule => rule.ReferenceId is not null)
            .GroupBy(rule => rule.Field))
        {
            names[field.Key] = await NamesOfAsync(
                field.Key,
                [.. field.Select(rule => rule.ReferenceId!.Value).Distinct()],
                cancellationToken);
        }

        return [.. rules.Select(rule =>
        {
            if (rule.ReferenceId is not { } id)
            {
                return rule;
            }

            return names.TryGetValue(rule.Field, out IReadOnlyDictionary<Guid, string>? known)
                && known.TryGetValue(id, out string? name)
                    ? rule with { Text = name }
                    : rule with { ReferenceId = null };
        })];
    }

    /// <summary>
    /// The names of the entries of a field (artists, album titles or genres) with these ids; ids of other entries are
    /// left out.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> NamesOfAsync(
        SmartPlaylistField field,
        Guid[] ids,
        CancellationToken cancellationToken)
    {
        return field switch
        {
            SmartPlaylistField.Artist => await dbContext.Artists.AsNoTracking()
                .Where(artist => ids.Contains(artist.Id))
                .ToDictionaryAsync(artist => artist.Id, artist => artist.Name, cancellationToken),
            SmartPlaylistField.Album => await dbContext.Albums.AsNoTracking()
                .Where(album => ids.Contains(album.Id))
                .ToDictionaryAsync(album => album.Id, album => album.Title, cancellationToken),
            SmartPlaylistField.Genre => await dbContext.Genres.AsNoTracking()
                .Where(genre => ids.Contains(genre.Id))
                .ToDictionaryAsync(genre => genre.Id, genre => genre.Name, cancellationToken),
            _ => [],
        };
    }

    /// <summary>
    /// The entry of a field with the normalized name of the text, if exactly one has it.
    /// </summary>
    private async Task<Guid?> FindByNameAsync(
        SmartPlaylistField field,
        string? text,
        CancellationToken cancellationToken)
    {
        if (NameKeys.Of(text) is not { } key)
        {
            return null;
        }

        List<Guid> matches = field switch
        {
            SmartPlaylistField.Artist => await dbContext.Artists.AsNoTracking()
                .Where(artist => artist.NormalizedName == key)
                .Select(artist => artist.Id)
                .Take(2)
                .ToListAsync(cancellationToken),
            SmartPlaylistField.Album => await dbContext.Albums.AsNoTracking()
                .Where(album => album.NormalizedTitle == key)
                .Select(album => album.Id)
                .Take(2)
                .ToListAsync(cancellationToken),
            _ => await dbContext.Genres.AsNoTracking()
                .Where(genre => genre.NormalizedName == key)
                .Select(genre => genre.Id)
                .Take(2)
                .ToListAsync(cancellationToken),
        };
        return matches is [Guid id] ? id : null;
    }

    public async Task<Page<Guid>> ListTrackIdsAsync(
        Guid userId,
        SmartPlaylistCriteria criteria,
        DateTimeOffset now,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        IQueryable<Track> filtered = Filtered(userId, criteria, now);
        int total = await filtered.CountAsync(cancellationToken);
        if (criteria.Limit is { } max)
        {
            total = Math.Min(total, max);
        }

        // The limit of the criteria ends the list; a page never reaches beyond it.
        int take = Math.Min(limit, total - offset);
        List<Guid> ids = take > 0
            ? await SmartPlaylistQueries.Sort(filtered, dbContext, userId, criteria)
                .Skip(offset)
                .Take(take)
                .Select(track => track.Id)
                .ToListAsync(cancellationToken)
            : [];
        return new Page<Guid>(ids, offset, limit, total);
    }

    public async Task<SmartPlaylistSummary> SummarizeAsync(
        Guid userId,
        SmartPlaylistCriteria criteria,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Track> tracks = Filtered(userId, criteria, now);
        if (criteria.Limit is { } max)
        {
            tracks = SmartPlaylistQueries.Sort(tracks, dbContext, userId, criteria).Take(max);
        }

        int count = await tracks.CountAsync(cancellationToken);
        double duration = count == 0
            ? 0
            : await tracks.SumAsync(
                track => track.Duration.HasValue ? track.Duration.Value.TotalMilliseconds : 0,
                cancellationToken);
        return new SmartPlaylistSummary(count, duration);
    }

    public async Task<IReadOnlyList<PlaylistCoverCandidateRow>> ListCoverCandidatesAsync(
        Guid playlistId,
        Guid userId,
        SmartPlaylistCriteria criteria,
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken = default)
    {
        int take = Math.Min(count, criteria.Limit ?? int.MaxValue);
        if (take <= 0)
        {
            return [];
        }

        var candidates = await SmartPlaylistQueries.Sort(Filtered(userId, criteria, now), dbContext, userId, criteria)
            .Take(take)
            .Select(track => new
            {
                track.Id,
                track.AlbumId,
                HasCover = track.Covers.Any()
                    || (track.AlbumId != null
                        && dbContext.Covers.Any(cover =>
                            cover.AlbumId == track.AlbumId
                            || (cover.Source == CoverSource.Embedded
                                && cover.Track != null
                                && cover.Track.AlbumId == track.AlbumId)))
                    || dbContext.OnlineCovers.Any(cover =>
                        cover.TrackId == track.Id
                        || (track.AlbumId != null
                            && cover.AlbumId == track.AlbumId)),
            })
            .ToListAsync(cancellationToken);
        return [.. candidates.Select((candidate, index) =>
            new PlaylistCoverCandidateRow(playlistId, index, candidate.Id, candidate.AlbumId, candidate.HasCover))];
    }

    private IQueryable<Track> Filtered(Guid userId, SmartPlaylistCriteria criteria, DateTimeOffset now)
    {
        return SmartPlaylistQueries.Filter(dbContext.Tracks.AsNoTracking(), dbContext, userId, criteria, now);
    }
}
