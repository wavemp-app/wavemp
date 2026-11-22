namespace WaveMP.Data.Repositories;

/// <summary>
/// What happens to the data of albums left empty.
/// </summary>
/// <param name="Successors">Per album left empty that has any: the albums with tracks that got its songs, the main
/// successor first.</param>
/// <param name="Favorites">Album favorites the successors get; an existing favorite keeps the earlier time.</param>
/// <param name="Updates">The successors whose first seen date or cover rule changes.</param>
/// <param name="OnlineCovers">Online covers that move to a main successor.</param>
/// <param name="PassedOnMoves">Moves from albums with tracks into albums left empty, passed on to the successors of
/// the latter; added to an existing move of the same pair.</param>
internal sealed record AlbumSuccessionPlan(
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Successors,
    IReadOnlyList<AlbumSuccessionFavorite> Favorites,
    IReadOnlyList<AlbumSuccessionUpdate> Updates,
    IReadOnlyList<AlbumSuccessionOnlineCover> OnlineCovers,
    IReadOnlyList<AlbumSuccessionMove> PassedOnMoves)
{
    /// <summary>
    /// The main successor of every album left empty that has one.
    /// </summary>
    public IReadOnlyDictionary<Guid, Guid> MainSuccessors =>
        Successors.ToDictionary(entry => entry.Key, entry => entry.Value[0]);
}
