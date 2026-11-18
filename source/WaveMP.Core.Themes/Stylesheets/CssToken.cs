namespace WaveMP.Core.Themes.Stylesheets;

/// <summary>
/// A token of a style sheet.
/// </summary>
/// <param name="Type">Kind of the token.</param>
/// <param name="Value">The value with all escapes resolved: the name of an ident, function (without the
/// parenthesis), at-keyword (without the <c>@</c>) or hash (without the <c>#</c>), the content of a string or the
/// address of a url token; empty for other tokens.</param>
/// <param name="Start">Index of the first character of the token in the preprocessed text.</param>
/// <param name="End">Index after the last character of the token in the preprocessed text.</param>
/// <param name="Line">Line of the start of the token, from 1.</param>
internal readonly record struct CssToken(CssTokenType Type, string Value, int Start, int End, int Line);
