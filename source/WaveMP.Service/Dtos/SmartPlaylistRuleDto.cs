namespace WaveMP.Service.Dtos;

/// <summary>
/// A rule of a smart playlist. Only the values the operator needs are set.
/// </summary>
public sealed class SmartPlaylistRuleDto
{
    public required SmartPlaylistFieldDto Field { get; init; }

    public required SmartPlaylistOperatorDto Operator { get; init; }

    /// <summary>
    /// Text of a text field.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Number of a number field (duration in seconds), the lower bound of Between or the number of days of a day field.
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// Upper bound of Between, included.
    /// </summary>
    public int? NumberTo { get; init; }
}
