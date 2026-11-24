using AwesomeAssertions;
using WaveMP.Core.Organizing.Templates;
using WaveMP.Core.Settings.Templates;

namespace WaveMP.Core.Organizing.Tests.Templates;

[TestFixture]
public class LayoutTemplateRendererTests
{
    private const int MaxPathBytes = 2000;
    private const string Default = "{AlbumArtist}/{Album}/{TrackNumber:00} {Title}";

    private static readonly TemplateValues _song = new(
        "Faded",
        Artist: "Alan Walker",
        AlbumArtist: "Alan Walker",
        Album: "Different World",
        Year: 2018,
        TrackNumber: 3,
        DiscNumber: 1,
        Genre: "Electronic");

    [Test]
    public void Render_TheDefaultTemplate_MakesFoldersOfAlbumArtistAndAlbum()
    {
        RenderedPath path = Render(Default, _song, "mp3");

        path.Folders.Should().Equal("Alan Walker", "Different World");
        path.BaseName.Should().Be("03 Faded");
        path.RelativePathOf("mp3").Should().Be("Alan Walker/Different World/03 Faded.mp3");
        path.DirectoryPath.Should().Be("Alan Walker/Different World");
        path.IsTooLong.Should().BeFalse();
    }

    [Test]
    public void Render_AFileBasedTemplate_HasNoFolders()
    {
        RenderedPath path = Render("{Artist} - {Title}", _song);

        path.Folders.Should().BeEmpty();
        path.RelativePathOf("flac").Should().Be("Alan Walker - Faded.flac");
        path.RelativePathOf(null).Should().Be("Alan Walker - Faded");
    }

    [Test]
    public void Render_AllPlaceholders_TakeTheirValues()
    {
        RenderedPath path = Render(
            "{Genre}/{Year}/{AlbumArtist} - {Album}/{DiscNumber}-{TrackNumber:000} {Artist} {Title}",
            _song);

        path.RelativePathOf(null).Should().Be("Electronic/2018/Alan Walker - Different World/1-003 Alan Walker Faded");
    }

    [Test]
    public void Render_AnOptionalSectionWithoutValue_Vanishes()
    {
        const string Template = "{Album}/[{DiscNumber}-]{TrackNumber:00} {Title}";

        Render(Template, _song).BaseName.Should().Be("1-03 Faded");
        Render(Template, _song with { DiscNumber = null }).BaseName.Should().Be("03 Faded");
    }

    [Test]
    public void Render_AMissingAlbumArtist_FallsBackToTheArtist()
    {
        RenderedPath path = Render(Default, _song with { AlbumArtist = null });

        path.Folders[0].Should().Be("Alan Walker");
    }

    [Test]
    public void Render_ACompilationWithoutAlbumArtist_GoesToVariousArtists()
    {
        RenderedPath path = Render(Default, _song with { AlbumArtist = null, IsCompilation = true });

        path.Folders[0].Should().Be(LayoutTemplateRenderer.VariousArtists);
    }

    [Test]
    public void Render_AMissingArtist_FallsBackToTheAlbumArtist()
    {
        RenderedPath path = Render("{Artist} - {Title}", _song with { Artist = null, AlbumArtist = "Band" });

        path.BaseName.Should().Be("Band - Faded");
    }

    [Test]
    public void Render_MissingValues_DropEmptyFoldersAndTrimSeparators()
    {
        TemplateValues loose = new("Faded");

        RenderedPath path = Render(Default, loose);
        RenderedPath flat = Render("{Artist} - {Title}", loose);

        path.Folders.Should().BeEmpty();
        path.BaseName.Should().Be("Faded");
        flat.BaseName.Should().Be("Faded");
    }

    [Test]
    public void Render_ValuesWithSlashesAndDots_StayOneNameBelowTheRoot()
    {
        TemplateValues song = _song with { AlbumArtist = "AC/DC", Album = "..", Title = "../../etc" };

        RenderedPath path = Render(Default, song);

        path.Folders.Should().Equal("AC_DC");
        path.BaseName.Should().Be("03 .._.._etc");
        path.Folders.Should().NotContain(folder => folder == "." || folder == ".." || folder.Contains('/'));
    }

    [Test]
    public void Render_SpecialNames_AreMadeSafe()
    {
        TemplateValues song = _song with
        {
            AlbumArtist = "CON",
            Album = ".Hidden Album.",
            Title = "Why? Because: \"Yes\"",
        };

        RenderedPath path = Render(Default, song);

        path.Folders.Should().Equal("CON_", "_Hidden Album");
        path.BaseName.Should().Be("03 Why_ Because_ _Yes");
    }

    [Test]
    public void Render_ANameWithNothingLeft_TakesTheFallback()
    {
        RenderedPath path = Render("{Title}", _song with { Title = "???" }, fallback: "Old Name");
        RenderedPath withoutFallback = Render("{Title}", _song with { Title = "???" });

        path.BaseName.Should().Be("Old Name");
        withoutFallback.BaseName.Should().Be(LayoutTemplateRenderer.EmptyBaseName);
    }

    [Test]
    public void Render_ALongTitle_LeavesRoomForTheExtension()
    {
        RenderedPath path = Render("{Title}", _song with { Title = new string('t', 400) }, "flac");

        path.BaseName.Should().HaveLength(PathNameSanitizer.MaxNameBytes - ".flac".Length);
        path.IsTooLong.Should().BeFalse();
    }

    [Test]
    public void Render_APathLongerThanTheScanAllows_IsTooLong()
    {
        TemplateValues song = _song with { AlbumArtist = new string('a', 200), Album = new string('b', 200) };

        RenderedPath path = LayoutTemplateRenderer.Render(Parse(Default), song, 300, "mp3");

        path.IsTooLong.Should().BeTrue();
    }

    [Test]
    public void Render_LiteralText_IsSanitizedToo()
    {
        RenderedPath path = Render("Music: {Artist}?/{Title}", _song);

        path.Folders.Should().Equal("Music_ Alan Walker");
    }

    [Test]
    public void Render_DecomposedValues_AreComposed()
    {
        RenderedPath path = Render("{Artist}/{Title}", _song with { Artist = "Beyoncé" });

        path.Folders.Should().Equal("Beyoncé");
    }

    private static RenderedPath Render(
        string template,
        TemplateValues values,
        string? extension = null,
        string? fallback = null)
    {
        return LayoutTemplateRenderer.Render(Parse(template), values, MaxPathBytes, extension, fallback);
    }

    private static LayoutTemplate Parse(string template)
    {
        LayoutTemplateParseResult result = LayoutTemplateSyntax.Parse(template);
        result.IsValid.Should().BeTrue();
        return result.Template!;
    }
}
