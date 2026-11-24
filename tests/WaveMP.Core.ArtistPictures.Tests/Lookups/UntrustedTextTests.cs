using AwesomeAssertions;
using WaveMP.Core.ArtistPictures.Lookups;

namespace WaveMP.Core.ArtistPictures.Tests.Lookups;

[TestFixture]
public class UntrustedTextTests
{
    [TestCase("Jane Doe", "Jane Doe")]
    [TestCase("<a href=\"//commons.wikimedia.org/wiki/User:Jane\" title=\"User:Jane\">Jane&nbsp;Doe</a>", "Jane Doe")]
    [TestCase("<span class=\"fn\"><b>Jane</b>\n\t Doe</span> (<i>talk</i>)", "Jane Doe ( talk )")]
    [TestCase("Jane<script>alert('x')</script> Doe", "Jane Doe")]
    [TestCase("Jane <style>body { color: red }</style>Doe", "Jane Doe")]
    [TestCase("Jane <!-- hidden --> Doe", "Jane Doe")]
    [TestCase("&lt;script&gt;alert(1)&lt;/script&gt;Jane", "Jane")]
    [TestCase("&lt;b&gt;Jane&lt;/b&gt;", "Jane")]
    [TestCase("Tom &amp; Jerry &#8211; caf&eacute;", "Tom & Jerry \u2013 caf\u00e9")]
    [TestCase("Jane <img src=x onerror=alert(1)", "Jane")]
    [TestCase("Jane&#8203;&#7; Doe", "Jane Doe")]
    public void PlainText_Html_IsPlainTextWithoutAnyMarkup(string html, string expected)
    {
        UntrustedText.PlainText(html, 500).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("<br/><script>x</script>")]
    public void PlainText_NothingLeft_IsNull(string? html)
    {
        UntrustedText.PlainText(html, 500).Should().BeNull();
    }

    [Test]
    public void PlainText_LongText_IsCutWithoutSplittingACharacter()
    {
        string text = new string('a', 9) + "\U0001F600" + "bbb";

        UntrustedText.PlainText(text, 10).Should().Be(new string('a', 9));
        UntrustedText.PlainText(text, 11).Should().Be(new string('a', 9) + "\U0001F600");
    }

    [Test]
    public void PlainText_HugeInput_IsReadOnlyUpToItsLimit()
    {
        string html = "<b>Jane</b>" + string.Concat(Enumerable.Repeat("<script>", 50_000));

        UntrustedText.PlainText(html, 500).Should().Be("Jane");
    }

    [TestCase("https://creativecommons.org/licenses/by-sa/4.0", "https://creativecommons.org/licenses/by-sa/4.0")]
    [TestCase(" https://commons.wikimedia.org/wiki/File:A b.jpg ", "https://commons.wikimedia.org/wiki/File:A%20b.jpg")]
    public void HttpsUrl_HttpsAddress_IsKept(string value, string expected)
    {
        UntrustedText.HttpsUrl(value).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("javascript:alert(1)")]
    [TestCase("JaVaScRiPt:alert(1)")]
    [TestCase("data:text/html,<script>alert(1)</script>")]
    [TestCase("http://creativecommons.org/licenses/by/2.0")]
    [TestCase("//commons.wikimedia.org/wiki/User:Jane")]
    [TestCase("/wiki/File:A.jpg")]
    [TestCase("https://user:secret@commons.wikimedia.org/")]
    [TestCase("vbscript:msgbox")]
    public void HttpsUrl_AnythingElse_IsDropped(string? value)
    {
        UntrustedText.HttpsUrl(value).Should().BeNull();
    }

    [Test]
    public void HttpsUrl_TooLong_IsDropped()
    {
        UntrustedText.HttpsUrl("https://example.org/" + new string('a', 3000)).Should().BeNull();
    }
}
