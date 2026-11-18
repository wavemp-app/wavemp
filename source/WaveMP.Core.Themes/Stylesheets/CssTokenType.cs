namespace WaveMP.Core.Themes.Stylesheets;

/// <summary>
/// The kinds of tokens of CSS Syntax Module Level 3 (section 4); comments are no tokens.
/// </summary>
internal enum CssTokenType
{
    Ident,
    Function,
    AtKeyword,
    Hash,
    String,
    BadString,
    Url,
    BadUrl,
    Delim,
    Number,
    Percentage,
    Dimension,
    Whitespace,
    Cdo,
    Cdc,
    Colon,
    Semicolon,
    Comma,
    OpenSquare,
    CloseSquare,
    OpenParenthesis,
    CloseParenthesis,
    OpenCurly,
    CloseCurly,
}
