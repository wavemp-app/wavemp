namespace WaveMP.Core.Autoplay.Selections;

/// <summary>
/// What a method of autoplay chooses the next songs for.
/// </summary>
/// <param name="UserId">The user who listens; their ratings, favorites and plays count.</param>
/// <param name="ListTrackIds">The tracks of the list that ended (album, playlist or queue), in their order. They are
/// never chosen again.</param>
/// <param name="ExcludedTrackIds">Further tracks that must not be chosen, e.g. the songs that already wait in the
/// autoplay list of the client.</param>
/// <param name="Count">Number of songs wanted.</param>
/// <param name="IncludeVideoOnly">Whether songs that only have a music video may be chosen.</param>
public sealed record AutoplayRequest(
    Guid UserId,
    IReadOnlyList<Guid> ListTrackIds,
    IReadOnlyCollection<Guid> ExcludedTrackIds,
    int Count,
    bool IncludeVideoOnly);
