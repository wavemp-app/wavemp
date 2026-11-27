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
    /// <see cref="LyricsDownloadStatus.SavedToDatabase"/> and for <see cref="LyricsDownloadStatus.Uploaded"/> lyrics
    /// that could not be saved as file.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Whether the saved lyrics belong to another version of the song (its duration differs too much), so only their
    /// plain lines were kept, without the times, which would not fit the track.
    /// </summary>
    public bool OtherVersion { get; set; }

    /// <summary>
    /// The version of the rules that chose the lyrics (<c>LrclibMatcher.Version</c>); lookups that found no lyrics
    /// with older rules are looked up again. 1 for lookups made before the version was stored.
    /// </summary>
    public int MatcherVersion { get; set; }

    /// <summary>
    /// When the track was looked up.
    /// </summary>
    public required DateTimeOffset CheckedAt { get; set; }
}
