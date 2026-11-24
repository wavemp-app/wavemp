namespace WaveMP.Core.Library.Groups;

/// <summary>
/// A run of consecutive entries of a list with the same key.
/// </summary>
/// <param name="First">The first entry of the run.</param>
/// <param name="Position">Index of the first entry in the list, starting at 0.</param>
/// <param name="Count">Number of entries of the run, at least 1.</param>
public sealed record GroupRun<TRow>(TRow First, int Position, int Count);
