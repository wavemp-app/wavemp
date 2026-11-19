using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// How a rule of a smart playlist compares its field. Text fields take Is, IsNot, Contains and DoesNotContain with
/// <c>text</c>; number fields Is, IsNot, AtLeast, AtMost with <c>number</c> and Between with <c>number</c> and
/// <c>numberTo</c>; day fields InLast and NotInLast with the number of days in <c>number</c>; yes/no fields IsTrue and
/// IsFalse without a value. Negations match every song the positive operator does not match, also songs without a
/// value.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<SmartPlaylistOperatorDto>))]
public enum SmartPlaylistOperatorDto
{
    /// <summary>
    /// Equal; texts without regard to case.
    /// </summary>
    Is,

    IsNot,

    /// <summary>
    /// The text is contained, without regard to case.
    /// </summary>
    Contains,

    DoesNotContain,

    AtLeast,

    AtMost,

    /// <summary>
    /// From <c>number</c> to <c>numberTo</c>, both included.
    /// </summary>
    Between,

    /// <summary>
    /// Within the last <c>number</c> days.
    /// </summary>
    InLast,

    NotInLast,

    IsTrue,

    IsFalse,
}
