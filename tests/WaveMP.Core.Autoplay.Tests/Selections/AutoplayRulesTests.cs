using AwesomeAssertions;
using WaveMP.Core.Autoplay.Selections;

namespace WaveMP.Core.Autoplay.Tests.Selections;

[TestFixture]
public class AutoplayRulesTests
{
    [TestCase(false, 0, 1.0)]
    [TestCase(false, 2, 1.0)]
    [TestCase(false, 3, 1.0)]
    [TestCase(false, 4, 1.25)]
    [TestCase(false, 5, 1.5)]
    [TestCase(true, 0, 1.5)]
    [TestCase(true, 5, 2.25)]
    [TestCase(false, 1, 0.0)]
    [TestCase(true, 1, 0.0)]
    public void WeightOf_FavoriteAndRating_WeighTheSong(bool isFavorite, int rating, double expected)
    {
        AutoplayRules.WeightOf(isFavorite, rating).Should().Be(expected);
    }

    [TestCase(1997, 1990)]
    [TestCase(2000, 2000)]
    [TestCase(2009, 2000)]
    public void DecadeOf_Year_ReturnsTheFirstYearOfItsDecade(int year, int expected)
    {
        AutoplayRules.DecadeOf(year).Should().Be(expected);
    }

    [Test]
    public void SimilarDecades_Years_ReturnsTheirDecadesAndThoseNextToThem()
    {
        AutoplayRules.SimilarDecades([1997, 1994, 2021]).Should().BeEquivalentTo([1980, 1990, 2000, 2010, 2020, 2030]);
    }
}
