namespace WaveMP.Data.Entities;

/// <summary>
/// A play waiting to be sent to a scrobbling service. The queue lives in the database, so scrobbles survive outages
/// of the service and restarts of WaveMP. The entry keeps the data of the track as it was played, so it is still sent
/// after a scan removed the track. Removed once the service accepted or rejected it, when it is too old and with its
/// connection.
/// </summary>
public class ScrobbleQueueEntry
{
    public Guid Id { get; set; }

    public required Guid ConnectionId { get; set; }

    public ScrobbleConnection? Connection { get; set; }

    public required string Artist { get; set; }

    public required string Title { get; set; }

    public string? Album { get; set; }

    public string? AlbumArtist { get; set; }

    public int? TrackNumber { get; set; }

    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// When the playback of the track started; the time of the scrobble.
    /// </summary>
    public required DateTimeOffset PlayedAt { get; set; }

    /// <summary>
    /// Failed attempts to send the entry so far.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// The entry is not sent before this time; later after every failed attempt.
    /// </summary>
    public required DateTimeOffset NextAttemptAt { get; set; }
}
