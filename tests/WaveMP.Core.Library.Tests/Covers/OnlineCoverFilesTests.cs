using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Library.Options;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Library.Tests.Covers;

[TestFixture]
public class OnlineCoverFilesTests
{
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5];

    private TemporaryDirectory _data = null!;
    private OnlineCoverFiles _files = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new TemporaryDirectory();
        _files = new OnlineCoverFiles(
            Microsoft.Extensions.Options.Options.Create(new OnlineCoverOptions { RootPath = _data.Combine("covers") }),
            NullLogger<OnlineCoverFiles>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _data.Dispose();
    }

    [Test]
    public void FileNameOf_Picture_IsTheIdWithTheExtensionOfItsType()
    {
        var id = Guid.Parse("019a6f2c-1111-7abc-8def-0123456789ab");

        _files.FileNameOf(id, _png).Should().Be("019a6f2c11117abc8def0123456789ab.png");
        _files.FileNameOf(id, "<svg/>"u8).Should().BeNull();
    }

    [Test]
    public async Task SaveAsync_ThenListAndDelete_ManagesTheFiles()
    {
        string name = _files.FileNameOf(Guid.NewGuid(), _png)!;
        File.WriteAllText(_data.Combine("covers-other.txt"), "x");

        (await _files.SaveAsync(name, _png)).Should().BeTrue();

        File.ReadAllBytes(_files.GetFullPath(name)!).Should().Equal(_png);
        _files.ListFileNames().Should().Equal(name);
        _files.Delete(name);
        _files.ListFileNames().Should().BeEmpty();
    }

    [Test]
    public void ListFileNames_ForeignFiles_AreIgnored()
    {
        Directory.CreateDirectory(_data.Combine("covers"));
        File.WriteAllText(_data.Combine("covers/notes.txt"), "x");

        _files.ListFileNames().Should().BeEmpty();
    }

    [TestCase("../cover.jpg")]
    [TestCase("cover.jpg")]
    [TestCase("019a6f2c11117abc8def0123456789ab.exe")]
    public void GetFullPath_ForeignName_IsNull(string name)
    {
        _files.GetFullPath(name).Should().BeNull();
    }
}
