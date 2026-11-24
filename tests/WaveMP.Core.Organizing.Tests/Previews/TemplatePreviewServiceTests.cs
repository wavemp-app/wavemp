using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Core.Organizing.Templates;
using WaveMP.Core.Scanning.Options;
using WaveMP.Core.Settings.Templates;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Tests.Previews;

[TestFixture]
public class TemplatePreviewServiceTests
{
    private static readonly TemplateSongRow _song = new(
        Guid.NewGuid(),
        "Faded",
        "Alan Walker",
        "Different World",
        "Alan Walker",
        false,
        2018,
        3,
        null,
        "Electronic",
        "Alan Walker - Faded",
        "flac",
        ["flac", "mp4", "jpg", "lrc"]);

    private IOrganizeRepository _repository = null!;
    private TemplatePreviewService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IOrganizeRepository>();
        _repository.GetSampleTemplateSongsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([_song]);
        _repository.GetTemplateSongsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([_song]);
        _service = new TemplatePreviewService(
            _repository,
            Microsoft.Extensions.Options.Options.Create(new ScanOptions()));
    }

    [Test]
    public async Task PreviewAsync_WithoutTracksAndSamples_ShowsTheFirstSongsOfTheLibrary()
    {
        TemplatePreviewResult result = await _service.PreviewAsync(
            new TemplatePreviewRequest("{AlbumArtist}/{Album}/{TrackNumber:00} {Title}"));

        result.IsValid.Should().BeTrue();
        result.IsFolderBased.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        TemplatePreviewPath path = result.Paths.Should().ContainSingle().Which;
        path.TrackId.Should().Be(_song.TrackId);
        path.Extension.Should().Be("flac");
        path.Path.RelativePathOf(path.Extension).Should().Be("Alan Walker/Different World/03 Faded.flac");
        await _repository.Received(1)
            .GetSampleTemplateSongsAsync(TemplatePreviewService.DefaultSongCount, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PreviewAsync_WithTracks_LoadsAtMostTheLimit()
    {
        Guid[] ids = [.. Enumerable.Range(0, 15).Select(_ => Guid.NewGuid())];

        await _service.PreviewAsync(new TemplatePreviewRequest("{Title}", ids));

        await _repository.Received(1).GetTemplateSongsAsync(
            Arg.Is<IReadOnlyList<Guid>>(list => list.SequenceEqual(ids.Take(TemplatePreviewService.MaxSongs))),
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceiveWithAnyArgs().GetSampleTemplateSongsAsync(default, default);
    }

    [Test]
    public async Task PreviewAsync_WithSamples_RendersThemWithoutTheLibrary()
    {
        TemplateSample sample = new(new TemplateValues("Song", Artist: "Band"), " .MP3 ");

        TemplatePreviewResult result = await _service.PreviewAsync(
            new TemplatePreviewRequest("{Artist} - {Title}", Samples: [sample]));

        result.IsFolderBased.Should().BeFalse();
        TemplatePreviewPath path = result.Paths.Should().ContainSingle().Which;
        path.TrackId.Should().BeNull();
        path.Extension.Should().Be("mp3");
        path.Path.BaseName.Should().Be("Band - Song");
        await _repository.DidNotReceiveWithAnyArgs().GetSampleTemplateSongsAsync(default, default);
    }

    [Test]
    public async Task PreviewAsync_ASampleWithAnOddExtension_HasNone()
    {
        TemplateSample sample = new(new TemplateValues("Song"), "m/p3");

        TemplatePreviewResult result = await _service.PreviewAsync(
            new TemplatePreviewRequest("{Title}", Samples: [sample]));

        result.Paths.Single().Extension.Should().BeNull();
    }

    [Test]
    public async Task PreviewAsync_AnInvalidTemplate_ReturnsItsProblemsWithoutLoadingSongs()
    {
        TemplatePreviewResult result = await _service.PreviewAsync(new TemplatePreviewRequest("{Artist}/{Nope}"));

        result.IsValid.Should().BeFalse();
        result.Paths.Should().BeEmpty();
        result.Errors.Select(error => error.Code).Should().Equal(
            LayoutTemplateErrorCode.UnknownPlaceholder,
            LayoutTemplateErrorCode.MissingTitle);
        await _repository.DidNotReceiveWithAnyArgs().GetSampleTemplateSongsAsync(default, default);
    }
}
