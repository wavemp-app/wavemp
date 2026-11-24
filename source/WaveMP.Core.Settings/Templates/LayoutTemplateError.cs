namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// A problem of a layout template.
/// </summary>
/// <param name="Code">Why the template was rejected.</param>
/// <param name="Position">Index of the character (UTF-16 code unit) of the normalized template the problem starts
/// at; 0 for problems of the whole template.</param>
/// <param name="Placeholder">The name in the placeholder for <see cref="LayoutTemplateErrorCode.UnknownPlaceholder"/>
/// and <see cref="LayoutTemplateErrorCode.InvalidNumberFormat"/>, at most
/// <see cref="LayoutTemplateSyntax.MaxPlaceholderLength"/> characters, otherwise <c>null</c>.</param>
public sealed record LayoutTemplateError(LayoutTemplateErrorCode Code, int Position = 0, string? Placeholder = null);
