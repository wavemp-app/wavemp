namespace WaveMP.Data.Repositories;

/// <summary>
/// What a smart playlist holds right now.
/// </summary>
/// <param name="TrackCount">Number of tracks.</param>
/// <param name="DurationMilliseconds">Sum of the durations of the tracks.</param>
public sealed record SmartPlaylistSummary(int TrackCount, double DurationMilliseconds);
