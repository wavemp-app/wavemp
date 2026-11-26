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
    /// The artist, album or genre a rule with Is or IsNot names, by id, e.g. as received with the rule or chosen from
    /// the library; the text is still required. Without an id, or with one that names no entry of the field, the
    /// server looks the text up by its normalized name and stores the id of the matching entry, if exactly one
    /// matches. Ignored for other rules.
    /// </summary>
    public Guid? ReferenceId { get; init; }

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
