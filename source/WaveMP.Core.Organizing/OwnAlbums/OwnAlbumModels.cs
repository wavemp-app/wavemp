using WaveMP.Data.Projections;

namespace WaveMP.Core.Organizing.OwnAlbums;

/// <summary>
/// An own album with its state.
/// </summary>
/// <param name="Album">The stored album.</param>
/// <param name="Status">Whether it was applied and changed since.</param>
public sealed record OwnAlbumItem(OwnAlbumRow Album, OwnAlbumStatus Status);

/// <summary>
/// Whether an own album was applied to the files.
/// </summary>
public enum OwnAlbumStatus
{
    /// <summary>
    /// Never applied.
    /// </summary>
    Draft,

    /// <summary>
    /// Applied and unchanged since.
    /// </summary>
    Applied,

    /// <summary>
    /// Changed since it was applied for the last time.
    /// </summary>
    Changed,
}

/// <summary>
/// A song of an own album with its library data.
/// </summary>
/// <param name="Member">Position and state in the album.</param>
/// <param name="Track">The song.</param>
public sealed record OwnAlbumTrackItem(OwnAlbumMemberRow Member, TrackRow Track);

/// <summary>
/// The values of an own album. Texts are trimmed by the service.
/// </summary>
/// <param name="Title">Title of the album.</param>
/// <param name="AlbumArtist">Album artist.</param>
/// <param name="Year">Year, or <c>null</c> to keep the year of each song.</param>
/// <param name="Genre">Genre, or <c>null</c> to keep the genre of each song.</param>
public sealed record OwnAlbumValues(string Title, string AlbumArtist, int? Year, string? Genre);

/// <summary>
/// Changes of an own album; <c>null</c> keeps a value. <see cref="Year"/> 0 and an empty <see cref="Genre"/> remove
/// the value.
/// </summary>
/// <param name="Title">New title.</param>
/// <param name="AlbumArtist">New album artist.</param>
/// <param name="Year">New year; 0 removes it.</param>
/// <param name="Genre">New genre; empty removes it.</param>
public sealed record OwnAlbumChanges(string? Title, string? AlbumArtist, int? Year, string? Genre);

/// <summary>
/// The outcome of a change of an own album.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Item">The album afterwards, if it succeeded.</param>
/// <param name="TrackIds">The songs a failure is about.</param>
public sealed record OwnAlbumResult(OwnAlbumResultStatus Status, OwnAlbumItem? Item, IReadOnlyList<Guid> TrackIds)
{
    public bool IsOk => Status == OwnAlbumResultStatus.Ok;

    public static OwnAlbumResult Ok(OwnAlbumItem item)
    {
        return new OwnAlbumResult(OwnAlbumResultStatus.Ok, item, []);
    }

    public static OwnAlbumResult Failed(OwnAlbumResultStatus status, IReadOnlyList<Guid>? trackIds = null)
    {
        return new OwnAlbumResult(status, null, trackIds ?? []);
    }
}

public enum OwnAlbumResultStatus
{
    Ok,
    NotFound,

    /// <summary>
    /// A song does not exist, or is not part of the album.
    /// </summary>
    TrackNotFound,

    /// <summary>
    /// A song has no audio file (video only), so its tags cannot be written.
    /// </summary>
    TrackNoAudio,

    /// <summary>
    /// A song is a member of another own album.
    /// </summary>
    TrackInOtherAlbum,

    /// <summary>
    /// More songs than an own album may have.
    /// </summary>
    TooManyTracks,

    /// <summary>
    /// The cover is no JPEG, PNG or WebP picture.
    /// </summary>
    CoverUnsupportedFormat,

    /// <summary>
    /// The cover cannot be stored.
    /// </summary>
    CoverUnavailable,
}

/// <summary>
/// The stored cover of an own album, ready to be delivered. The receiver disposes <paramref name="Content"/>.
/// </summary>
/// <param name="Content">The picture as uploaded.</param>
/// <param name="ContentType">Its content type.</param>
/// <param name="ETag">Strong entity tag of this version (without quotes).</param>
/// <param name="LastModified">When it was uploaded.</param>
public sealed record OwnAlbumCover(Stream Content, string ContentType, string ETag, DateTimeOffset LastModified);
