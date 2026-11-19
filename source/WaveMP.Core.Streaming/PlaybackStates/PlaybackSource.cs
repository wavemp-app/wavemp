using WaveMP.Data.Entities;

namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// What a list in the player was started from.
/// </summary>
/// <param name="Kind">An album, an artist, a playlist or a smart playlist.</param>
/// <param name="Id">Its id.</param>
public sealed record PlaybackSource(PlaybackSourceKind Kind, Guid Id);
