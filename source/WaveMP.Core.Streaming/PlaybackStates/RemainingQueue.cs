namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// The entries of a saved queue whose tracks still exist.
/// </summary>
/// <param name="Positions">The saved positions of the remaining entries, in their order.</param>
/// <param name="CurrentIndex">The new position of the current entry; -1 without one.</param>
/// <param name="CurrentKept">Whether the saved current entry remains; otherwise its position no longer applies.
/// </param>
/// <param name="AutoplayPositions">The new positions of the entries autoplay added.</param>
/// <param name="OriginalOrder">The new positions in the order before shuffling.</param>
public sealed record RemainingQueue(
    IReadOnlyList<int> Positions,
    int CurrentIndex,
    bool CurrentKept,
    IReadOnlyList<int> AutoplayPositions,
    IReadOnlyList<int> OriginalOrder);
