namespace WaveMP.Core.Autoplay.Selections;

/// <summary>
/// A method that chooses the songs autoplay plays after a list. The choice by the artists of the list
/// (<see cref="ArtistAutoplaySelector"/>) is the method of WaveMP; later methods (e.g. by tempo or sound) implement
/// this interface as well.
/// </summary>
public interface IAutoplaySelector
{
    /// <summary>
    /// The ids of at most <see cref="AutoplayRequest.Count"/> songs in the order they play; empty if no matching song
    /// is found.
    /// </summary>
    Task<IReadOnlyList<Guid>> SelectAsync(AutoplayRequest request, CancellationToken cancellationToken = default);
}
