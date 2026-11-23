namespace WaveMP.Data.Repositories;

/// <summary>
/// The part of the carry-over that albums and artists share (see <see cref="AlbumSuccessionPlanner"/> and
/// <see cref="ArtistSuccessionPlanner"/>): who takes over from the nodes left empty, in which order, which favorites
/// they get and which moves are passed on. Pure and deterministic:
/// <list type="bullet">
/// <item><b>Successors</b> of a node left empty: the nodes with songs its songs moved to, also through other nodes
/// left empty (chain A -> B -> C, B empty: C is a successor of A). A node left empty without any is deleted with its
/// data.</item>
/// <item><b>Weight</b> of a successor: the songs of the first move on the way to it (the most, if there are several
/// ways).</item>
/// <item><b>Main successor</b>: the highest weight, then the earliest first seen date, then the smallest id.</item>
/// <item><b>Order of the predecessors</b> of a successor (for values only one node can have): the highest weight,
/// then the earliest first seen date of the predecessor, then the smallest id.</item>
/// <item><b>Favorites</b>: every successor gets the favorites of all its predecessors (union over a merge, to every
/// part of a split), with the earliest time per user.</item>
/// <item><b>First seen date</b>: the earliest of the successor and its predecessors.</item>
/// <item><b>Moves into a node left empty</b> from a node with songs are passed on to the successors of the empty node,
/// so the data still finds the songs when the other node is left empty later; a move to the node itself is
/// dropped.</item>
/// </list>
/// </summary>
internal static class SuccessionPlanner
{
    public static Succession Plan(IReadOnlyCollection<SuccessionNode> nodes, IReadOnlyCollection<SuccessionMove> moves)
    {
        var nodesById = nodes.ToDictionary(node => node.Id);
        ILookup<Guid, SuccessionMove> movesFrom = moves
            .Where(move => move.FromId != move.ToId)
            .ToLookup(move => move.FromId);

        // Weight of every successor per node left empty.
        Dictionary<Guid, Dictionary<Guid, int>> weights = [];
        foreach (SuccessionNode orphan in nodes.Where(node => !node.HasSongs).OrderBy(node => node.Id))
        {
            Dictionary<Guid, int> found = [];
            foreach (SuccessionMove first in movesFrom[orphan.Id])
            {
                foreach (Guid successor in Reach(first.ToId, orphan.Id, nodesById, movesFrom))
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
                .Select(id => nodesById[id])
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
                group => (IReadOnlyList<SuccessionNode>)[.. group
                    .Select(pair => nodesById[pair.Predecessor])
                    .OrderByDescending(predecessor => weights[predecessor.Id][group.Key])
                    .ThenBy(predecessor => predecessor.FirstSeenAt)
                    .ThenBy(predecessor => predecessor.Id)]);

        List<SuccessionMove> passedOn = [.. moves
            .Where(move => nodesById.TryGetValue(move.FromId, out SuccessionNode? from) && from.HasSongs
                && successors.ContainsKey(move.ToId))
            .SelectMany(move => successors[move.ToId]
                .Where(successor => successor != move.FromId)
                .Select(successor => move with { ToId = successor }))
            .GroupBy(move => (move.FromId, move.ToId))
            .Select(group => new SuccessionMove(
                group.Key.FromId,
                group.Key.ToId,
                group.Sum(move => move.TrackCount),
                group.Max(move => move.MovedAt)))
            .OrderBy(move => move.FromId)
            .ThenBy(move => move.ToId)];

        return new Succession(successors, predecessors, passedOn);
    }

    /// <summary>
    /// The favorites the successors get: every favorite of a node left empty goes to each of its successors; a user
    /// keeps one favorite per successor, with the earliest time. Ordered by successor, then user.
    /// </summary>
    public static IReadOnlyList<SuccessionFavorite> CarryFavorites(
        Succession succession,
        IEnumerable<SuccessionFavorite> favorites)
    {
        return [.. favorites
            .Where(favorite => succession.Successors.ContainsKey(favorite.TargetId))
            .SelectMany(favorite => succession.Successors[favorite.TargetId]
                .Select(successor => favorite with { TargetId = successor }))
            .GroupBy(favorite => (favorite.UserId, favorite.TargetId))
            .Select(group => group.MinBy(favorite => favorite.CreatedAt)!)
            .OrderBy(favorite => favorite.TargetId)
            .ThenBy(favorite => favorite.UserId)];
    }

    /// <summary>
    /// The earliest first seen date of a successor and its predecessors.
    /// </summary>
    public static DateTimeOffset EarliestFirstSeenAt(
        DateTimeOffset successorFirstSeenAt,
        IReadOnlyList<SuccessionNode> predecessors)
    {
        return predecessors.Select(predecessor => predecessor.FirstSeenAt).Append(successorFirstSeenAt).Min();
    }

    /// <summary>
    /// The nodes with songs reached from <paramref name="start"/>: the node itself if it has songs, otherwise the
    /// nodes its songs moved to, through nodes left empty only. Nodes unknown to the plan are not reached.
    /// </summary>
    private static HashSet<Guid> Reach(
        Guid start,
        Guid origin,
        Dictionary<Guid, SuccessionNode> nodesById,
        ILookup<Guid, SuccessionMove> movesFrom)
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

            if (!nodesById.TryGetValue(current, out SuccessionNode? node))
            {
                continue;
            }

            if (node.HasSongs)
            {
                reached.Add(current);
                continue;
            }

            foreach (SuccessionMove next in movesFrom[current])
            {
                pending.Push(next.ToId);
            }
        }

        return reached;
    }
}
