namespace WaveMP.Service.Dtos;

/// <summary>
/// An artist of MusicBrainz that may be the artist of the library, for the online lookup of artist pictures. All texts
/// are plain text as MusicBrainz names them.
/// </summary>
public sealed class MusicBrainzArtistMatchDto
{
    /// <summary>
    /// The MusicBrainz id of the artist.
    /// </summary>
    public required Guid MusicBrainzArtistId { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Name for sorting, e.g. "Beatles, The".
    /// </summary>
    public string? SortName { get; init; }

    /// <summary>
    /// Comment that tells artists of the same name apart.
    /// </summary>
    public string? Disambiguation { get; init; }

    /// <summary>
    /// Kind of artist as MusicBrainz names it, e.g. Person or Group (not translated).
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Country code of the artist, e.g. GB.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// Begin of the life span or career: a year, year and month or a date (e.g. 1960 or 1960-08).
    /// </summary>
    public string? LifeSpanBegin { get; init; }

    /// <summary>
    /// End of the life span or career, in the same form.
    /// </summary>
    public string? LifeSpanEnd { get; init; }

    /// <summary>
    /// Relevance of the search, 0 to 100.
    /// </summary>
    public required int Score { get; init; }

    /// <summary>
    /// Whether an admin chose this MusicBrainz artist for the artist before.
    /// </summary>
    public required bool IsRemembered { get; init; }
}
