namespace WaveMP.Core.Scanning.Writers;

/// <summary>
/// The changes of the tracks of a folder. A track the folder kept from another place (a song whose folder was renamed
/// or moved or whose files were renamed) counts as updated and is listed in <see cref="AdoptedTrackIds"/>.
/// </summary>
public sealed record FolderWriteResult(int TracksAdded, int TracksUpdated, int TracksRemoved)
{
    /// <summary>
    /// The ids of the tracks the folder kept from another place, ordered.
    /// </summary>
    public IReadOnlyList<Guid> AdoptedTrackIds { get; init; } = [];
}
