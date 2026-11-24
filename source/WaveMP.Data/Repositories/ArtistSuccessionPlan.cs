namespace WaveMP.Data.Repositories;

/// <summary>
/// What happens to the data of artists left empty.
/// </summary>
/// <param name="Successors">Per artist left empty that has any: the artists with songs that got its songs, the main
/// successor first.</param>
/// <param name="Favorites">Artist favorites the successors get; an existing favorite keeps the earlier time.</param>
/// <param name="Updates">The successors whose first seen date or MusicBrainz artist changes.</param>
/// <param name="Pictures">Uploaded and online pictures that move to a main successor.</param>
/// <param name="PassedOnMoves">Moves from artists with songs into artists left empty, passed on to the successors of
/// the latter; added to an existing move of the same pair.</param>
internal sealed record ArtistSuccessionPlan(
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Successors,
    IReadOnlyList<ArtistSuccessionFavorite> Favorites,
    IReadOnlyList<ArtistSuccessionUpdate> Updates,
    IReadOnlyList<ArtistSuccessionPicture> Pictures,
    IReadOnlyList<ArtistSuccessionMove> PassedOnMoves)
{
    /// <summary>
    /// The main successor of every artist left empty that has one: the artist that takes over the values only one
    /// artist can have, such as a list in the player started from the artist left empty, its pictures and its
    /// MusicBrainz artist.
    /// </summary>
    public IReadOnlyDictionary<Guid, Guid> MainSuccessors =>
        Successors.ToDictionary(entry => entry.Key, entry => entry.Value[0]);
}
