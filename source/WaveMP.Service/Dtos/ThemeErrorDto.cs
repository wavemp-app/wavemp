using System.Text.Json.Serialization;

namespace WaveMP.Service.Dtos;

/// <summary>
/// One reason why a theme package is rejected.
/// </summary>
public sealed class ThemeErrorDto
{
    public required ThemeErrorCodeDto Code { get; init; }

    /// <summary>
    /// The file in the package the error concerns, e.g. <c>images/play.svg</c>; missing for the package as a whole.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    /// <summary>
    /// The line in that file, from 1, if known.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; init; }

    /// <summary>
    /// The member of <c>manifest.json</c> the error concerns, e.g. <c>themeApi</c> or <c>previews.dark</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; init; }

    /// <summary>
    /// A further file, e.g. the missing file an address of the CSS file names.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? File { get; init; }

    /// <summary>
    /// Lower limit, e.g. the oldest supported version of the theme API for <c>manifest.unsupported_api_version</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Min { get; init; }

    /// <summary>
    /// Upper limit, e.g. the newest supported version of the theme API, the most bytes for
    /// <c>package.too_large</c> and <c>file.too_large</c> or the most entries for <c>package.too_many_entries</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Max { get; init; }
}
