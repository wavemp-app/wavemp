namespace WaveMP.Service.Dtos;

/// <summary>
/// A genre of the tracks; genres whose names differ only in case, diacritics, spaces or punctuation are one genre.
/// </summary>
public sealed class GenreDto
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Name of the genre as first found in the tracks; use it as filter genre of the lists.
    /// </summary>
    public required string Name { get; init; }

    public required int TrackCount { get; init; }

    /// <summary>
    /// Albums with tracks of the genre.
    /// </summary>
    public required int AlbumCount { get; init; }
}
