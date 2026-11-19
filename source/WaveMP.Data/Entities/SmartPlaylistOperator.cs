namespace WaveMP.Data.Entities;

/// <summary>
/// How a rule of a smart playlist compares its field. The negations (<see cref="IsNot"/>,
/// <see cref="DoesNotContain"/>, <see cref="NotInLast"/>, <see cref="IsFalse"/>) match exactly the tracks the positive
/// operator does not match, also tracks without a value (e.g. without genre or never played).
/// </summary>
public enum SmartPlaylistOperator
{
    /// <summary>
    /// Text: equal without regard to case. Number: equal.
    /// </summary>
    Is,

    /// <summary>
    /// Negation of <see cref="Is"/>.
    /// </summary>
    IsNot,

    /// <summary>
    /// Text: contains the text without regard to case.
    /// </summary>
    Contains,

    /// <summary>
    /// Negation of <see cref="Contains"/>.
    /// </summary>
    DoesNotContain,

    /// <summary>
    /// Number: at least the value.
    /// </summary>
    AtLeast,

    /// <summary>
    /// Number: at most the value.
    /// </summary>
    AtMost,

    /// <summary>
    /// Number: from the value to the second value, both included.
    /// </summary>
    Between,

    /// <summary>
    /// Date: within the last number of days.
    /// </summary>
    InLast,

    /// <summary>
    /// Negation of <see cref="InLast"/>.
    /// </summary>
    NotInLast,

    /// <summary>
    /// Yes or no: yes.
    /// </summary>
    IsTrue,

    /// <summary>
    /// Yes or no: no.
    /// </summary>
    IsFalse,
}
