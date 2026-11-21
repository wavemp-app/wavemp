namespace WaveMP.Service.Dtos;

/// <summary>
/// The cover of a playlist: an uploaded picture (Custom), else the covers of up to twelve tracks of different albums
/// from the start of the playlist (Tracks), else none.
/// </summary>
public sealed class PlaylistCoverDto
{
    public required PlaylistCoverKindDto Kind { get; init; }

    /// <summary>
    /// Version of the uploaded picture, only with Custom; it changes with every upload, so clients can use it as part
    /// of their cache key.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Ids of the tracks whose covers make up the cover, in the order of the playlist, at most twelve; only with
    /// Tracks, otherwise empty. Their pictures come from the cover endpoint of the tracks. The web app shows the first
    /// four as a 2x2 grid (repeating them when there are fewer) and lets the others take turns in its tiles.
    /// </summary>
    public required IReadOnlyList<Guid> TrackIds { get; init; }
}
