namespace WaveMP.Service.Dtos;

/// <summary>
/// An own album: an album the admin puts together from songs of the library. Applying it (an organize preview of kind
/// OwnAlbum, then apply) writes its values into the tags of the audio files and moves the files by the layout
/// template.
/// </summary>
public sealed class OwnAlbumDto
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public required string AlbumArtist { get; init; }

    /// <summary>
    /// Year written into the songs; null keeps the year of each song.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Genre written into the songs; null keeps the genre of each song.
    /// </summary>
    public string? Genre { get; init; }

    /// <summary>
    /// Whether a cover was uploaded (GetOwnAlbumCover); it is written as cover.{ext} into the album folder.
    /// </summary>
    public required bool HasCover { get; init; }

    /// <summary>
    /// When the cover was uploaded; also its version for caches. Null without cover.
    /// </summary>
    public DateTimeOffset? CoverUpdatedAt { get; init; }

    /// <summary>
    /// Number of songs that are members.
    /// </summary>
    public required int TrackCount { get; init; }

    /// <summary>
    /// Number of songs taken out since the last apply; the next apply restores their original tags.
    /// </summary>
    public required int RemovedTrackCount { get; init; }

    public required OwnAlbumStatusDto Status { get; init; }

    /// <summary>
    /// When the album was applied for the last time.
    /// </summary>
    public DateTimeOffset? AppliedAt { get; init; }

    /// <summary>
    /// The album of the library its first song belongs to, once the album was applied.
    /// </summary>
    public Guid? LibraryAlbumId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
