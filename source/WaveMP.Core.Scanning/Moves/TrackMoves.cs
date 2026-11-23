using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Moves;

/// <summary>
/// The songs a scan found at a new place (see <see cref="TrackMovePlanner"/>): the new files by their place, and the
/// vanished tracks they may keep. A vanished track of a move is deferred: the folder writes of the scan leave it and
/// its stored files alone until a write keeps it at its new place, and the scan deletes it at its end if none did
/// (see <see cref="ILibraryScanRepository.DeleteVanishedTracksAsync"/>).
/// </summary>
public sealed class TrackMoves
{
    private readonly Dictionary<(Guid SourceId, string RelativePath), TrackMove> _byNewFile;
    private readonly Dictionary<Guid, VanishedTrack> _deferred;

    /// <param name="moves">The moves, at most one per new file and per vanished track.</param>
    /// <param name="deferred">The vanished tracks of the moves.</param>
    public TrackMoves(IEnumerable<TrackMove> moves, IEnumerable<VanishedTrack> deferred)
    {
        Moves = [.. moves
            .OrderBy(move => move.SourceId)
            .ThenBy(move => move.RelativePath, StringComparer.Ordinal)];
        Deferred = [.. deferred.OrderBy(track => track.TrackId)];
        _byNewFile = Moves.ToDictionary(move => (move.SourceId, move.RelativePath));
        _deferred = Deferred.ToDictionary(track => track.TrackId);
    }

    /// <summary>
    /// No moves, for writes that keep every song in its place (e.g. forming the albums again or a changed file
    /// selection).
    /// </summary>
    public static TrackMoves None { get; } = new([], []);

    /// <summary>
    /// The moves, ordered by the source and the path of the new file.
    /// </summary>
    public IReadOnlyList<TrackMove> Moves { get; }

    /// <summary>
    /// The vanished tracks of the moves with their stored files, ordered by id.
    /// </summary>
    public IReadOnlyList<VanishedTrack> Deferred { get; }

    public bool IsEmpty => Moves.Count == 0;

    /// <summary>
    /// The move of a new file, or <c>null</c> if the file is no move.
    /// </summary>
    public TrackMove? Find(Guid sourceId, string relativePath)
    {
        return _byNewFile.GetValueOrDefault((sourceId, relativePath));
    }

    /// <summary>
    /// Whether a track is the vanished track of a move.
    /// </summary>
    public bool IsDeferred(Guid trackId)
    {
        return _deferred.ContainsKey(trackId);
    }

    /// <summary>
    /// The vanished track of a move with its stored files, or <c>null</c> if the track is none.
    /// </summary>
    public VanishedTrack? FindDeferred(Guid trackId)
    {
        return _deferred.GetValueOrDefault(trackId);
    }
}
