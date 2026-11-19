namespace WaveMP.Core.Autoplay.Services;

/// <summary>
/// Asks for the songs autoplay plays after a list.
/// </summary>
/// <param name="ListTrackIds">The tracks of the list that ended (album, playlist or queue), in their order; the last
/// <see cref="Selections.AutoplayLimits.MaxListTracks"/> count.</param>
/// <param name="ExcludedTrackIds">Tracks that must not be chosen, e.g. the songs that already wait in the autoplay list
/// of the client.</param>
/// <param name="Count">Number of songs wanted, see <see cref="Selections.AutoplayLimits"/>.</param>
public sealed record AutoplayCommand(
    IReadOnlyList<Guid> ListTrackIds,
    IReadOnlyCollection<Guid> ExcludedTrackIds,
    int Count);
