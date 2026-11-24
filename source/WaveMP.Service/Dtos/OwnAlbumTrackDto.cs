namespace WaveMP.Service.Dtos;

/// <summary>
/// A song of an own album.
/// </summary>
public sealed class OwnAlbumTrackDto
{
    /// <summary>
    /// Index of the song in the album, starting at 0; its track number becomes position + 1.
    /// </summary>
    public required int Position { get; init; }

    public required OwnAlbumTrackStateDto State { get; init; }

    /// <summary>
    /// When the song was added.
    /// </summary>
    public required DateTimeOffset AddedAt { get; init; }

    public required TrackDto Track { get; init; }
}
