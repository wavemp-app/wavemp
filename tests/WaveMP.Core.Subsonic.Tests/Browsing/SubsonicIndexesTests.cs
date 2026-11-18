using AwesomeAssertions;
using WaveMP.Core.Subsonic.Browsing;

namespace WaveMP.Core.Subsonic.Tests.Browsing;

[TestFixture]
public class SubsonicIndexesTests
{
    [TestCase("The Beatles", "Beatles")]
    [TestCase("the beatles", "beatles")]
    [TestCase("Los Lobos", "Lobos")]
    [TestCase("Theory of a Deadman", "Theory of a Deadman")]
    [TestCase("The", "The")]
    [TestCase("  Le Tigre ", "Tigre")]
    public void SortName_LeavesOutALeadingArticle(string name, string expected)
    {
        SubsonicIndexes.SortName(name).Should().Be(expected);
    }

    [TestCase("Abba", "A")]
    [TestCase("abba", "A")]
    [TestCase("The Beatles", "B")]
    [TestCase("Édith Piaf", "E")]
    [TestCase("Ölbaum", "O")]
    [TestCase("2Pac", "#")]
    [TestCase("!!!", "#")]
    [TestCase("", "#")]
    public void IndexOf_IsTheFirstLetterWithoutAccents(string name, string expected)
    {
        SubsonicIndexes.IndexOf(name).Should().Be(expected);
    }

    [Test]
    public void Group_SortsIndexesAndItemsWithOtherLast()
    {
        string[] names = ["Zebra", "2Pac", "The Beatles", "abba", "Bach", "Édith"];

        IReadOnlyList<(string Index, IReadOnlyList<string> Items)> groups = SubsonicIndexes.Group(names, name => name);

        groups.Select(group => group.Index).Should().Equal("A", "B", "E", "Z", "#");
        groups[1].Items.Should().Equal("Bach", "The Beatles");
        groups[4].Items.Should().Equal("2Pac");
    }
}
