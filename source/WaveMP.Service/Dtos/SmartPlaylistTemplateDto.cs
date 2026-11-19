namespace WaveMP.Service.Dtos;

/// <summary>
/// A built-in template of smart playlists: the rules, sort order and limit a new smart playlist can start with. The
/// client names it in the language of the user and sends it with a name to CreateMySmartPlaylist.
/// </summary>
public sealed class SmartPlaylistTemplateDto
{
    public required SmartPlaylistTemplateIdDto Id { get; init; }

    public required SmartPlaylistMatchDto Match { get; init; }

    public required IReadOnlyList<SmartPlaylistRuleDto> Rules { get; init; }

    public required SmartPlaylistTrackSortDto Sort { get; init; }

    public required SortDirectionDto Direction { get; init; }

    /// <summary>
    /// Largest number of songs; null for all matching songs.
    /// </summary>
    public int? Limit { get; init; }
}
