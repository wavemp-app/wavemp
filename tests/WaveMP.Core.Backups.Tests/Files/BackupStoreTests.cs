using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Backups.Options;
using WaveMP.Core.Backups.Services;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Backups.Tests.Files;

[TestFixture]
public class BackupStoreTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    private TemporaryDirectory _data = null!;
    private string _root = null!;
    private BackupStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new TemporaryDirectory();
        _root = _data.Combine("backups");
        _store = CreateStore(_root);
    }

    [TearDown]
    public void TearDown()
    {
        _data.Dispose();
    }

    [Test]
    public void List_WithoutFolder_ReturnsNothing()
    {
        _store.List().Should().BeEmpty();
    }

    [Test]
    public void List_Always_ReturnsTheExportsNewestFirstWithSizeAndTriggerAndIgnoresOtherFiles()
    {
        WriteExport(BackupFileNames.CreateId(_now.AddDays(-2), BackupTrigger.Schedule), 10);
        WriteExport(BackupFileNames.CreateId(_now, BackupTrigger.Manual), 30);
        WriteExport(BackupFileNames.CreateId(_now, BackupTrigger.Manual, 2), 40);
        WriteExport(BackupFileNames.CreateId(_now.AddDays(-1), BackupTrigger.Migration), 20);
        File.WriteAllText(Path.Combine(_root, "notes.dump"), "other");
        File.WriteAllText(Path.Combine(_root, "wavemp-20261116T040000Z-manual.dump.partial"), "unfinished");

        IReadOnlyList<BackupFile> files = _store.List();

        files.Select(file => file.FileName).Should().Equal(
            "wavemp-20261116T030000Z-manual-2.dump",
            "wavemp-20261116T030000Z-manual.dump",
            "wavemp-20261115T030000Z-migration.dump",
            "wavemp-20261114T030000Z-schedule.dump");
        files[2].Should().Be(new BackupFile(
            "wavemp-20261115T030000Z-migration",
            "wavemp-20261115T030000Z-migration.dump",
            _now.AddDays(-1),
            20,
            BackupTrigger.Migration));
    }

    [Test]
    public void Prepare_WithoutFolder_CreatesItAndRemovesUnfinishedExports()
    {
        string id = _store.Prepare(_now, BackupTrigger.Schedule);
        File.WriteAllText(_store.PartialPathOf(id), "interrupted");

        string next = _store.Prepare(_now.AddMinutes(1), BackupTrigger.Manual);

        Directory.Exists(_root).Should().BeTrue();
        Directory.EnumerateFiles(_root).Should().BeEmpty();
        next.Should().Be("wavemp-20261116T030100Z-manual");
    }

    [Test]
    public void Prepare_WhenTheSecondIsTaken_ReturnsTheNextFreeSequence()
    {
        WriteExport(BackupFileNames.CreateId(_now, BackupTrigger.Manual), 1);
        WriteExport(BackupFileNames.CreateId(_now, BackupTrigger.Manual, 2), 1);

        _store.Prepare(_now, BackupTrigger.Manual).Should().Be("wavemp-20261116T030000Z-manual-3");
    }

    [Test]
    public void Prepare_WhenTheFolderCannotBeCreated_ThrowsFolderUnwritable()
    {
        // A file where the folder should be.
        File.WriteAllText(_data.Combine("blocked"), "file");
        BackupStore store = CreateStore(_data.Combine("blocked"));

        Action prepare = () => store.Prepare(_now, BackupTrigger.Schedule);

        prepare.Should().Throw<BackupFailedException>()
            .Which.Error.Should().Be(BackupError.FolderUnwritable);
    }

    [Test]
    public void Complete_WithWrittenFile_TurnsItIntoAnExport()
    {
        string id = _store.Prepare(_now, BackupTrigger.Manual);
        File.WriteAllBytes(_store.PartialPathOf(id), new byte[123]);

        BackupFile file = _store.Complete(id);

        file.Should().Be(new BackupFile(id, id + ".dump", _now, 123, BackupTrigger.Manual));
        File.Exists(_store.PartialPathOf(id)).Should().BeFalse();
        _store.List().Should().Equal(file);
    }

    [Test]
    public void Complete_WithoutWrittenFile_ThrowsFolderUnwritable()
    {
        string id = _store.Prepare(_now, BackupTrigger.Manual);

        Action complete = () => _store.Complete(id);

        complete.Should().Throw<BackupFailedException>()
            .Which.Error.Should().Be(BackupError.FolderUnwritable);
    }

    [Test]
    public void Discard_WithWrittenFile_RemovesIt()
    {
        string id = _store.Prepare(_now, BackupTrigger.Manual);
        File.WriteAllText(_store.PartialPathOf(id), "failed");

        _store.Discard(id);
        _store.Discard(id);

        File.Exists(_store.PartialPathOf(id)).Should().BeFalse();
    }

    [Test]
    public void FindAndDelete_WithExport_FindAndDeleteIt()
    {
        string id = BackupFileNames.CreateId(_now, BackupTrigger.Schedule);
        WriteExport(id, 5);

        _store.Find(id).Should().NotBeNull();
        _store.Delete(id).Should().BeTrue();
        _store.Find(id).Should().BeNull();
        _store.Delete(id).Should().BeFalse();
    }

    [TestCase("notes")]
    [TestCase("../backups/wavemp-20261116T030000Z-schedule")]
    public void FindAndDelete_WithOtherName_FindAndDeleteNothing(string id)
    {
        File.WriteAllText(Path.Combine(_data.CreateDirectory("backups"), "notes.dump"), "other");

        _store.Find(id).Should().BeNull();
        _store.Delete(id).Should().BeFalse();
        File.Exists(Path.Combine(_root, "notes.dump")).Should().BeTrue();
    }

    [Test]
    public void ApplyRetention_WithMoreExports_DeletesTheOldestBeyondTheNumber()
    {
        for (int day = 0; day < 5; day++)
        {
            WriteExport(BackupFileNames.CreateId(_now.AddDays(-day), BackupTrigger.Schedule), 1);
        }

        File.WriteAllText(Path.Combine(_root, "keep-me.dump"), "other");

        IReadOnlyList<BackupFile> deleted = _store.ApplyRetention(3);

        deleted.Select(file => file.CreatedAt).Should().Equal(_now.AddDays(-3), _now.AddDays(-4));
        _store.List().Select(file => file.CreatedAt).Should().Equal(_now, _now.AddDays(-1), _now.AddDays(-2));
        File.Exists(Path.Combine(_root, "keep-me.dump")).Should().BeTrue();
    }

    [Test]
    public void ApplyRetention_WithFewerExports_DeletesNothing()
    {
        WriteExport(BackupFileNames.CreateId(_now, BackupTrigger.Schedule), 1);

        _store.ApplyRetention(7).Should().BeEmpty();
        _store.List().Should().HaveCount(1);
    }

    private static BackupStore CreateStore(string root)
    {
        return new BackupStore(
            Microsoft.Extensions.Options.Options.Create(new BackupOptions { RootPath = root }),
            NullLogger<BackupStore>.Instance);
    }

    private void WriteExport(string id, int size)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, id + BackupFileNames.Extension), new byte[size]);
    }
}
