namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// Why a layout template was rejected.
/// </summary>
public enum LayoutTemplateErrorCode
{
    /// <summary>
    /// The template is empty.
    /// </summary>
    Required,

    /// <summary>
    /// The template is longer than <see cref="LayoutTemplateSyntax.MaxLength"/> characters.
    /// </summary>
    TooLong,

    /// <summary>
    /// A placeholder names no known value (the name is part of the error).
    /// </summary>
    UnknownPlaceholder,

    /// <summary>
    /// A <c>{</c> without <c>}</c>, a <c>}</c> without <c>{</c>, or a <c>{</c> inside a placeholder.
    /// </summary>
    UnbalancedBrace,

    /// <summary>
    /// A <c>[</c> without <c>]</c> or a <c>]</c> without <c>[</c>.
    /// </summary>
    UnbalancedSection,

    /// <summary>
    /// A <c>[</c> inside an optional section.
    /// </summary>
    NestedSection,

    /// <summary>
    /// A format other than 1 to 4 zeros, or a format for a value that is not a number (the name is part of the
    /// error).
    /// </summary>
    InvalidNumberFormat,

    /// <summary>
    /// The file name has no <c>{Title}</c>, so the songs of a folder would not get names of their own.
    /// </summary>
    MissingTitle,

    /// <summary>
    /// The template starts with <c>/</c>.
    /// </summary>
    Rooted,

    /// <summary>
    /// A folder name is empty (<c>//</c>).
    /// </summary>
    EmptyFolder,

    /// <summary>
    /// A folder name is <c>.</c> or <c>..</c>.
    /// </summary>
    ParentFolder,

    /// <summary>
    /// An optional section contains <c>/</c>; sections stay within one name.
    /// </summary>
    FolderInSection,
}
