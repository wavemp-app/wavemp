namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// An entry of the queue as a client saves it.
/// </summary>
/// <param name="TrackId">The track of the entry.</param>
/// <param name="Autoplay">Whether autoplay added the entry after the list had ended.</param>
public sealed record PlaybackStateItem(Guid TrackId, bool Autoplay);
