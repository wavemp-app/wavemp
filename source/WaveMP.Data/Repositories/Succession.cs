namespace WaveMP.Data.Repositories;

/// <summary>
/// Who takes over from the albums or artists left empty, by <see cref="SuccessionPlanner"/>.
/// </summary>
/// <param name="Successors">Per node left empty that has any: the nodes with songs that got its songs, the main
/// successor first.</param>
/// <param name="Predecessors">Per successor: the nodes left empty it takes over from, in their order (for values only
/// one node can have).</param>
/// <param name="PassedOnMoves">Moves from nodes with songs into nodes left empty, passed on to the successors of the
/// latter; added to an existing move of the same pair.</param>
internal sealed record Succession(
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Successors,
    IReadOnlyDictionary<Guid, IReadOnlyList<SuccessionNode>> Predecessors,
    IReadOnlyList<SuccessionMove> PassedOnMoves);
