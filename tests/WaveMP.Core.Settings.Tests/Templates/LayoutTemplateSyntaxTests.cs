using AwesomeAssertions;
using WaveMP.Core.Settings.Templates;

namespace WaveMP.Core.Settings.Tests.Templates;

[TestFixture]
public class LayoutTemplateSyntaxTests
{
    [Test]
    public void Parse_TheDefaultTemplate_IsFolderBasedWithPaddedTrackNumber()
    {
        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse("{AlbumArtist}/{Album}/{TrackNumber:00} {Title}");

        result.IsValid.Should().BeTrue();
        result.Template!.IsFolderBased.Should().BeTrue();
        result.Template.Segments.Should().HaveCount(3);
        result.Template.Segments[0].Should().Equal(new LayoutTemplatePlaceholder(TemplateField.AlbumArtist));
        result.Template.FileName.Should().Equal(
            new LayoutTemplatePlaceholder(TemplateField.TrackNumber, 2),
            new LayoutTemplateText(" "),
            new LayoutTemplatePlaceholder(TemplateField.Title));
    }

    [Test]
    public void Parse_AFileBasedTemplate_IsNotFolderBased()
    {
        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse("{Artist} - {Title}");

        result.IsValid.Should().BeTrue();
        result.Template!.IsFolderBased.Should().BeFalse();
        LayoutTemplateSyntax.IsFolderBased("{Artist} - {Title}").Should().BeFalse();
        LayoutTemplateSyntax.IsFolderBased("{Artist}/{Title}").Should().BeTrue();
        LayoutTemplateSyntax.IsFolderBased("{Nope}").Should().BeNull();
    }

    [Test]
    public void Parse_PlaceholderNames_IgnoreCase()
    {
        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse("{albumartist}/{YEAR:0000} {title}");

        result.IsValid.Should().BeTrue();
        result.Template!.Segments[0].Should().Equal(new LayoutTemplatePlaceholder(TemplateField.AlbumArtist));
        result.Template.FileName[0].Should().Be(new LayoutTemplatePlaceholder(TemplateField.Year, 4));
    }

    [Test]
    public void Parse_AnOptionalSection_HoldsItsParts()
    {
        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse("[{DiscNumber}-]{TrackNumber:00} {Title}");

        result.IsValid.Should().BeTrue();
        LayoutTemplateSection section = result.Template!.FileName[0].Should().BeOfType<LayoutTemplateSection>().Which;
        section.Parts.Should().Equal(
            new LayoutTemplatePlaceholder(TemplateField.DiscNumber),
            new LayoutTemplateText("-"));
    }

    [Test]
    public void Parse_DoubledCharacters_StandForThemselves()
    {
        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse("{Album} [[{Year}]]/{{{Title}}}");

        result.IsValid.Should().BeTrue();
        result.Template!.Segments[0].Should().Equal(
            new LayoutTemplatePlaceholder(TemplateField.Album),
            new LayoutTemplateText(" ["),
            new LayoutTemplatePlaceholder(TemplateField.Year),
            new LayoutTemplateText("]"));
        result.Template.FileName.Should().Equal(
            new LayoutTemplateText("{"),
            new LayoutTemplatePlaceholder(TemplateField.Title),
            new LayoutTemplateText("}"));
    }

