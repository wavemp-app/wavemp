namespace WaveMP.Service.Dtos;

/// <summary>
/// Values of tags of an audio file. Only the fields named in fields count; a named field without value is removed.
/// </summary>
public sealed class OrganizeTagValuesDto
{
    public required IReadOnlyList<OrganizeTagFieldDto> Fields { get; init; }

    public string? Album { get; init; }

    public required IReadOnlyList<string> AlbumArtists { get; init; }

    public int? TrackNumber { get; init; }

    public int? TrackCount { get; init; }

    public int? DiscNumber { get; init; }

    public int? Year { get; init; }

    public required IReadOnlyList<string> Genres { get; init; }
}
