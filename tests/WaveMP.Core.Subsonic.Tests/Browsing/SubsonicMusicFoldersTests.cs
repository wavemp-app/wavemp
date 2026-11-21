using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Subsonic.Browsing;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Subsonic.Tests.Browsing;

[TestFixture]
public class SubsonicMusicFoldersTests
{
    private static readonly MediaSourceInfo _music = new(MediaSource.DefaultId, "Music", "/media", true, true);

    private static readonly MediaSourceInfo _videos = new(
        Guid.Parse("019a1111-2222-7333-8444-555555555555"),
        "Videos",
        "/videos",
        true,
        false);

    private static readonly MediaSourceInfo _archive = new(
        Guid.Parse("019a6666-7777-7888-8999-aaaaaaaaaaaa"),
        "Archive",
        "/archive",
        false,
        false);

    private SubsonicMusicFolders _folders = null!;

    [SetUp]
    public void SetUp()
    {
        IMediaSourceCatalog catalog = Substitute.For<IMediaSourceCatalog>();
        catalog.Sources.Returns([_archive, _music, _videos]);
        _folders = new SubsonicMusicFolders(catalog);
    }

    [Test]
    public void List_Always_HasOneFolderPerActiveSourceWithItsName()
    {
        IReadOnlyList<SubsonicElement> list = _folders.List();

        list.Select(folder => folder.Name).Should().AllBe("musicFolder");
        list.Select(Id).Should().Equal(SubsonicIds.DefaultMusicFolderId, SubsonicIds.MusicFolder(_videos.Id));
        list.Select(folder => Attribute(folder, "name")).Should().Equal("Music", "Videos");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Resolve_WithoutFolder_FiltersNothing(string? value)
    {
        SubsonicParameters parameters = value is null
            ? SubsonicParameters.Empty
            : TestParameters.Of(("musicFolderId", value));

        _folders.Resolve(parameters).Should().Be(SubsonicMusicFolderFilter.All);
    }

    [Test]
    public void Resolve_FolderOfASource_FiltersByTheSource()
    {
        string videos = SubsonicIds.MusicFolder(_videos.Id).ToString(System.Globalization.CultureInfo.InvariantCulture);

        _folders.Resolve(TestParameters.Of(("musicFolderId", "1")))!.SourceId.Should().Be(MediaSource.DefaultId);
        _folders.Resolve(TestParameters.Of(("musicFolderId", videos)))!.SourceId.Should().Be(_videos.Id);
    }

    [Test]
    public void Resolve_FolderOfADeactivatedSource_StillFiltersByIt()
    {
        string archive = SubsonicIds.MusicFolder(_archive.Id)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

        _folders.Resolve(TestParameters.Of(("musicFolderId", archive)))!.SourceId.Should().Be(_archive.Id);
    }

    [TestCase("0")]
    [TestCase("2")]
    [TestCase("-1")]
    [TestCase("music")]
    public void Resolve_UnknownFolder_ReturnsNull(string value)
    {
        _folders.Resolve(TestParameters.Of(("musicFolderId", value))).Should().BeNull();
    }

    private static int? Id(SubsonicElement element)
    {
        return Attribute(element, "id") as int?;
    }

    private static object? Attribute(SubsonicElement element, string name)
    {
        return element.Attributes.FirstOrDefault(attribute => attribute.Key == name).Value;
    }
}
