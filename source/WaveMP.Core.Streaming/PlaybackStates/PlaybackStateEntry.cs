using WaveMP.Data.Projections;

namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// An entry of a saved queue with its track.
/// </summary>
/// <param name="Track">The track, with the favorite and rating of the user.</param>
/// <param name="Autoplay">Whether autoplay added the entry after the list had ended.</param>
public sealed record PlaybackStateEntry(TrackRow Track, bool Autoplay);
