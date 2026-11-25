using System.Globalization;
using System.Text;

namespace WaveMP.Core.Themes.Stylesheets;

/// <summary>
/// Splits a style sheet into tokens exactly like browsers do, following "CSS Syntax Module Level 3" (section 4,
/// tokenization), including escapes, comments and the special handling of <c>url(</c>. The checks of theme style sheets
/// rely on seeing the same tokens as the browser, so that no escaped or otherwise obfuscated address slips through:
/// <c>u\72l(</c>, <c>\75 rl(</c> and <c>URL(</c> are all the function or url token <c>url</c>.
/// <para>
/// No parser library is used on purpose: the established CSS parsers for .NET build an object model and drop
/// declarations they do not know (e.g. newer properties), so a check on top of them would not see every address.
/// </para>
/// </summary>
internal static class CssTokenizer
{
    private const char ReplacementCharacter = '�';
    private const int MaxCodePoint = 0x10FFFF;

    /// <summary>
    /// Preprocesses the text like browsers (CR LF, CR and form feeds become LF, NUL becomes U+FFFD) and returns it,
    /// so the indexes of the tokens refer to it.
    /// </summary>
    public static string Preprocess(string text)
    {
        StringBuilder result = new(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            switch (current)
            {
                case '\r':
                    result.Append('\n');
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    break;
                case '\f':
                    result.Append('\n');
                    break;
                case '\0':
                    result.Append(ReplacementCharacter);
                    break;
                default:
                    if (char.IsHighSurrogate(current)
                        && index + 1 < text.Length
                        && char.IsLowSurrogate(text[index + 1]))
                    {
                        result.Append(current).Append(text[index + 1]);
                        index++;
                    }
                    else if (char.IsSurrogate(current))
                    {
                        result.Append(ReplacementCharacter);
                    }
                    else
                    {
                        result.Append(current);
                    }

                    break;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// The tokens of a preprocessed text (see <see cref="Preprocess"/>), without comments.
    /// </summary>
    public static IReadOnlyList<CssToken> Tokenize(string preprocessed)
    {
        Reader reader = new(preprocessed);
        List<CssToken> tokens = [];
        while (reader.NextToken() is { } token)
        {
            tokens.Add(token);
        }

        return tokens;
    }

    private sealed class Reader(string text)
    {
        private int _position;
        private int _line = 1;
        private int _lineCountedTo;

        public CssToken? NextToken()
        {
            ConsumeComments();
            if (_position >= text.Length)
            {
                return null;
            }

            int start = _position;
            int line = LineAt(start);
            char current = text[_position++];
            switch (current)
            {
                case '\n' or '\t' or ' ':
                    while (IsWhitespace(Peek(0)))
                    {
                        _position++;
                    }

                    return Token(CssTokenType.Whitespace, string.Empty, start, line);
                case '"' or '\'':
                    return ConsumeString(current, start, line);
                case '#':
                    if (IsIdentCodePoint(Peek(0)) || IsValidEscape(Peek(0), Peek(1)))
                    {
                        return Token(CssTokenType.Hash, ConsumeIdentSequence(), start, line);
                    }

                    return Token(CssTokenType.Delim, "#", start, line);
                case '(':
                    return Token(CssTokenType.OpenParenthesis, string.Empty, start, line);
                case ')':
                    return Token(CssTokenType.CloseParenthesis, string.Empty, start, line);
                case '+':
                    if (StartsNumber(current, Peek(0), Peek(1)))
                    {
                        _position--;
                        return ConsumeNumeric(start, line);
                    }

                    return Token(CssTokenType.Delim, "+", start, line);
                case ',':
                    return Token(CssTokenType.Comma, string.Empty, start, line);
                case '-':
                    if (StartsNumber(current, Peek(0), Peek(1)))
                    {
                        _position--;
                        return ConsumeNumeric(start, line);
                    }

                    if (Peek(0) == '-' && Peek(1) == '>')
                    {
                        _position += 2;
                        return Token(CssTokenType.Cdc, string.Empty, start, line);
                    }

                    if (StartsIdentSequence(current, Peek(0), Peek(1)))
                    {
                        _position--;
                        return ConsumeIdentLike(start, line);
                    }

                    return Token(CssTokenType.Delim, "-", start, line);
                case '.':
                    if (StartsNumber(current, Peek(0), Peek(1)))
                    {
                        _position--;
                        return ConsumeNumeric(start, line);
                    }

                    return Token(CssTokenType.Delim, ".", start, line);
                case ':':
                    return Token(CssTokenType.Colon, string.Empty, start, line);
                case ';':
                    return Token(CssTokenType.Semicolon, string.Empty, start, line);
                case '<':
                    if (Peek(0) == '!' && Peek(1) == '-' && Peek(2) == '-')
                    {
                        _position += 3;
                        return Token(CssTokenType.Cdo, string.Empty, start, line);
                    }

                    return Token(CssTokenType.Delim, "<", start, line);
                case '@':
                    if (StartsIdentSequence(Peek(0), Peek(1), Peek(2)))
                    {
                        return Token(CssTokenType.AtKeyword, ConsumeIdentSequence(), start, line);
                    }

                    return Token(CssTokenType.Delim, "@", start, line);
                case '[':
                    return Token(CssTokenType.OpenSquare, string.Empty, start, line);
                case '\\':
                    if (IsValidEscape(current, Peek(0)))
                    {
                        _position--;
                        return ConsumeIdentLike(start, line);
                    }

                    return Token(CssTokenType.Delim, "\\", start, line);
                case ']':
                    return Token(CssTokenType.CloseSquare, string.Empty, start, line);
                case '{':
                    return Token(CssTokenType.OpenCurly, string.Empty, start, line);
                case '}':
                    return Token(CssTokenType.CloseCurly, string.Empty, start, line);
                default:
                    if (char.IsAsciiDigit(current))
                    {
                        _position--;
                        return ConsumeNumeric(start, line);
                    }

                    if (IsIdentStart(current))
                    {
                        _position--;
                        return ConsumeIdentLike(start, line);
                    }

                    return Token(CssTokenType.Delim, current.ToString(), start, line);
            }
        }

        private CssToken Token(CssTokenType type, string value, int start, int line)
        {
            return new CssToken(type, value, start, _position, line);
        }

        private int LineAt(int index)
        {
            for (; _lineCountedTo < index; _lineCountedTo++)
            {
                if (text[_lineCountedTo] == '\n')
                {
                    _line++;
                }
            }

            return _line;
        }

        private char Peek(int offset)
        {
            int index = _position + offset;
            return index < text.Length ? text[index] : '\0';
        }

        private bool IsEnd(int offset)
        {
            return _position + offset >= text.Length;
        }

        private void ConsumeComments()
        {
            while (Peek(0) == '/' && Peek(1) == '*')
            {
                int end = text.IndexOf("*/", _position + 2, StringComparison.Ordinal);
                _position = end < 0 ? text.Length : end + 2;
            }
        }

        private CssToken ConsumeString(char ending, int start, int line)
        {
            StringBuilder value = new();
            while (true)
            {
                if (IsEnd(0))
                {
                    // Parse error, but still a string.
                    return Token(CssTokenType.String, value.ToString(), start, line);
                }

                char current = text[_position++];
                if (current == ending)
                {
                    return Token(CssTokenType.String, value.ToString(), start, line);
                }

                if (current == '\n')
                {
                    _position--;
                    return Token(CssTokenType.BadString, value.ToString(), start, line);
                }

                if (current == '\\')
                {
                    if (IsEnd(0))
                    {
                        continue;
                    }

                    if (Peek(0) == '\n')
                    {
                        _position++;
                        continue;
                    }

                    value.Append(ConsumeEscapedCodePoint());
                    continue;
                }

                value.Append(current);
            }
        }

        private CssToken ConsumeNumeric(int start, int line)
        {
            ConsumeNumber();
            if (StartsIdentSequence(Peek(0), Peek(1), Peek(2)))
            {
                return Token(CssTokenType.Dimension, ConsumeIdentSequence(), start, line);
            }

            if (Peek(0) == '%' && !IsEnd(0))
            {
                _position++;
                return Token(CssTokenType.Percentage, string.Empty, start, line);
            }

            return Token(CssTokenType.Number, string.Empty, start, line);
        }

        private void ConsumeNumber()
        {
            if (Peek(0) is '+' or '-' && !IsEnd(0))
            {
                _position++;
            }

            ConsumeDigits();
            if (Peek(0) == '.' && char.IsAsciiDigit(Peek(1)) && !IsEnd(1))
            {
                _position++;
                ConsumeDigits();
            }

            if (Peek(0) is 'e' or 'E' && !IsEnd(0))
            {
                if (char.IsAsciiDigit(Peek(1)) && !IsEnd(1))
                {
                    _position++;
                    ConsumeDigits();
                }
                else if (Peek(1) is '+' or '-' && char.IsAsciiDigit(Peek(2)) && !IsEnd(2))
                {
                    _position += 2;
                    ConsumeDigits();
                }
            }
        }

        private void ConsumeDigits()
        {
            while (!IsEnd(0) && char.IsAsciiDigit(Peek(0)))
            {
                _position++;
            }
        }

        private CssToken ConsumeIdentLike(int start, int line)
        {
            string name = ConsumeIdentSequence();
            if (name.Equals("url", StringComparison.OrdinalIgnoreCase) && Peek(0) == '(' && !IsEnd(0))
            {
                _position++;
                while (IsWhitespace(Peek(0)) && IsWhitespace(Peek(1)) && !IsEnd(1))
                {
                    _position++;
                }

                if (Peek(0) is '"' or '\'' || (IsWhitespace(Peek(0)) && Peek(1) is '"' or '\'' && !IsEnd(1)))
                {
                    return Token(CssTokenType.Function, name, start, line);
                }

                return ConsumeUrl(start, line);
            }

            if (Peek(0) == '(' && !IsEnd(0))
            {
                _position++;
                return Token(CssTokenType.Function, name, start, line);
            }

            return Token(CssTokenType.Ident, name, start, line);
        }

        private CssToken ConsumeUrl(int start, int line)
        {
            StringBuilder value = new();
            while (IsWhitespace(Peek(0)) && !IsEnd(0))
            {
                _position++;
            }

            while (true)
            {
                if (IsEnd(0))
                {
                    // Parse error, but still a url token.
                    return Token(CssTokenType.Url, value.ToString(), start, line);
                }

                char current = text[_position++];
                if (current == ')')
                {
                    return Token(CssTokenType.Url, value.ToString(), start, line);
                }

                if (IsWhitespace(current))
                {
                    while (IsWhitespace(Peek(0)) && !IsEnd(0))
                    {
                        _position++;
                    }

                    if (IsEnd(0))
                    {
                        return Token(CssTokenType.Url, value.ToString(), start, line);
                    }

                    if (Peek(0) == ')')
                    {
                        _position++;
                        return Token(CssTokenType.Url, value.ToString(), start, line);
                    }

                    ConsumeBadUrlRemnants();
                    return Token(CssTokenType.BadUrl, string.Empty, start, line);
                }

                if (current is '"' or '\'' or '(' || IsNonPrintable(current))
                {
                    ConsumeBadUrlRemnants();
                    return Token(CssTokenType.BadUrl, string.Empty, start, line);
                }

                if (current == '\\')
                {
                    if (IsValidEscape(current, Peek(0)) && !IsEnd(0))
                    {
                        value.Append(ConsumeEscapedCodePoint());
                        continue;
                    }

                    ConsumeBadUrlRemnants();
                    return Token(CssTokenType.BadUrl, string.Empty, start, line);
                }

                value.Append(current);
            }
        }

        private void ConsumeBadUrlRemnants()
        {
            while (!IsEnd(0))
            {
                char current = text[_position++];
                if (current == ')')
                {
                    return;
                }

                if (current == '\\' && IsValidEscape(current, Peek(0)) && !IsEnd(0))
                {
                    ConsumeEscapedCodePoint();
                }
            }
        }

        private string ConsumeIdentSequence()
        {
            StringBuilder result = new();
            while (!IsEnd(0))
            {
                char current = Peek(0);
                if (IsIdentCodePoint(current))
                {
                    result.Append(current);
                    _position++;
                }
                else if (IsValidEscape(current, Peek(1)) && !IsEnd(1))
                {
                    _position++;
                    result.Append(ConsumeEscapedCodePoint());
                }
                else
                {
                    break;
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Consumes the code point after a backslash (which is already consumed).
        /// </summary>
        private string ConsumeEscapedCodePoint()
        {
            if (IsEnd(0))
            {
                return ReplacementCharacter.ToString();
            }

            char current = text[_position++];
            if (!char.IsAsciiHexDigit(current))
            {
                return current.ToString();
            }

            int length = 1;
            while (length < 6 && !IsEnd(0) && char.IsAsciiHexDigit(Peek(0)))
            {
                _position++;
                length++;
            }

            int value = int.Parse(
                text.AsSpan(_position - length, length),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            if (IsWhitespace(Peek(0)) && !IsEnd(0))
            {
                _position++;
            }

            bool isSurrogate = value is >= 0xD800 and <= 0xDFFF;
            return value == 0 || isSurrogate || value > MaxCodePoint
                ? ReplacementCharacter.ToString()
                : char.ConvertFromUtf32(value);
        }

        private static bool StartsIdentSequence(char first, char second, char third)
        {
            if (first == '-')
            {
                return IsIdentStart(second) || second == '-' || IsValidEscape(second, third);
            }

            if (IsIdentStart(first))
            {
                return true;
            }

            return first == '\\' && IsValidEscape(first, second);
        }

        private static bool StartsNumber(char first, char second, char third)
        {
            if (first is '+' or '-')
            {
                return char.IsAsciiDigit(second) || (second == '.' && char.IsAsciiDigit(third));
            }

            return first == '.' ? char.IsAsciiDigit(second) : char.IsAsciiDigit(first);
        }

        private static bool IsValidEscape(char first, char second)
        {
            // A backslash at the end of the text is no valid escape either; Peek returns '\0' there, which the
            // preprocessing never leaves in the text.
            return first == '\\' && second != '\n' && second != '\0';
        }

        private static bool IsWhitespace(char value)
        {
            return value is '\n' or '\t' or ' ';
        }

        private static bool IsIdentStart(char value)
        {
            return char.IsAsciiLetter(value) || value == '_' || value >= '\u0080';
        }

        private static bool IsIdentCodePoint(char value)
        {
            return IsIdentStart(value) || char.IsAsciiDigit(value) || value == '-';
        }

        private static bool IsNonPrintable(char value)
        {
            return value is <= '\u0008' or '\u000B' or (>= '\u000E' and <= '\u001F') or '\u007F';
        }
    }
}
