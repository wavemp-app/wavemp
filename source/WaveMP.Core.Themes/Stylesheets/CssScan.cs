namespace WaveMP.Core.Themes.Stylesheets;

/// <summary>
/// What a style sheet loads and which rules it uses, from its tokens (see <see cref="CssTokenizer"/>): every address
/// (url tokens, <c>url("...")</c>, <c>src("...")</c> and the strings that <c>image()</c> and <c>image-set()</c> read
/// as addresses) with the range of text that stands for it, every at-rule, and the tokens that browsers would read
/// differently than they look (broken strings and addresses, addresses taken from variables).
/// </summary>
internal sealed class CssScan
{
    // Functions that read a string argument as an address: image-set("a.png" 1x) is image-set(url("a.png") 1x).
    private static readonly HashSet<string> _imageFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "image",
        "-webkit-image",
        "image-set",
        "-webkit-image-set",
    };

    // Functions that take one string as address: url("a.png") and src("a.png").
    private static readonly HashSet<string> _addressFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "url",
        "src",
    };

    // Functions that put a value from elsewhere into their place; inside an address they could hide one.
    private static readonly HashSet<string> _substitutionFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "var",
        "env",
        "attr",
        "if",
        "inherit",
    };

    private CssScan(string text)
    {
        Text = text;
    }

    /// <summary>
    /// The preprocessed text the ranges refer to.
    /// </summary>
    public string Text { get; }

    public List<CssReference> References { get; } = [];

    public List<CssAtRule> AtRules { get; } = [];

    /// <summary>
    /// Lines with a broken string or address: browsers drop them, but where one ends depends on details.
    /// </summary>
    public List<int> SyntaxErrors { get; } = [];

    /// <summary>
    /// Lines with an address whose value would come from a variable or an attribute, e.g. <c>src(var(--a))</c> or
    /// <c>image-set(var(--a) 1x)</c>, and with an address function that is not exactly one string.
    /// </summary>
    public List<int> IndirectAddresses { get; } = [];

    public static CssScan Run(string text)
    {
        string preprocessed = CssTokenizer.Preprocess(text);
        CssScan scan = new(preprocessed);
        scan.Walk(CssTokenizer.Tokenize(preprocessed));
        return scan;
    }

    private void Walk(IReadOnlyList<CssToken> tokens)
    {
        // The open functions and blocks; a function holds its name, a parenthesis or bracket block an empty name.
        Stack<string> open = new();
        int imageDepth = 0;
        for (int index = 0; index < tokens.Count; index++)
        {
            CssToken token = tokens[index];
            switch (token.Type)
            {
                case CssTokenType.BadString:
                case CssTokenType.BadUrl:
                    SyntaxErrors.Add(token.Line);
                    break;
                case CssTokenType.AtKeyword:
                    AtRules.Add(new CssAtRule(token.Value.ToLowerInvariant(), token.Line));
                    break;
                case CssTokenType.Url:
                    References.Add(new CssReference(token.Value, token.Start, token.End, token.Line, IsString: false));
                    break;
                case CssTokenType.String when open.TryPeek(out string? function) &&
                    _imageFunctions.Contains(function):
                    References.Add(new CssReference(token.Value, token.Start, token.End, token.Line, IsString: true));
                    break;
                case CssTokenType.Function when _addressFunctions.Contains(token.Value):
                    index = ReadAddressFunction(tokens, index);
                    break;
                case CssTokenType.Function:
                    if (imageDepth > 0 && _substitutionFunctions.Contains(token.Value))
                    {
                        IndirectAddresses.Add(token.Line);
                    }

                    if (_imageFunctions.Contains(token.Value))
                    {
                        imageDepth++;
                    }

                    open.Push(token.Value);
                    break;
                case CssTokenType.OpenParenthesis or CssTokenType.OpenSquare or CssTokenType.OpenCurly:
                    open.Push(string.Empty);
                    break;
                case CssTokenType.CloseParenthesis or CssTokenType.CloseSquare or CssTokenType.CloseCurly:
                    if (open.TryPop(out string? closed) && _imageFunctions.Contains(closed))
                    {
                        imageDepth--;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Reads <c>url("...")</c> or <c>src("...")</c> from the function token at <paramref name="start"/>: exactly one
    /// string, maybe with whitespace around it. Returns the index of the last token read.
    /// </summary>
    private int ReadAddressFunction(IReadOnlyList<CssToken> tokens, int start)
    {
        CssToken function = tokens[start];
        int index = SkipWhitespace(tokens, start + 1);
        if (index < tokens.Count && tokens[index].Type == CssTokenType.String)
        {
            CssToken value = tokens[index];
            int end = SkipWhitespace(tokens, index + 1);
            if (end < tokens.Count && tokens[end].Type == CssTokenType.CloseParenthesis)
            {
                References.Add(
                    new CssReference(value.Value, function.Start, tokens[end].End, function.Line, IsString: false));
                return end;
            }
        }

        // Anything else (no string, variables, modifiers, a missing parenthesis) is not accepted as address. The rest
        // of the function is skipped up to its closing parenthesis.
        IndirectAddresses.Add(function.Line);
        int depth = 1;
        for (index = start + 1; index < tokens.Count; index++)
        {
            switch (tokens[index].Type)
            {
                case CssTokenType.Function or CssTokenType.OpenParenthesis:
                    depth++;
                    break;
                case CssTokenType.CloseParenthesis:
                    depth--;
                    if (depth == 0)
                    {
                        return index;
                    }

                    break;
            }
        }

        return tokens.Count - 1;
    }

    private static int SkipWhitespace(IReadOnlyList<CssToken> tokens, int index)
    {
        while (index < tokens.Count && tokens[index].Type == CssTokenType.Whitespace)
        {
            index++;
        }

        return index;
    }
}

/// <summary>
/// An address in a style sheet.
/// </summary>
/// <param name="Value">The address with all escapes resolved.</param>
/// <param name="Start">Start of the text that stands for the address (the whole <c>url(...)</c>, or the
/// string).</param>
/// <param name="End">End of that text.</param>
/// <param name="Line">Line of the address.</param>
/// <param name="IsString">Whether the text is a string (in <c>image-set()</c>), not a <c>url(...)</c>.</param>
internal sealed record CssReference(string Value, int Start, int End, int Line, bool IsString);

/// <summary>
/// An at-rule of a style sheet, by its name in lower case without the <c>@</c>.
/// </summary>
internal sealed record CssAtRule(string Name, int Line);
