using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Decides where the data of albums left empty goes (favorites of all profiles, cover rule, online cover, first seen
/// date), from the stored moves of their songs. Pure, so every rule is deterministic and testable without a database:
/// <list type="bullet">
/// <item><b>Successors</b> of an album left empty: the albums with tracks its songs moved to, also through other albums
/// left empty (chain A -> B -> C, B empty: C is a successor of A). An album left empty without any is deleted with its
/// data as before.</item>
/// <item><b>Weight</b> of a successor: the songs of the first move on the way to it (the most, if there are several
/// ways).</item>
/// <item><b>Main successor</b>: the highest weight, then the earliest first seen date, then the smallest id.</item>
/// <item><b>Order of the predecessors</b> of a successor (for values only one album can have): the highest weight, then
/// the earliest first seen date of the predecessor, then the smallest id.</item>
/// <item><b>Favorites</b>: every successor gets the album favorites of all its predecessors (union over a merge, to
/// every part of a split), with the earliest time per user.</item>
/// <item><b>Cover rule</b>: the rule of the successor wins; without one, the rule of the first predecessor that has
/// one.</item>
/// <item><b>Online cover</b>: only to the main successor and only if it has none, from the first predecessor (among
/// those it is the main successor of) that has one; never two per album.</item>
/// <item><b>First seen date</b>: the earliest of the successor and its predecessors.</item>
/// <item><b>Moves into an album left empty</b> from an album with tracks are passed on to the successors of the empty
/// album, so the data still finds the songs when the other album is left empty later; a move to the album itself is
/// dropped.</item>
/// </list>
/// </summary>
internal static class AlbumSuccessionPlanner
{
    public static AlbumSuccessionPlan Plan(
        IReadOnlyCollection<AlbumSuccessionAlbum> albums,
        IReadOnlyCollection<AlbumSuccessionMove> moves,
        IReadOnlyCollection<AlbumSuccessionFavorite> favorites)
    {
        var albumsById = albums.ToDictionary(album => album.Id);
        ILookup<Guid, AlbumSuccessionMove> movesFrom = moves
            .Where(move => move.FromAlbumId != move.ToAlbumId)
            .ToLookup(move => move.FromAlbumId);

        // Weight of every successor per album left empty.
        Dictionary<Guid, Dictionary<Guid, int>> weights = [];
        foreach (AlbumSuccessionAlbum orphan in albums.Where(album => !album.HasTracks).OrderBy(album => album.Id))
        {
            Dictionary<Guid, int> found = [];
            foreach (AlbumSuccessionMove first in movesFrom[orphan.Id])
            {
                foreach (Guid successor in Reach(first.ToAlbumId, orphan.Id, albumsById, movesFrom))
                {
                    found[successor] = Math.Max(found.GetValueOrDefault(successor), first.TrackCount);
                }
            }

            if (found.Count > 0)
            {
                weights[orphan.Id] = found;
            }
        }

        var successors = weights.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<Guid>)[.. entry.Value.Keys
                .Select(id => albumsById[id])
                .OrderByDescending(successor => entry.Value[successor.Id])
                .ThenBy(successor => successor.FirstSeenAt)
                .ThenBy(successor => successor.Id)
                .Select(successor => successor.Id)]);

        // The predecessors of every successor in their order.
        var predecessors = weights
            .SelectMany(entry => entry.Value.Keys.Select(successor => (Successor: successor, Predecessor: entry.Key)))
            .GroupBy(pair => pair.Successor)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(pair => albumsById[pair.Predecessor])
                    .OrderByDescending(predecessor => weights[predecessor.Id][group.Key])
                    .ThenBy(predecessor => predecessor.FirstSeenAt)
                    .ThenBy(predecessor => predecessor.Id)
                    .ToList());

        List<AlbumSuccessionUpdate> updates = [];
        List<AlbumSuccessionOnlineCover> onlineCovers = [];
        foreach ((Guid successorId, List<AlbumSuccessionAlbum> before) in predecessors.OrderBy(entry => entry.Key))
        {
            AlbumSuccessionAlbum successor = albumsById[successorId];
            DateTimeOffset firstSeenAt = before.Select(predecessor => predecessor.FirstSeenAt)
                .Append(successor.FirstSeenAt)
                .Min();
            CoverPriority? coverPriority = successor.CoverPriority
                ?? before.Select(predecessor => predecessor.CoverPriority).FirstOrDefault(rule => rule is not null);
            if (firstSeenAt != successor.FirstSeenAt || coverPriority != successor.CoverPriority)
            {
                updates.Add(new AlbumSuccessionUpdate(successorId, firstSeenAt, coverPriority));
            }

            if (successor.OnlineCoverId is null
                && before
                    .Where(predecessor => successors[predecessor.Id][0] == successorId)
                    .Select(predecessor => predecessor.OnlineCoverId)
                    .FirstOrDefault(cover => cover is not null) is { } onlineCoverId)
            {
                onlineCovers.Add(new AlbumSuccessionOnlineCover(onlineCoverId, successorId));
            }
        }

        List<AlbumSuccessionFavorite> carriedFavorites = [.. favorites
            .Where(favorite => successors.ContainsKey(favorite.AlbumId))
            .SelectMany(favorite => successors[favorite.AlbumId]
                .Select(successor => favorite with { AlbumId = successor }))
            .GroupBy(favorite => (favorite.UserId, favorite.AlbumId))
            .Select(group => group.MinBy(favorite => favorite.CreatedAt)!)
            .OrderBy(favorite => favorite.AlbumId)
            .ThenBy(favorite => favorite.UserId)];

        List<AlbumSuccessionMove> passedOn = [.. moves
            .Where(move => albumsById.TryGetValue(move.FromAlbumId, out AlbumSuccessionAlbum? from) && from.HasTracks
                && successors.ContainsKey(move.ToAlbumId))
            .SelectMany(move => successors[move.ToAlbumId]
                .Where(successor => successor != move.FromAlbumId)
                .Select(successor => move with { ToAlbumId = successor }))
            .GroupBy(move => (move.FromAlbumId, move.ToAlbumId))
            .Select(group => new AlbumSuccessionMove(
                group.Key.FromAlbumId,
                group.Key.ToAlbumId,
                group.Sum(move => move.TrackCount),
                group.Max(move => move.MovedAt)))
            .OrderBy(move => move.FromAlbumId)
            .ThenBy(move => move.ToAlbumId)];

        return new AlbumSuccessionPlan(successors, carriedFavorites, updates, onlineCovers, passedOn);
    }

    /// <summary>
    /// The albums with tracks reached from <paramref name="start"/>: the album itself if it has tracks, otherwise the
    /// albums its songs moved to, through albums left empty only. Albums unknown to the plan are not reached.
    /// </summary>
    private static HashSet<Guid> Reach(
        Guid start,
        Guid origin,
        Dictionary<Guid, AlbumSuccessionAlbum> albumsById,
        ILookup<Guid, AlbumSuccessionMove> movesFrom)
    {
        HashSet<Guid> reached = [];
        HashSet<Guid> visited = [origin];
        Stack<Guid> pending = new([start]);
        while (pending.TryPop(out Guid current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            if (!albumsById.TryGetValue(current, out AlbumSuccessionAlbum? album))
            {
                continue;
            }

            if (album.HasTracks)
            {
                reached.Add(current);
                continue;
            }

            foreach (AlbumSuccessionMove next in movesFrom[current])
            {
                pending.Push(next.ToAlbumId);
            }
        }

        return reached;
    }
}
