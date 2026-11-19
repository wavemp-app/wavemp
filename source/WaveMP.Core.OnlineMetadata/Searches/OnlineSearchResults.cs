using WaveMP.Core.OnlineMetadata.Matches;
using WaveMP.Core.Scanning.Edits;

namespace WaveMP.Core.OnlineMetadata.Searches;

/// <summary>
/// The outcome of an online search.
/// </summary>
public enum OnlineSearchStatus
{
    /// <summary>
    /// The search ran; the list of matches may be empty.
    /// </summary>
    Searched,

    /// <summary>
    /// The song or album does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// The online search is switched off; no request was sent.
    /// </summary>
    Disabled,

    /// <summary>
    /// MusicBrainz cannot be reached (no internet connection) or failed.
    /// </summary>
    Unavailable,
}

/// <summary>
/// The matches of a song, the best first, next to its current tags.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="Current">The current tags and whether they can be written; with <see cref="OnlineSearchStatus.Searched"/>
/// only.</param>
/// <param name="Matches">The matches, the best first.</param>
public sealed record TrackSearchResult(
    OnlineSearchStatus Status,
    TrackMetadata? Current,
    IReadOnlyList<TrackMatch> Matches)
{
    public static TrackSearchResult Failure(OnlineSearchStatus status)
    {
        return new TrackSearchResult(status, null, []);
    }
}

/// <summary>
/// The current tags of an album: of the album in the library, with how many of its songs can be written.
/// </summary>
/// <param name="Values">The values of the album.</param>
/// <param name="TrackCount">Number of songs of the album.</param>
/// <param name="EditableTrackCount">Number of songs whose audio file can be written.</param>
public sealed record AlbumCurrent(OnlineAlbumValues Values, int TrackCount, int EditableTrackCount);

/// <summary>
/// The matches of an album, the best first, next to its current tags.
/// </summary>
/// <param name="Status">The outcome.</param>
/// <param name="Current">The current tags; with <see cref="OnlineSearchStatus.Searched"/> only.</param>
/// <param name="Matches">The matches, the best first.</param>
public sealed record AlbumSearchResult(
    OnlineSearchStatus Status,
    AlbumCurrent? Current,
    IReadOnlyList<AlbumMatch> Matches)
{
    public static AlbumSearchResult Failure(OnlineSearchStatus status)
    {
        return new AlbumSearchResult(status, null, []);
    }
}

/// <summary>
/// The front cover of a release for a preview.
/// </summary>
/// <param name="Status">Found, not found (the release has no cover), disabled or unavailable.</param>
/// <param name="Picture">The picture; with <see cref="Clients.OnlineStatus.Found"/> only.</param>
/// <param name="ContentType">The type of the picture, recognized by its content.</param>
public sealed record OnlineCoverPreview(Clients.OnlineStatus Status, byte[]? Picture, string? ContentType);
