using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A smart playlist to create or to replace: its name, rules, sort order and limit.
/// </summary>
public sealed class SaveSmartPlaylistDto
{
    /// <summary>
    /// Name, 1 to 100 characters; spaces at the ends are removed. Names do not need to be unique.
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }

    /// <summary>
    /// How the rules are combined; All if missing.
    /// </summary>
    public SmartPlaylistMatchDto? Match { get; init; }

    /// <summary>
    /// The rules, at most 20; without rules every song of the library belongs to the smart playlist.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public required IReadOnlyList<SaveSmartPlaylistRuleDto> Rules { get; init; }

    /// <summary>
    /// Sort key of the songs; Artist if missing.
    /// </summary>
    public SmartPlaylistTrackSortDto? Sort { get; init; }

    /// <summary>
    /// Sort direction; ascending if missing.
    /// </summary>
    public SortDirectionDto? Direction { get; init; }

    /// <summary>
    /// Largest number of songs, 1 to 10000, the first ones in the sort order; all matching songs if missing.
    /// </summary>
    [Range(1, 10_000)]
    public int? Limit { get; init; }
}
