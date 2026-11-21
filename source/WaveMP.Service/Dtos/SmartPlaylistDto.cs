namespace WaveMP.Service.Dtos;

/// <summary>
/// A smart playlist of the current profile: rules that pick songs from the library. Its songs are computed on every
/// request, so it always reflects the current library.
/// </summary>
public sealed class SmartPlaylistDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required SmartPlaylistMatchDto Match { get; init; }

    /// <summary>
    /// The rules in their order.
    /// </summary>
    public required IReadOnlyList<SmartPlaylistRuleDto> Rules { get; init; }

    public required SmartPlaylistTrackSortDto Sort { get; init; }

    public required SortDirectionDto Direction { get; init; }

    /// <summary>
    /// Largest number of songs; null for all matching songs.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Number of songs right now.
    /// </summary>
    public required int TrackCount { get; init; }

    /// <summary>
    /// Total duration of the songs right now in milliseconds.
    /// </summary>
    public required long DurationMilliseconds { get; init; }

    /// <summary>
    /// The covers of up to twelve of its first songs (Tracks), else none; never Custom.
    /// </summary>
    public required PlaylistCoverDto Cover { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the name or the rules changed last.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
