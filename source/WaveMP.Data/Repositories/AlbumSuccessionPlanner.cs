using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Decides where the data of albums left empty goes (favorites of all profiles, cover rule, online cover, first seen
/// date), from the stored moves of their songs. Pure, so every rule is deterministic and testable without a database.
/// Successors, their order, the favorites, the first seen date and the passed-on moves follow the rules the albums
/// share with the artists (<see cref="SuccessionPlanner"/>):
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
        Succession succession = SuccessionPlanner.Plan(
            [.. albums.Select(album => new SuccessionNode(album.Id, album.HasTracks, album.FirstSeenAt))],
            [.. moves.Select(move =>
                new SuccessionMove(move.FromAlbumId, move.ToAlbumId, move.TrackCount, move.MovedAt))]);
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> successors = succession.Successors;

        List<AlbumSuccessionUpdate> updates = [];
        List<AlbumSuccessionOnlineCover> onlineCovers = [];
        foreach ((Guid successorId, IReadOnlyList<SuccessionNode> nodes) in succession.Predecessors
            .OrderBy(entry => entry.Key))
        {
            AlbumSuccessionAlbum successor = albumsById[successorId];
            List<AlbumSuccessionAlbum> before = [.. nodes.Select(node => albumsById[node.Id])];
            DateTimeOffset firstSeenAt = SuccessionPlanner.EarliestFirstSeenAt(successor.FirstSeenAt, nodes);
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

        List<AlbumSuccessionFavorite> carriedFavorites = [.. SuccessionPlanner
            .CarryFavorites(
                succession,
                favorites.Select(favorite =>
                    new SuccessionFavorite(favorite.UserId, favorite.AlbumId, favorite.CreatedAt)))
            .Select(favorite => new AlbumSuccessionFavorite(favorite.UserId, favorite.TargetId, favorite.CreatedAt))];

        List<AlbumSuccessionMove> passedOn = [.. succession.PassedOnMoves
            .Select(move => new AlbumSuccessionMove(move.FromId, move.ToId, move.TrackCount, move.MovedAt))];

        return new AlbumSuccessionPlan(successors, carriedFavorites, updates, onlineCovers, passedOn);
    }
}