    [Test]
    public void Parse_SurroundingWhiteSpaceAndDecomposedText_IsNormalized()
    {
        string decomposed = "Müsik/{Title}";

        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse("  " + decomposed + " ");

        result.IsValid.Should().BeTrue();
        result.Template!.Segments[0].Should().Equal(new LayoutTemplateText("Müsik"));
        LayoutTemplateSyntax.Normalize("  " + decomposed + " ").Should().Be("Müsik/{Title}");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Parse_Empty_IsRequired(string? template)
    {
        ExpectError(template, LayoutTemplateErrorCode.Required, 0);
    }

    [Test]
    public void Parse_TooLong_IsRejected()
    {
        ExpectError("{Title}" + new string('a', LayoutTemplateSyntax.MaxLength), LayoutTemplateErrorCode.TooLong, 0);
    }

    [Test]
    public void Parse_AnUnknownPlaceholder_NamesIt()
    {
        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse("{Composer}/{Title} {}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Equal(
            new LayoutTemplateError(LayoutTemplateErrorCode.UnknownPlaceholder, 0, "Composer"),
            new LayoutTemplateError(LayoutTemplateErrorCode.UnknownPlaceholder, 19, string.Empty));
    }

    [Test]
    public void Parse_AnUnknownPlaceholder_IsCutToItsLimit()
    {
        string name = new('x', 100);

        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse("{" + name + "} {Title}");

        result.Errors.Single().Placeholder.Should().HaveLength(LayoutTemplateSyntax.MaxPlaceholderLength);
    }

    [TestCase("{1}{Title}")]
    [TestCase("{Title }")]
    public void Parse_ANameThatIsNoField_IsUnknown(string template)
    {
        LayoutTemplateSyntax.Parse(template).Errors.Should()
            .Contain(error => error.Code == LayoutTemplateErrorCode.UnknownPlaceholder);
    }

    [TestCase("{TrackNumber:000000} {Title}", "TrackNumber")]
    [TestCase("{TrackNumber:} {Title}", "TrackNumber")]
    [TestCase("{TrackNumber:#0} {Title}", "TrackNumber")]
    [TestCase("{Artist:00} {Title}", "Artist")]
    public void Parse_AnInvalidNumberFormat_NamesThePlaceholder(string template, string name)
    {
        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse(template);

        result.Errors.Should().Equal(new LayoutTemplateError(LayoutTemplateErrorCode.InvalidNumberFormat, 0, name));
    }

    [TestCase("{Title", LayoutTemplateErrorCode.UnbalancedBrace, 0)]
    [TestCase("{Ti{tle}", LayoutTemplateErrorCode.UnbalancedBrace, 0)]
    [TestCase("{Title}}x", LayoutTemplateErrorCode.UnbalancedBrace, 7)]
    [TestCase("[{Year} {Title}", LayoutTemplateErrorCode.UnbalancedSection, 0)]
    [TestCase("{Year}] {Title}", LayoutTemplateErrorCode.UnbalancedSection, 6)]
    [TestCase("[{Year} [x]] {Title}", LayoutTemplateErrorCode.NestedSection, 8)]
    [TestCase("[{Artist}/]{Title}", LayoutTemplateErrorCode.FolderInSection, 9)]
    [TestCase("{Artist}/{Album}", LayoutTemplateErrorCode.MissingTitle, 9)]
    [TestCase("{Title}/", LayoutTemplateErrorCode.MissingTitle, 8)]
    [TestCase("/{Artist}/{Title}", LayoutTemplateErrorCode.Rooted, 0)]
    [TestCase("{Artist}//{Title}", LayoutTemplateErrorCode.EmptyFolder, 9)]
    [TestCase("{Artist}/../{Title}", LayoutTemplateErrorCode.ParentFolder, 9)]
    [TestCase("./{Title}", LayoutTemplateErrorCode.ParentFolder, 0)]
    public void Parse_BrokenStructure_ReportsWhereItStarts(
        string template,
        LayoutTemplateErrorCode code,
        int position)
    {
        ExpectError(template, code, position);
    }

    [Test]
    public void Parse_TheTitleInAnOptionalSection_Counts()
    {
        LayoutTemplateSyntax.Parse("{Artist}/[{Title}]").IsValid.Should().BeTrue();
    }

    [Test]
    public void Parse_DotsAroundOtherText_AreNoParentFolder()
    {
        LayoutTemplateSyntax.Parse("...{Album}/{Title}").IsValid.Should().BeTrue();
    }

    private static void ExpectError(string? template, LayoutTemplateErrorCode code, int position)
    {
        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse(template);

        result.IsValid.Should().BeFalse();
        result.Template.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Should().Be(new LayoutTemplateError(code, position));
    }
}
