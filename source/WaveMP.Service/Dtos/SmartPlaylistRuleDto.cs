namespace WaveMP.Service.Dtos;

/// <summary>
/// A rule of a smart playlist. Only the values the operator needs are set.
/// </summary>
public sealed class SmartPlaylistRuleDto
{
    public required SmartPlaylistFieldDto Field { get; init; }

    public required SmartPlaylistOperatorDto Operator { get; init; }

    /// <summary>
    /// Text of a text field. For a rule with <c>referenceId</c>, the current name of the artist, album or genre.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// The artist, album or genre a rule with Is or IsNot names, by id; the rule follows a new spelling of its name.
    /// Such a rule without an id names no single entry of the library (no entry has that name, or several albums do)
    /// and compares the normalized name of its text: lower case, without diacritics, spaces and punctuation. Clients
    /// mark these rules.
    /// </summary>
    public Guid? ReferenceId { get; init; }

    /// <summary>
    /// Number of a number field (duration in seconds), the lower bound of Between or the number of days of a day field.
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// Upper bound of Between, included.
    /// </summary>
    public int? NumberTo { get; init; }
}
