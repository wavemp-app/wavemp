using AwesomeAssertions;
using WaveMP.Core.Metadata.Artists;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Tests.Artists;

[TestFixture]
public class ArtistNamesTests
{
    [TestCase("Bob Sinclar feat. K.Flay", "Bob Sinclar", "K.Flay")]
    [TestCase("Bob Sinclar ft. K.Flay", "Bob Sinclar", "K.Flay")]
    [TestCase("Bob Sinclar featuring K.Flay", "Bob Sinclar", "K.Flay")]
    [TestCase("Bob Sinclar FEAT. K.Flay", "Bob Sinclar", "K.Flay")]
    [TestCase("Bob Sinclar (feat. K.Flay)", "Bob Sinclar", "K.Flay")]
    [TestCase("Bob Sinclar [ft. K.Flay]", "Bob Sinclar", "K.Flay")]
    public void Split_Featuring_GivesTheMainAndTheFeaturedArtist(string text, string main, string featured)
    {
        ArtistNames.Split(text).Should().Equal(
            new ArtistName(main),
            new ArtistName(featured, ArtistRole.Featured));
    }

    [Test]
    public void Split_Semicolons_GivesAllMainArtistsInTheirOrder()
    {
        ArtistNames.Split("K/DA; Beyoncé ;Guns N' Roses").Should().Equal(
            new ArtistName("K/DA"),
            new ArtistName("Beyoncé"),
            new ArtistName("Guns N' Roses"));
    }

    [Test]
    public void Split_SeveralMainAndFeaturedArtists_KeepsTheirRolesAndOrder()
    {
        ArtistNames.Split("A; B feat. C; D").Should().Equal(
            new ArtistName("A"),
            new ArtistName("B"),
            new ArtistName("C", ArtistRole.Featured),
            new ArtistName("D", ArtistRole.Featured));
    }

    [TestCase("Earth, Wind & Fire")]
    [TestCase("Simon & Garfunkel")]
    [TestCase("Simon and Garfunkel")]
    [TestCase("AC/DC")]
    [TestCase("K/DA")]
    [TestCase("K.Flay")]
    [TestCase("Lil Nas X")]
    [TestCase("Florence + the Machine")]
    [TestCase("Marshmello x Anne-Marie")]
    [TestCase("Guns N' Roses")]
    [TestCase("Featurecast")]
    [TestCase("Daft Punk Featuring")]
    public void Split_NamesWithoutUnambiguousSeparators_StayOneArtist(string name)
    {
        ArtistNames.Split(name).Should().Equal(new ArtistName(name));
    }

    [Test]
    public void Split_ArtistRepeatedInOtherSpelling_IsKeptOnceAsMainArtist()
    {
        ArtistNames.Split("K.Flay feat. KFlay; Other").Should().Equal(
            new ArtistName("K.Flay"),
            new ArtistName("Other", ArtistRole.Featured));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void Split_NoText_GivesNoArtists(string? text)
    {
        ArtistNames.Split(text).Should().BeEmpty();
    }

    [Test]
    public void FromValues_DisplayTextWithFeaturing_GivesTheFeaturedRoleAndTheIdsByPosition()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        IReadOnlyList<ArtistName> artists = ArtistNames.FromValues(
            ["Bob Sinclar", "K.Flay"],
            "Bob Sinclar feat. K.Flay",
            [first, second]);

        artists.Should().Equal(
            new ArtistName("Bob Sinclar", ArtistRole.Main, first),
            new ArtistName("K.Flay", ArtistRole.Featured, second));
    }

    [Test]
    public void Resolve_RealMultiValues_AreNeverSplitFurther()
    {
        ArtistNames.Resolve(["Simon & Garfunkel", "AC/DC"], "Simon & Garfunkel; AC/DC", []).Should().Equal(
            new ArtistName("Simon & Garfunkel"),
            new ArtistName("AC/DC"));
    }

    [Test]
    public void Resolve_SingleTextWithOneId_GivesTheIdToTheArtist()
    {
        var id = Guid.NewGuid();

        ArtistNames.Resolve([], "Beyoncé", [id]).Should().Equal(new ArtistName("Beyoncé", ArtistRole.Main, id));
    }

    [Test]
    public void DisplayText_MainAndFeaturedArtists_IsReadBackBySplit()
    {
        ArtistName[] artists =
        [
            new("Bob Sinclar"),
            new("Other Main"),
            new("K.Flay", ArtistRole.Featured),
        ];

        string? text = ArtistNames.DisplayText(artists);

        text.Should().Be("Bob Sinclar; Other Main feat. K.Flay");
        ArtistNames.Split(text).Should().Equal(artists);
    }

    [Test]
    public void DisplayText_NoArtists_IsNull()
    {
        ArtistNames.DisplayText([]).Should().BeNull();
    }

    [TestCase("Bob Sinclar & K.Flay", new[] { "Bob Sinclar", "K.Flay" })]
    [TestCase("Simon & Garfunkel", new[] { "Simon", "Garfunkel" })]
    [TestCase("Earth, Wind & Fire", new[] { "Earth", "Wind", "Fire" })]
    [TestCase("AC/DC", new[] { "AC", "DC" })]
    [TestCase("Marshmello x Anne-Marie", new[] { "Marshmello", "Anne-Marie" })]
    [TestCase("Florence + the Machine", new[] { "Florence", "the Machine" })]
    [TestCase("Hall and Oates", new[] { "Hall", "Oates" })]
    public void SuggestSplit_AmbiguousName_SuggestsItsParts(string name, string[] expected)
    {
        ArtistNames.SuggestSplit(name).Should().Equal(expected);
    }

    [TestCase("K.Flay")]
    [TestCase("Lil Nas X")]
    [TestCase("Beyoncé")]
    [TestCase("Guns N' Roses")]
    [TestCase("Axwell")]
    [TestCase(null)]
    public void SuggestSplit_NameWithoutAmbiguousSeparator_SuggestsNothing(string? name)
    {
        ArtistNames.SuggestSplit(name).Should().BeNull();
    }

    [Test]
    public void SuggestSplit_ArtistsOfASong_ReplacesOnlyTheAmbiguousNamesAndKeepsTheRoles()
    {
        IReadOnlyList<ArtistName>? suggestion = ArtistNames.SuggestSplit(
        [
            new ArtistName("K.Flay"),
            new ArtistName("A & B", ArtistRole.Featured),
        ]);

        suggestion.Should().Equal(
            new ArtistName("K.Flay"),
            new ArtistName("A", ArtistRole.Featured),
            new ArtistName("B", ArtistRole.Featured));
    }

    [Test]
    public void SuggestSplit_ArtistsWithoutAmbiguousNames_SuggestsNothing()
    {
        ArtistNames.SuggestSplit([new ArtistName("K.Flay"), new ArtistName("Lil Nas X")]).Should().BeNull();
    }
}
