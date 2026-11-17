using System.Text;
using AwesomeAssertions;
using WaveMP.Core.Lyrics.Files;

namespace WaveMP.Core.Lyrics.Tests.Files;

[TestFixture]
public class LyricsTextTests
{
    private const string Text = "[00:01.00]Grüße, café";

    [Test]
    public void Decode_Utf8WithAndWithoutByteOrderMark_ReturnsTheText()
    {
        LyricsText.Decode(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(Text)).ToArray())
            .Should().Be(Text);
        LyricsText.Decode(Encoding.UTF8.GetBytes(Text)).Should().Be(Text);
    }

    [Test]
    public void Decode_Utf16WithByteOrderMark_ReturnsTheText()
    {
        LyricsText.Decode(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Text)).ToArray())
            .Should().Be(Text);
        LyricsText.Decode(
                Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(Text)).ToArray())
            .Should().Be(Text);
    }

    [Test]
    public void Decode_Latin1_ReturnsTheText()
    {
        LyricsText.Decode(Encoding.Latin1.GetBytes(Text)).Should().Be(Text);
    }
}
