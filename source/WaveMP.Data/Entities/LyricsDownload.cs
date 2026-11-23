namespace WaveMP.Data.Entities;

/// <summary>
/// The result of looking up the lyrics of a track in the online lyrics service, one per track. The download job skips
/// tracks with a result, except those whose lookup found no lyrics a while ago (not found or instrumental) or that
/// already had lyrics a while ago or before a scan read their files again, which it looks up again; an admin can look
/// up a track again at any time. Lyrics that could not be saved as <c>.lrc</c> file
/// next to the track (folder not writable) are kept here. Removed with its track; kept when a rescan finds the track
/// at a new place, because the track keeps its id.
/// </summary>
public class LyricsDownload
{
    public required Guid TrackId { get; set; }

    public Track? Track { get; set; }

    public required LyricsDownloadStatus Status { get; set; }

    /// <summary>
    /// The lyrics in LRC format (synchronized lines with timestamps, or plain lines without), only for
    /// <see cref="LyricsDownloadStatus.SavedToDatabase"/>.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// When the track was looked up.
    /// </summary>
    public required DateTimeOffset CheckedAt { get; set; }
}
