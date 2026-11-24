using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Why a layout template cannot be used. Clients translate it.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LayoutTemplateErrorCodeDto>))]
public enum LayoutTemplateErrorCodeDto
{
    /// <summary>
    /// The template is empty.
    /// </summary>
    [JsonStringEnumMemberName("required")]
    Required,

    /// <summary>
    /// The template is longer than <c>max</c> characters.
    /// </summary>
    [JsonStringEnumMemberName("max_length")]
    MaxLength,

    /// <summary>
    /// A placeholder names no known value; <c>placeholder</c> holds its text.
    /// </summary>
    [JsonStringEnumMemberName("unknown_placeholder")]
    UnknownPlaceholder,

    /// <summary>
    /// An opening brace without closing brace, a closing brace without opening brace, or an opening brace inside a
    /// placeholder.
    /// </summary>
    [JsonStringEnumMemberName("unbalanced_brace")]
    UnbalancedBrace,

    /// <summary>
    /// An opening square bracket without closing one or the other way round.
    /// </summary>
    [JsonStringEnumMemberName("unbalanced_section")]
    UnbalancedSection,

    /// <summary>
    /// An optional section inside an optional section.
    /// </summary>
    [JsonStringEnumMemberName("nested_section")]
    NestedSection,

    /// <summary>
    /// A format other than 1 to 4 zeros, or a format for a value that is not a number; <c>placeholder</c> holds the
    /// name.
    /// </summary>
    [JsonStringEnumMemberName("invalid_number_format")]
    InvalidNumberFormat,

    /// <summary>
    /// The file name (after the last <c>/</c>) has no <c>{Title}</c>.
    /// </summary>
    [JsonStringEnumMemberName("missing_title")]
    MissingTitle,

    /// <summary>
    /// The template starts with <c>/</c>.
    /// </summary>
    [JsonStringEnumMemberName("rooted")]
    Rooted,

    /// <summary>
    /// A folder name is empty (<c>//</c>).
    /// </summary>
    [JsonStringEnumMemberName("empty_folder")]
    EmptyFolder,

    /// <summary>
    /// A folder name is <c>.</c> or <c>..</c>.
    /// </summary>
    [JsonStringEnumMemberName("parent_folder")]
    ParentFolder,

    /// <summary>
    /// An optional section contains <c>/</c>.
    /// </summary>
    [JsonStringEnumMemberName("folder_in_section")]
    FolderInSection,
}
