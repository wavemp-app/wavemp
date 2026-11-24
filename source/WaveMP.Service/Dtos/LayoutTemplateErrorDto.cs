using System.Text.Json.Serialization;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A problem of a layout template.
/// </summary>
public sealed class LayoutTemplateErrorDto
{
    public required LayoutTemplateErrorCodeDto Code { get; init; }

    /// <summary>
    /// Index of the character (UTF-16 code unit) of the trimmed template where the problem starts; 0 for problems of
    /// the whole template.
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// The text of the placeholder for <c>unknown_placeholder</c> and its name for <c>invalid_number_format</c>, at
    /// most 64 characters; missing for other codes.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Placeholder { get; init; }

    /// <summary>
    /// The longest allowed template for <c>max_length</c>; missing for other codes.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Max { get; init; }
}
