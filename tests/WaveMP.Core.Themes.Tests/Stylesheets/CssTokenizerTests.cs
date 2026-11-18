using AwesomeAssertions;
using WaveMP.Core.Themes.Stylesheets;

namespace WaveMP.Core.Themes.Tests.Stylesheets;

[TestFixture]
public class CssTokenizerTests
{
    private static IReadOnlyList<CssToken> Tokens(string css)
    {
        return [.. CssTokenizer.Tokenize(CssTokenizer.Preprocess(css)).Where(token => token.Type != CssTokenType.Whitespace)];
    }

    [TestCase("url(a.svg)", "a.svg")]
    [TestCase("URL(a.svg)", "a.svg")]
    [TestCase("u\\72l(a.svg)", "a.svg")]
    [TestCase("\\75 rl(a.svg)", "a.svg")]
    [TestCase("url(  a.svg  )", "a.svg")]
    [TestCase("url(a\\29.svg)", "a).svg")]
    [TestCase("url(http\\3a//x)", "http://x")]
    public void Tokenize_UnquotedUrl_IsOneUrlTokenWithTheResolvedValue(string css, string expected)
    {
        CssToken token = Tokens(css).Should().ContainSingle().Subject;

        token.Type.Should().Be(CssTokenType.Url);
        token.Value.Should().Be(expected);
    }

    [TestCase("url('a.svg')")]
    [TestCase("url( \"a.svg\")")]
    [TestCase("u\\rl(\"a.svg\")")]
    public void Tokenize_QuotedUrl_IsAFunctionWithAString(string css)
    {
        IReadOnlyList<CssToken> tokens = Tokens(css);

        tokens[0].Type.Should().Be(CssTokenType.Function);
        tokens[0].Value.Should().BeEquivalentTo("url");
        tokens[1].Type.Should().Be(CssTokenType.String);
        tokens[1].Value.Should().Be("a.svg");
        tokens[2].Type.Should().Be(CssTokenType.CloseParenthesis);
    }

    [TestCase("@import", "import")]
    [TestCase("@\\69mport", "import")]
    [TestCase("@IMPORT", "IMPORT")]
    [TestCase("@\\000069 mport", "import")]
    public void Tokenize_AtKeyword_ResolvesEscapes(string css, string expected)
    {
        CssToken token = Tokens(css).Should().ContainSingle().Subject;

        token.Type.Should().Be(CssTokenType.AtKeyword);
        token.Value.Should().Be(expected);
    }

    [Test]
    public void Tokenize_Comments_AreNoTokens()
    {
        IReadOnlyList<CssToken> tokens = Tokens("/* url(http://x) */ a /* @import */");

        tokens.Should().ContainSingle().Which.Value.Should().Be("a");
    }

    [Test]
    public void Tokenize_CommentInsideAName_SplitsTheName()
    {
        IReadOnlyList<CssToken> tokens = Tokens("ur/**/l(x)");

        tokens.Select(token => token.Type).Should().Equal(
            CssTokenType.Ident,
            CssTokenType.Function,
            CssTokenType.Ident,
            CssTokenType.CloseParenthesis);
    }

    [TestCase("url(a b)")]
    [TestCase("url(a\"b)")]
    [TestCase("url(a(b)")]
    [TestCase("url(a\u0001b)")]
    public void Tokenize_BrokenUrl_IsABadUrl(string css)
    {
        Tokens(css).Should().ContainSingle().Which.Type.Should().Be(CssTokenType.BadUrl);
    }

    [Test]
    public void Tokenize_StringWithLineBreak_IsABadString()
    {
        Tokens("'a\nb'")[0].Type.Should().Be(CssTokenType.BadString);
    }

    [Test]
    public void Tokenize_Tokens_KnowTheirLineAndRange()
    {
        const string Css = "a {\r\n  b: url(c.svg);\n}";
        string text = CssTokenizer.Preprocess(Css);

        CssToken url = CssTokenizer.Tokenize(text).Single(token => token.Type == CssTokenType.Url);

        url.Line.Should().Be(2);
        text[url.Start..url.End].Should().Be("url(c.svg)");
    }

    [TestCase("12px", nameof(CssTokenType.Dimension))]
    [TestCase("-1.5e3", nameof(CssTokenType.Number))]
    [TestCase("50%", nameof(CssTokenType.Percentage))]
    [TestCase("#fff", nameof(CssTokenType.Hash))]
    [TestCase("-->", nameof(CssTokenType.Cdc))]
    [TestCase("<!--", nameof(CssTokenType.Cdo))]
    [TestCase("--wmp-x", nameof(CssTokenType.Ident))]
    public void Tokenize_OtherTokens_AreRecognized(string css, string expected)
    {
        Tokens(css).Should().ContainSingle().Which.Type.Should().Be(Enum.Parse<CssTokenType>(expected));
    }
}
