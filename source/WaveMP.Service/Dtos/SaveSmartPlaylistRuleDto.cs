using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A rule of a smart playlist to save. Values the operator does not need are ignored.
/// </summary>
public sealed class SaveSmartPlaylistRuleDto
{
    /// <summary>
    /// The field of the song.
    /// </summary>
    [Required]
    public required SmartPlaylistFieldDto? Field { get; init; }

    /// <summary>
    /// The operator; it has to fit the kind of the field.
    /// </summary>
    [Required]
    public required SmartPlaylistOperatorDto? Operator { get; init; }

    /// <summary>
    /// Text of a text field, 1 to 200 characters; spaces at the ends are removed.
    /// </summary>
    [StringLength(200)]
    public string? Text { get; init; }

    /// <summary>
    /// Number of a number field (duration in seconds), the lower bound of Between or the number of days of a day field;
    /// the range depends on the field (see SmartPlaylistField).
    /// </summary>
    public int? Number { get; init; }

    /// <summary>
    /// Upper bound of Between, from <c>number</c> to the largest value of the field.
    /// </summary>
    public int? NumberTo { get; init; }
}
