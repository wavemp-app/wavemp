namespace WaveMP.Data.Repositories;

/// <summary>
/// Decides where the data of artists left empty goes (favorites of all profiles, first seen date, the source of a list
/// in the player, the uploaded and the online picture, the MusicBrainz artist), from the stored moves of their songs.
/// Pure, so every rule is deterministic and testable without a database. The rules are those of the albums
/// (<see cref="SuccessionPlanner"/>, <see cref="AlbumSuccessionPlanner"/>):
/// <list type="bullet">
/// <item><b>Successors</b> of an artist left empty: the artists with songs its songs moved to, also through other
/// artists left empty. An artist left empty without any is deleted with its data as before.</item>
/// <item><b>Main successor</b>: the artist that got the most songs, then the earliest first seen date, then the
/// smallest id. It alone takes over values only one artist can have.</item>
/// <item><b>Favorites</b>: every successor gets the artist favorites of all its predecessors (union over a merge, to
/// every artist of a split), with the earliest time per user.</item>
/// <item><b>Uploaded picture</b> and <b>online picture</b>, each kind on its own, and the <b>MusicBrainz
/// artist</b>: only to the main successor and only if it has none, from the first predecessor (in the order of the
/// predecessors, among those it is the main successor of) that has one; never two pictures of one kind per artist.
/// The others are removed with their artist.</item>
/// <item><b>First seen date</b>: the earliest of the successor and its predecessors.</item>
/// <item><b>Moves into an artist left empty</b> from an artist with songs are passed on to the successors of the empty
/// artist; a move to the artist itself is dropped.</item>
/// </list>
/// </summary>
internal static class ArtistSuccessionPlanner
{
    public static ArtistSuccessionPlan Plan(
        IReadOnlyCollection<ArtistSuccessionArtist> artists,
        IReadOnlyCollection<ArtistSuccessionMove> moves,
        IReadOnlyCollection<ArtistSuccessionFavorite> favorites)
    {
        var artistsById = artists.ToDictionary(artist => artist.Id);
        Succession succession = SuccessionPlanner.Plan(
            [.. artists.Select(artist => new SuccessionNode(artist.Id, artist.HasSongs, artist.FirstSeenAt))],
            [.. moves.Select(move =>
                new SuccessionMove(move.FromArtistId, move.ToArtistId, move.TrackCount, move.MovedAt))]);

        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> successors = succession.Successors;
        List<ArtistSuccessionUpdate> updates = [];
        List<ArtistSuccessionPicture> pictures = [];
        foreach ((Guid successorId, IReadOnlyList<SuccessionNode> nodes) in succession.Predecessors
            .OrderBy(entry => entry.Key))
        {
            ArtistSuccessionArtist successor = artistsById[successorId];
            DateTimeOffset firstSeenAt = SuccessionPlanner.EarliestFirstSeenAt(successor.FirstSeenAt, nodes);

            // Values only one artist can have come from the artists this one is the main successor of.
            List<ArtistSuccessionArtist> before = [.. nodes
                .Where(node => successors[node.Id][0] == successorId)
                .Select(node => artistsById[node.Id])];
            Guid? musicBrainzArtistId = successor.MusicBrainzArtistId
                ?? before.Select(predecessor => predecessor.MusicBrainzArtistId).FirstOrDefault(id => id is not null);
            if (firstSeenAt != successor.FirstSeenAt || musicBrainzArtistId != successor.MusicBrainzArtistId)
            {
                updates.Add(new ArtistSuccessionUpdate(successorId, firstSeenAt, musicBrainzArtistId));
            }

            if (successor.UploadedPictureId is null
                && before.Select(predecessor => predecessor.UploadedPictureId).FirstOrDefault(id => id is not null)
                    is { } uploadedPictureId)
            {
                pictures.Add(new ArtistSuccessionPicture(uploadedPictureId, successorId));
            }

            if (successor.OnlinePictureId is null
                && before.Select(predecessor => predecessor.OnlinePictureId).FirstOrDefault(id => id is not null)
                    is { } onlinePictureId)
            {
                pictures.Add(new ArtistSuccessionPicture(onlinePictureId, successorId));
            }
        }

        List<ArtistSuccessionFavorite> carriedFavorites = [.. SuccessionPlanner
            .CarryFavorites(
                succession,
                favorites.Select(favorite =>
                    new SuccessionFavorite(favorite.UserId, favorite.ArtistId, favorite.CreatedAt)))
            .Select(favorite =>
                new ArtistSuccessionFavorite(favorite.UserId, favorite.TargetId, favorite.CreatedAt))];

        List<ArtistSuccessionMove> passedOn = [.. succession.PassedOnMoves
            .Select(move => new ArtistSuccessionMove(move.FromId, move.ToId, move.TrackCount, move.MovedAt))];

        return new ArtistSuccessionPlan(successors, carriedFavorites, updates, pictures, passedOn);
    }
}
