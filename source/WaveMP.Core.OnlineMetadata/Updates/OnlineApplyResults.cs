using WaveMP.Core.OnlineMetadata.Covers;
using WaveMP.Core.Scanning.Edits;

namespace WaveMP.Core.OnlineMetadata.Updates;

/// <summary>
/// Why nothing of a match was applied.
/// </summary>
public enum OnlineApplyError
{
    /// <summary>
    /// The online search is switched off.
    /// </summary>
    Disabled,

    /// <summary>
    /// The song does not exist.
    /// </summary>
    TrackNotFound,

    /// <summary>
    /// The album does not exist.
    /// </summary>
    AlbumNotFound,

    /// <summary>
    /// The song has no audio file; tags are written into audio files only.
    /// </summary>
    NoAudio,

    /// <summary>
    /// The audio file of the song cannot be written.
    /// </summary>
    NotWritable,

    /// <summary>
    /// The release has no cover in the Cover Art Archive.
    /// </summary>
    CoverNotFound,

    /// <summary>
    /// The Cover Art Archive cannot be reached or failed.
    /// </summary>
    Unavailable,
}

/// <summary>
/// The result of applying a match to a song: its metadata read again and where the cover went, or why nothing was
/// applied.
/// </summary>
public sealed record TrackApplyResult(OnlineApplyError? Error, TrackMetadata? Metadata, CoverSaveOutcome Cover)
{
    public static TrackApplyResult Failure(OnlineApplyError error)
    {
        return new TrackApplyResult(error, null, CoverSaveOutcome.None);
    }
}

/// <summary>
/// The result of applying a match to an album: how many songs were written and skipped (no audio file or not
/// writable), the id of the album afterwards (a new title or album artist can make it another album) and where the
/// cover went, or why nothing was applied.
/// </summary>
public sealed record AlbumApplyResult(
    OnlineApplyError? Error,
    Guid? AlbumId,
    int TracksWritten,
    int TracksSkipped,
    CoverSaveOutcome Cover)
{
    public static AlbumApplyResult Failure(OnlineApplyError error)
    {
        return new AlbumApplyResult(error, null, 0, 0, CoverSaveOutcome.None);
    }
}
